using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>AIR deploy: spawn a cargo aircraft (vanilla UH-90, or MC-260/VL-49 if present), fly to the front, T jump.</summary>
    internal static class GaAirLift
    {
        internal static bool Waiting;
        internal static bool Flying;
        internal static bool Steer;
        internal static Aircraft Ac;
        internal static AircraftDefinition Picked;
        internal static float Pitch;
        internal static float Roll;
        internal static float Yaw;
        internal static float Throttle;

        private static float _waitUntil;
        private static Vector3 _front;
        private static float _frontAt;
        private static AircraftDefinition _def;
        private static bool _gearsUp;
        private static int _phase;
        private static float _hdg;
        private static bool _camJump;
        private static bool _aiOn;
        private static bool _dropReady;
        private static bool _punched;
        private static Vector3 _home;
        private static Vector3 _runwayFwd;
        private static MethodInfo _startAi;
        private static float _holdAgl;

        internal static bool CanStart()
        {
            AircraftDefinition def = FindDef();
            Airbase ab = GaHangar.FieldAirbase();
            if (def == null || ab == null)
                return false;
            try { return ab.CanSpawnAircraft(def); }
            catch { return false; }
        }

        internal static float HalfCost(AircraftDefinition d)
        {
            if (d == null)
                return 0f;
            float v = 0f;
            try { v = d.value; }
            catch { v = 0f; }
            if (v < 0f)
                v = 0f;
            return v * 0.5f;
        }

        internal static string PickName()
        {
            AircraftDefinition d = Picked;
            if (d == null)
                return "PARADROP";
            try
            {
                if (!string.IsNullOrEmpty(d.code))
                    return d.code.ToUpperInvariant();
                if (!string.IsNullOrEmpty(d.unitName))
                    return d.unitName.ToUpperInvariant();
            }
            catch { }
            return "LIFT";
        }

        internal static bool BlockGroundEject(Aircraft ac)
        {
            if (!Flying || ac == null || Ac == null)
                return false;
            if (!object.ReferenceEquals(ac, Ac))
                return false;
            float agl = AglOf(ac.transform.position);
            return !Steer || agl < 90f;
        }

        internal static bool Start()
        {
            AircraftDefinition def = FindDef();
            Airbase ab = GaHangar.FieldAirbase();
            if (def == null || ab == null)
                return false;
            Player player = null;
            try
            {
                if (!GameManager.GetLocalPlayer(out player) || player == null)
                    return false;
            }
            catch
            {
                return false;
            }
            Waiting = true;
            Flying = false;
            Steer = false;
            Ac = null;
            _waitUntil = Time.unscaledTime + 10f;
            try
            {
                if (!ab.CanSpawnAircraft(def))
                {
                    Waiting = false;
                    return false;
                }
                Airbase.TrySpawnResult result = ab.TrySpawnAircraft(
                    player, def, new LiveryKey(0), new Loadout(), 1f);
                if (!result.Allowed)
                {
                    Waiting = false;
                    return false;
                }
            }
            catch
            {
                Waiting = false;
                return false;
            }
            return true;
        }

        internal static void Attach(Aircraft ac)
        {
            Waiting = false;
            if (ac == null)
                return;
            Ac = ac;
            Flying = true;
            Steer = false;
            _frontAt = 0f;
            _gearsUp = false;
            _phase = 0;
            _hdg = ac.transform.eulerAngles.y;
            _home = ac.transform.position;
            _runwayFwd = ac.transform.forward;
            _runwayFwd.y = 0f;
            if (_runwayFwd.sqrMagnitude < 0.01f)
                _runwayFwd = Vector3.forward;
            _runwayFwd.Normalize();
            _aiOn = false;
            _holdAgl = 0f;
            try { ac.SetFlightAssist(true); }
            catch { }
            try { ac.SetGear(true); }
            catch { }
            try { GameManager.flightControlsEnabled = true; }
            catch { }
            HandTaxi();
        }

        internal static void Clear()
        {
            Waiting = false;
            Flying = false;
            Steer = false;
            Ac = null;
            Pitch = 0f;
            Roll = 0f;
            Yaw = 0f;
            Throttle = 0f;
            _gearsUp = false;
            _phase = 0;
            _camJump = false;
            _aiOn = false;
            _dropReady = false;
            _punched = false;
            _holdAgl = 0f;
        }

        internal static void Tick()
        {
            if (Waiting && Time.unscaledTime > _waitUntil)
                Clear();
            if (!Flying || Ac == null)
                return;
            try
            {
                if (Ac.disabled)
                {
                    GaMode.OnLiftCrash();
                    return;
                }
            }
            catch
            {
                GaMode.OnLiftCrash();
                return;
            }
            Vector3 pos = Ac.transform.position;
            float spd = Speed();
            float agl = AglOf(pos);
            TickDropCall(pos, agl);
            TickMissilePunch();
            if (!Flying || Ac == null)
                return;
            if (!Steer)
            {
                HandTaxi();
                StayAirborne();
                if (!ReadyToSteer(agl, spd))
                    return;
                BeginSteer(agl);
            }
            FlyFront(pos, agl);
            if (!_gearsUp && agl > 140f)
            {
                _gearsUp = true;
                try { Ac.SetGear(false); }
                catch { }
            }
        }

        internal static bool PassCamSwitch(CameraStateManager mgr, CameraBaseState state)
        {
            if (!Flying || mgr == null || state == null)
                return true;
            if (state == mgr.orbitState || state == mgr.freeState)
                return true;
            if (_camJump)
                return true;
            CameraBaseState want = mgr.currentState == mgr.orbitState
                ? mgr.freeState
                : mgr.orbitState;
            if (want == mgr.orbitState)
                EnsureFollow(mgr);
            _camJump = true;
            try { mgr.SwitchState(want); }
            catch { }
            _camJump = false;
            return false;
        }

        internal static void ApplyCam(CameraStateManager mgr)
        {
            if (Ac == null || mgr == null)
                return;
            if (mgr.currentState == mgr.orbitState)
                return;
            Camera cam = mgr.mainCamera;
            if (cam == null)
                cam = GaPerfFrameGate.Cam();
            if (cam == null)
                return;
            Transform t = Ac.transform;
            Vector3 tail = GaChute.ExitPoint(Ac, 0);
            cam.transform.position = tail + t.up * 0.65f + t.forward * 0.8f;
            Vector3 look = -t.forward * 0.92f - t.up * 0.16f;
            if (look.sqrMagnitude < 0.01f)
                look = -t.forward;
            cam.transform.rotation = Quaternion.LookRotation(look.normalized, t.up);
            try { CameraStateManager.cameraMode = CameraMode.free; }
            catch { }
            try { CameraStateManager.enableMouseLook = false; }
            catch { }
        }

        internal static void ApplyAxis(PilotPlayerState state)
        {
            if (state == null)
                return;
            state.pitchInput = Pitch;
            state.rollInput = Roll;
            state.yawInput = Yaw;
            PushInputs(GaPilotAc.Of(state));
        }

        internal static void ApplyThrottle(PilotPlayerState state)
        {
            PushInputs(state != null ? GaPilotAc.Of(state) : Ac);
        }

        internal static string Hint()
        {
            if (Waiting)
                return PickName() + "  spawning…";
            if (!Flying || Ac == null)
                return "";
            float dist = Vector3.Distance(Ac.transform.position, Front());
            float agl = GaPhys.Agl(Ac.transform.position, 2500f);
            string aglS = agl < 0f ? "----" : Mathf.RoundToInt(agl).ToString();
            string stage = Steer ? (_phase >= 3 ? "FRONT" : "CLIMB") : TaxiHint();
            string view = InOrbit() ? "ORBIT" : "TAIL";
            string drop = _dropReady ? "  DROP ZONE" : "";
            return PickName() + "  " + view + "  " + stage + drop + "  "
                + Mathf.RoundToInt(dist / 1000f).ToString()
                + "km   AGL " + aglS + "m   SWITCH VIEW orbit  T jump";
        }

        internal static void PushPower(Aircraft ac)
        {
            if (ac == null)
                ac = Ac;
            if (ac == null)
                return;
            try
            {
                ControlInputs inputs = ac.GetInputs();
                if (inputs == null)
                    return;
                inputs.throttle = Throttle;
                inputs.brake = 0f;
            }
            catch { }
        }

        internal static void PushInputs(Aircraft ac)
        {
            if (ac == null)
                ac = Ac;
            if (ac == null)
                return;
            try
            {
                ControlInputs inputs = ac.GetInputs();
                if (inputs == null)
                    return;
                inputs.pitch = Pitch;
                inputs.roll = Roll;
                inputs.yaw = Yaw;
                inputs.throttle = Throttle;
                inputs.brake = 0f;
            }
            catch { }
        }

        private static float Speed()
        {
            try { return Ac.speed; }
            catch
            {
                try
                {
                    if (Ac.rb != null)
                        return Ac.rb.velocity.magnitude;
                }
                catch { }
            }
            return 0f;
        }

        private static float AglOf(Vector3 pos)
        {
            float agl = -1f;
            try { agl = Ac.radarAlt; }
            catch { }
            if (agl < 0f)
                agl = GaPhys.Agl(pos, 2500f);
            return agl;
        }

        private static Pilot Crew()
        {
            return CrewOf(Ac);
        }

        private static Pilot CrewOf(Aircraft ac)
        {
            if (ac == null)
                return null;
            try
            {
                Pilot[] pilots = ac.pilots;
                if (pilots != null && pilots.Length > 0)
                    return pilots[0];
            }
            catch { }
            return null;
        }

        internal static void SendHome()
        {
            Aircraft ac = Ac;
            if (ac == null)
                return;
            Steer = false;
            Flying = false;
            Pilot p = null;
            try
            {
                Pilot[] pilots = ac.pilots;
                if (pilots != null && pilots.Length > 0)
                    p = pilots[0];
            }
            catch { }
            EnsureAi(p);
            if (p == null)
                return;
            try
            {
                if (p.AILandingState == null)
                    p.AILandingState = new AIPilotLandingState();
                p.SwitchState(p.AILandingState);
            }
            catch { }
        }

        private static readonly string[] ReadyCall = new string[] { "Ready.", "Ready.", "READY!" };

        private static void TickDropCall(Vector3 pos, float agl)
        {
            if (_dropReady || !Steer)
                return;
            if (agl < 120f)
                return;
            Vector3 dest = Front();
            dest.y = pos.y;
            if ((dest - pos).sqrMagnitude > 2500f * 2500f)
                return;
            _dropReady = true;
            GaRadio.Burst("JUMPMASTER", ReadyCall, 0.48f);
        }

        private static void TickMissilePunch()
        {
            if (_punched || !Flying || Ac == null)
                return;
            if (!MissileUnavoidable(Ac))
                return;
            _punched = true;
            GaRadio.Say("JUMPMASTER", "Eject! Eject! Eject!", false, 10);
            GaMode.ForceHop();
        }

        private static bool MissileUnavoidable(Aircraft ac)
        {
            Missile m = null;
            try
            {
                MissileWarning warn = ac.GetMissileWarningSystem();
                if (warn == null || !warn.TryGetNearestIncoming(out m) || m == null)
                    return false;
            }
            catch
            {
                return false;
            }
            Vector3 ap = ac.transform.position;
            Vector3 mp = m.transform.position;
            Vector3 to = ap - mp;
            float dist = to.magnitude;
            if (dist > 160f)
                return false;
            Vector3 mv = Vector3.zero;
            try
            {
                if (m.rb != null)
                    mv = m.rb.velocity;
            }
            catch { }
            if (mv.sqrMagnitude < 1f)
            {
                try { mv = m.transform.forward * Mathf.Max(m.speed, 1f); }
                catch { }
            }
            Vector3 av = Vector3.zero;
            try
            {
                if (ac.rb != null)
                    av = ac.rb.velocity;
            }
            catch { }
            if (to.sqrMagnitude < 0.01f)
                return dist < 40f;
            float closing = Vector3.Dot(mv - av, to.normalized);
            if (closing < 35f && dist > 50f)
                return false;
            float tti = closing > 1f ? dist / closing : 99f;
            return dist < 42f || tti < 0.5f;
        }

        private static void FlyFront(Vector3 pos, float agl)
        {
            Throttle = 1f;
            try
            {
                ControlInputs inputs = Ac.GetInputs();
                if (inputs != null)
                {
                    inputs.throttle = 1f;
                    inputs.brake = 0f;
                }
            }
            catch { }
            Vector3 aim;
            float hold;
            float bank;
            bool climb;
            if (!AimFront(Ac, pos, agl, out aim, out hold, out bank, out climb))
                return;
            _phase = climb ? 1 : 3;
            try
            {
                Autopilot ap = Ac.autopilot;
                if (ap == null)
                    return;
                ap.AutoAim(aim.ToGlobalPosition(), true, true, false, 0.55f, bank, false, hold, Vector3.zero);
            }
            catch { }
        }

        private static void EnsureAi(Pilot p)
        {
            if (p == null)
                return;
            if (!_aiOn)
            {
                try
                {
                    if (_startAi == null)
                        _startAi = AccessTools.Method(typeof(Pilot), "SetStartingAiState");
                    if (_startAi != null)
                        _startAi.Invoke(p, null);
                    _aiOn = true;
                }
                catch
                {
                    _aiOn = false;
                }
            }
            try
            {
                if (p.AITaxiState == null)
                    p.AITaxiState = new AIPilotTaxiState();
                if (p.AITakeoffState == null)
                    p.AITakeoffState = new AIPilotTakeoffState();
                if (p.AILandingState == null)
                    p.AILandingState = new AIPilotLandingState();
                if (p.AICombatState == null && p.aircraft != null)
                    p.AICombatState = new AIPilotCombatModes(p.aircraft);
            }
            catch { }
        }

        private static void HandTaxi()
        {
            Pilot p = Crew();
            if (p == null)
                return;
            PilotBaseState cur = p.currentState;
            if (cur is AIPilotTaxiState || cur is AIPilotTakeoffState)
                return;
            if (cur is AIPilotCombatModes)
                return;
            EnsureAi(p);
            try { p.SwitchState(p.AITaxiState); }
            catch { }
        }

        private static void StayAirborne()
        {
            KickLanding(Crew());
        }

        private static void KickLanding(Pilot p)
        {
            if (p == null)
                return;
            if (!(p.currentState is AIPilotLandingState))
                return;
            try
            {
                if (p.AICombatState == null && p.aircraft != null)
                    p.AICombatState = new AIPilotCombatModes(p.aircraft);
                if (p.AICombatState != null)
                    p.SwitchState(p.AICombatState);
            }
            catch { }
        }

        internal static void TickHostiles()
        {
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return;
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i] as Aircraft;
                if (ac == null || ac.disabled)
                    continue;
                if (object.ReferenceEquals(ac, Ac))
                    continue;
                if (!GaChute.IsDropship(ac))
                    continue;
                float agl = -1f;
                try { agl = ac.radarAlt; }
                catch { }
                if (agl < 70f)
                    continue;
                Pilot p = CrewOf(ac);
                KickLanding(p);
                if (agl > 140f)
                {
                    try { ac.SetGear(false); }
                    catch { }
                }
            }
        }

        internal static bool GuideDropship(Aircraft ac, out Vector3 aim, out float hold, out float bank)
        {
            aim = Vector3.zero;
            hold = 400f;
            bank = 12f;
            if (GaSupply.SteerAim(ac, out aim, out hold, out bank))
                return true;
            if (GaSupport.SteerAim(ac, out aim, out hold, out bank))
                return true;
            if (ac == null || ac.disabled)
                return false;
            if (!GaChute.IsDropship(ac))
                return false;
            float agl = -1f;
            try { agl = ac.radarAlt; }
            catch { }
            if (agl < 70f)
                return false;
            Pilot p = CrewOf(ac);
            if (p != null)
            {
                PilotBaseState cur = p.currentState;
                if (cur is AIPilotTaxiState || cur is AIPilotTakeoffState || cur is AIPilotLandingState)
                    return false;
            }
            bool climb;
            return AimFront(ac, ac.transform.position, agl, out aim, out hold, out bank, out climb);
        }

        private static bool AimFront(Aircraft ac, Vector3 pos, float agl, out Vector3 aim, out float hold, out float bank, out bool climb)
        {
            aim = pos;
            hold = agl;
            bank = 12f;
            climb = false;
            if (ac == null)
                return false;
            Vector3 dest = ClosestFront(ac, pos);
            Vector3 dir = dest - pos;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1f)
            {
                dir = ac.transform.forward;
                dir.y = 0f;
            }
            if (dir.sqrMagnitude < 0.01f)
            {
                dir = _runwayFwd.sqrMagnitude > 0.01f ? _runwayFwd : Vector3.forward;
                dir.y = 0f;
            }
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();
            const float cruise = 4000f;
            const float ahead = 4200f;
            climb = agl < cruise - 80f;
            aim = pos + dir * ahead;
            aim.y = pos.y;
            float dt = Time.deltaTime;
            if (dt > 0.05f)
                dt = 0.05f;
            if (climb)
            {
                _holdAgl = Mathf.MoveTowards(_holdAgl, cruise, 22f * dt);
                if (_holdAgl < agl)
                    _holdAgl = agl;
                if (_holdAgl > agl + 30f)
                    _holdAgl = agl + 30f;
            }
            else
                _holdAgl = cruise;
            hold = _holdAgl;
            bank = 8f;
            return true;
        }

        private static bool ReadyToSteer(float agl, float spd)
        {
            if (InHangar() && agl < 80f)
                return false;
            Pilot p = Crew();
            PilotBaseState cur = p != null ? p.currentState : null;
            if (cur is AIPilotTaxiState)
                return false;
            if (agl < 80f)
                return false;
            if (cur is AIPilotCombatModes && agl > 85f)
                return true;
            if (cur is AIPilotTakeoffState && agl > 110f && spd > 58f)
                return true;
            if (agl > 130f && spd > 62f)
                return true;
            return false;
        }

        private static void BeginSteer(float agl)
        {
            Steer = true;
            _hdg = Ac.transform.eulerAngles.y;
            Vector3 fwd = Ac.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.01f)
                _runwayFwd = fwd.normalized;
            _holdAgl = Mathf.Max(agl, 200f);
            if (agl > 3800f)
                _phase = 3;
            else
                _phase = 1;
            Pilot p = Crew();
            if (p == null || p.playerState == null)
                return;
            try { p.SwitchState(p.playerState); }
            catch { }
        }

        private static bool InHangar()
        {
            if (Ac == null)
                return true;
            Transform t = Ac.transform;
            Vector3 origin = t.position + t.up * 5f;
            int mask = GaPhys.WorldMask;
            RaycastHit hit;
            if (Physics.SphereCast(origin, 6f, t.forward, out hit, 40f, mask, QueryTriggerInteraction.Ignore)
                && hit.collider != null
                && !hit.collider.transform.IsChildOf(t))
                return true;
            if (Physics.Raycast(origin, t.up, out hit, 25f, mask, QueryTriggerInteraction.Ignore)
                && hit.collider != null
                && !hit.collider.transform.IsChildOf(t))
                return true;
            return false;
        }

        private static void EnsureFollow(CameraStateManager mgr)
        {
            if (mgr == null || Ac == null)
                return;
            try
            {
                if (object.ReferenceEquals(mgr.followingUnit, Ac))
                    return;
                mgr.SetFollowingUnit(Ac);
            }
            catch { }
        }

        private static bool InOrbit()
        {
            try
            {
                CameraStateManager mgr = SceneSingleton<CameraStateManager>.i;
                return mgr != null && mgr.currentState == mgr.orbitState;
            }
            catch
            {
                return false;
            }
        }

        private static string TaxiHint()
        {
            Pilot p = Crew();
            PilotBaseState cur = p != null ? p.currentState : null;
            if (cur is AIPilotTakeoffState)
                return "TAKEOFF";
            if (cur is AIPilotTaxiState)
                return "TAXI";
            return "TAXI";
        }

        private static AircraftDefinition FindDef()
        {
            _def = Picked;
            return _def;
        }

        private static Vector3 Front()
        {
            if (Time.unscaledTime < _frontAt && _front.sqrMagnitude > 1f)
                return _front;
            _frontAt = Time.unscaledTime + 1.2f;
            Vector3 pos = Ac != null ? Ac.transform.position : _home;
            _front = ClosestFront(Ac, pos);
            return _front;
        }

        private static Vector3 ClosestFront(Aircraft ac, Vector3 pos)
        {
            try
            {
                GlobalPosition gp;
                if (ac != null && MissionPosition.TryGetClosestPosition(ac, out gp))
                {
                    Vector3 m = gp.ToLocalPosition();
                    if ((m - pos).sqrMagnitude > 200f * 200f)
                        return m;
                }
                FactionHQ hq = null;
                try { if (ac != null) hq = ac.NetworkHQ; }
                catch { }
                if (hq == null)
                    hq = GaPerfFrameGate.Hq();
                if (hq != null && MissionPosition.TryGetClosestPosition(hq, pos.ToGlobalPosition(), out gp))
                {
                    Vector3 m = gp.ToLocalPosition();
                    if ((m - pos).sqrMagnitude > 200f * 200f)
                        return m;
                }
            }
            catch { }
            FactionHQ local = null;
            try { if (ac != null) local = ac.NetworkHQ; }
            catch { }
            if (local == null)
                local = GaPerfFrameGate.Hq();
            Vector3 fwd = Vector3.forward;
            if (ac != null)
            {
                fwd = ac.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f)
                    fwd = Vector3.forward;
                else
                    fwd.Normalize();
            }
            else if (_runwayFwd.sqrMagnitude > 0.01f)
                fwd = _runwayFwd;
            Airbase[] abs = GaPerfFrameGate.Airbases();
            Vector3 best = pos + fwd * 8000f;
            float bestD = 1e18f;
            if (abs == null)
                return best;
            for (int i = 0; i < abs.Length; i++)
            {
                Airbase ab = abs[i];
                if (ab == null || ab.disabled)
                    continue;
                try
                {
                    if (local != null && ab.CurrentHQ != null
                        && object.ReferenceEquals(ab.CurrentHQ, local))
                        continue;
                }
                catch { continue; }
                Vector3 p;
                try { p = ab.center != null ? ab.center.position : ab.transform.position; }
                catch { continue; }
                float d = (p - pos).sqrMagnitude;
                if (d < 400f * 400f)
                    continue;
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }
            return best;
        }
    }
}
