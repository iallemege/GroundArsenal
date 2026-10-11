using System.Collections.Generic;
using NuclearOption.SavedMission;
using UnityEngine;
using UnityEngine.Rendering;

namespace GroundArsenal
{
    /// <summary>U: call a cargo aircraft to parachute an ammo crate on the player.</summary>
    internal static class GaSupply
    {
        private static Aircraft _ac;
        private static Vector3 _dropAt;
        private static bool _dropped;
        private static bool _warped;
        private static float _coolAt;
        private static float _waitUntil;
        private static float _goneAt;
        private static GameObject _crate;
        private static GameObject _chute;
        private static Rigidbody _crateRb;
        private static bool _landed;
        private static Material _boxMat;
        private static readonly List<int> _known = new List<int>(16);

        internal static bool IsMine(Aircraft ac)
        {
            return ac != null && object.ReferenceEquals(ac, _ac);
        }

        internal static void Clear()
        {
            _ac = null;
            _dropped = false;
            _warped = false;
            _waitUntil = 0f;
            KillCrate();
        }

        internal static void Tick()
        {
            if (!GaMode.Active)
            {
                FlyBird();
                return;
            }
            if (!GaHud.BlocksLook && !GaRadio.Open && !GaMode.Downed && !GaMode.Dead
                && Input.GetKeyDown(KeyCode.U))
                Request();
            FlyBird();
            TickCrate();
            TickReArm();
        }

        internal static string Hint()
        {
            if (_crate != null && !_landed)
                return "SUPPLY  inbound";
            if (NearCrate())
                return "F  REARM  crate";
            if (_ac != null && !_dropped)
                return GaChute.LiftName() + "  supply run";
            if (Time.unscaledTime < _coolAt)
            {
                int s = Mathf.CeilToInt(_coolAt - Time.unscaledTime);
                return "SUPPLY  wait " + s.ToString() + "s";
            }
            return null;
        }

        internal static bool SteerAim(Aircraft ac, out Vector3 aim, out float hold, out float bank)
        {
            aim = Vector3.zero;
            hold = 420f;
            bank = 10f;
            if (!IsMine(ac) || _dropped)
                return false;
            Vector3 pos = ac.transform.position;
            Vector3 dir = _dropAt - pos;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1f)
                dir = ac.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();
            float agl = 400f;
            try { agl = ac.radarAlt; }
            catch { }
            aim = pos + dir * 2200f;
            aim.y = pos.y;
            hold = Mathf.Max(agl, 400f);
            bank = 8f;
            return true;
        }

        private static void Request()
        {
            if (Time.unscaledTime < _coolAt)
            {
                GaMode.Deny("SUPPLY WAIT", "补给冷却中");
                return;
            }
            if (_ac != null && !_dropped)
            {
                GaMode.Deny("SUPPLY INBOUND", "补给机已在途中");
                return;
            }
            Transform pawn = GaMode.PawnRoot;
            if (pawn == null)
                return;
            AircraftDefinition def = GaChute.LiftDef();
            Airbase ab = GaHangar.FieldAirbase();
            if (def == null || ab == null)
            {
                GaMode.Deny("NO AIRLIFT", "没有可用运输机");
                return;
            }
            SnapshotBirds();
            try
            {
                if (!ab.CanSpawnAircraft(def))
                {
                    GaMode.Deny("HANGER BUSY", "机库繁忙");
                    return;
                }
                Airbase.TrySpawnResult result = ab.TrySpawnAircraft(
                    null, def, new LiveryKey(0), new Loadout(), 1f);
                if (!result.Allowed)
                {
                    GaMode.Deny("CAN'T LAUNCH", "无法起飞补给机");
                    return;
                }
            }
            catch
            {
                GaMode.Deny("CAN'T LAUNCH", "无法起飞补给机");
                return;
            }
            _dropAt = pawn.position;
            _dropped = false;
            _warped = false;
            _ac = null;
            _waitUntil = Time.unscaledTime + 12f;
            _coolAt = Time.unscaledTime + 48f;
            _goneAt = 0f;
            GaMode.Deny("SUPPLY CALLED", "已呼叫空投补给");
        }

        private static void FlyBird()
        {
            if (_ac == null && _waitUntil > 0.01f)
            {
                if (Time.unscaledTime < _waitUntil)
                    GrabBird();
                else
                {
                    _waitUntil = 0f;
                    GaMode.Deny("NO SUPPLY AIRCRAFT", "补给机未能起飞");
                }
            }
            if (_ac == null)
                return;
            try
            {
                if (_ac.disabled)
                {
                    _ac = null;
                    return;
                }
            }
            catch
            {
                _ac = null;
                return;
            }
            if (!_warped)
                WarpBird();
            Kick();
            Transform pawn = GaMode.PawnRoot;
            if (pawn != null && !_dropped)
                _dropAt = pawn.position;
            Vector3 pos = _ac.transform.position;
            Vector3 flat = _dropAt - pos;
            flat.y = 0f;
            if (!_dropped && flat.sqrMagnitude < 150f * 150f)
            {
                DropCrate(pos);
                _dropped = true;
                _goneAt = Time.unscaledTime + 10f;
            }
            if (_dropped && Time.unscaledTime > _goneAt)
                _ac = null;
        }

        private static void GrabBird()
        {
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return;
            FactionHQ hq = GaPerfFrameGate.Hq();
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i] as Aircraft;
                if (ac == null || ac.disabled)
                    continue;
                if (!GaChute.IsDropship(ac))
                    continue;
                if (object.ReferenceEquals(ac, GaAirLift.Ac))
                    continue;
                int id = ac.GetInstanceID();
                if (_known.Contains(id))
                    continue;
                try
                {
                    if (hq != null && ac.NetworkHQ != null
                        && !object.ReferenceEquals(ac.NetworkHQ, hq))
                        continue;
                }
                catch { continue; }
                _ac = ac;
                _waitUntil = 0f;
                return;
            }
        }

        private static void SnapshotBirds()
        {
            _known.Clear();
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return;
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i] as Aircraft;
                if (ac == null)
                    continue;
                if (!GaChute.IsDropship(ac))
                    continue;
                _known.Add(ac.GetInstanceID());
            }
        }

        private static void WarpBird()
        {
            if (_ac == null)
                return;
            Vector3 dir = Vector3.forward;
            Transform pawn = GaMode.PawnRoot;
            if (pawn != null)
                _dropAt = pawn.position;
            Airbase ab = GaHangar.FieldAirbase();
            if (ab != null)
            {
                Vector3 from;
                try { from = ab.center != null ? ab.center.position : ab.transform.position; }
                catch { from = _ac.transform.position; }
                dir = _dropAt - from;
                dir.y = 0f;
            }
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();
            Vector3 start = _dropAt - dir * 1800f + Vector3.up * 520f;
            try
            {
                _ac.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir, Vector3.up));
                if (_ac.rb != null)
                {
                    _ac.rb.velocity = dir * 78f;
                    _ac.rb.angularVelocity = Vector3.zero;
                }
                _ac.SetGear(false);
                _ac.SetFlightAssist(true);
            }
            catch { }
            _warped = true;
        }

        private static void Kick()
        {
            if (_ac == null)
                return;
            try
            {
                Pilot[] pilots = _ac.pilots;
                if (pilots == null || pilots.Length == 0)
                    return;
                Pilot p = pilots[0];
                if (p == null)
                    return;
                if (p.currentState is AIPilotLandingState)
                {
                    if (p.AICombatState == null)
                        p.AICombatState = new AIPilotCombatModes(_ac);
                    if (p.AICombatState != null)
                        p.SwitchState(p.AICombatState);
                }
            }
            catch { }
        }

        private static void DropCrate(Vector3 from)
        {
            KillCrate();
            Vector3 pos = from;
            try { pos = GaChute.ExitPoint(_ac, 0); }
            catch { pos = from - Vector3.up * 6f; }
            pos += Vector3.down * 5f;
            _crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _crate.name = "GaSupplyCrate";
            _crate.transform.position = pos;
            _crate.transform.rotation = Quaternion.identity;
            _crate.transform.localScale = new Vector3(1.7f, 1.15f, 1.7f);
            MeshRenderer r = _crate.GetComponent<MeshRenderer>();
            if (r != null)
            {
                r.sharedMaterial = BoxMat();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            BoxCollider box = _crate.GetComponent<BoxCollider>();
            if (box != null)
            {
                try { box.excludeLayers = 0; }
                catch { }
            }
            _crateRb = _crate.AddComponent<Rigidbody>();
            _crateRb.mass = 90f;
            _crateRb.drag = 0.4f;
            _crateRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            try
            {
                if (_ac != null && _ac.rb != null)
                    _crateRb.velocity = _ac.rb.velocity + Vector3.down * 4f;
                else
                    _crateRb.velocity = Vector3.down * 8f;
            }
            catch { _crateRb.velocity = Vector3.down * 8f; }
            _chute = GaChute.MakeCanopy(_crate.transform);
            if (_chute != null)
            {
                _chute.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                _chute.transform.localScale = Vector3.one * 1.35f;
            }
            _landed = false;
        }

        private static void TickCrate()
        {
            if (_crate == null)
                return;
            if (_landed)
                return;
            Vector3 p = _crate.transform.position;
            RaycastHit hit;
            bool ground = false;
            if (GaPhys.RayWalk(p + Vector3.up * 0.4f, 3.2f, out hit))
            {
                if (hit.distance < 1.45f)
                    ground = true;
                else if (_crateRb != null && _crateRb.velocity.y > -1.4f && hit.distance < 1.8f)
                    ground = true;
            }
            if (!ground)
            {
                if (_crateRb != null && _crateRb.velocity.y < -16f)
                    _crateRb.velocity = new Vector3(_crateRb.velocity.x, -16f, _crateRb.velocity.z);
                return;
            }
            _landed = true;
            if (_crateRb != null)
            {
                _crateRb.velocity = Vector3.zero;
                _crateRb.angularVelocity = Vector3.zero;
                _crateRb.isKinematic = true;
            }
            if (_chute != null)
            {
                UnityEngine.Object.Destroy(_chute);
                _chute = null;
            }
        }

        private static void TickReArm()
        {
            if (!NearCrate())
            {
                if (GaHud.ArsenalOpen)
                    return;
                return;
            }
            if (GaHud.ArsenalOpen)
            {
                if (Input.GetKeyDown(KeyCode.F))
                {
                    GaHud.ArsenalOpen = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                return;
            }
            if (GaHud.BlocksLook || GaRadio.Open || GaMode.Downed)
                return;
            if (!Input.GetKeyDown(KeyCode.F))
                return;
            GaCombat.ResupplyFromPack();
            GaHud.ArsenalOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GaMode.Deny("REARM", "更换武装");
        }

        private static bool NearCrate()
        {
            if (_crate == null || !_landed)
                return false;
            Transform pawn = GaMode.PawnRoot;
            if (pawn == null)
                return false;
            return (pawn.position - _crate.transform.position).sqrMagnitude < 4.2f * 4.2f;
        }

        private static Material BoxMat()
        {
            if (_boxMat != null)
                return _boxMat;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null)
                sh = Shader.Find("Unlit/Color");
            if (sh == null)
                sh = Shader.Find("Sprites/Default");
            if (sh == null)
                sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null)
                return null;
            _boxMat = new Material(sh);
            _boxMat.name = "GaSupplyBox";
            Color c = new Color(0.42f, 0.38f, 0.22f, 1f);
            _boxMat.color = c;
            if (_boxMat.HasProperty("_BaseColor"))
                _boxMat.SetColor("_BaseColor", c);
            if (_boxMat.HasProperty("_Color"))
                _boxMat.SetColor("_Color", c);
            return _boxMat;
        }

        private static void KillCrate()
        {
            if (_chute != null)
                UnityEngine.Object.Destroy(_chute);
            _chute = null;
            if (_crate != null)
                UnityEngine.Object.Destroy(_crate);
            _crate = null;
            _crateRb = null;
            _landed = false;
            if (GaHud.ArsenalOpen)
            {
                GaHud.ArsenalOpen = false;
                try
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                catch { }
            }
        }
    }
}
