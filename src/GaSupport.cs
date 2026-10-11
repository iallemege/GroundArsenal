using System.Collections.Generic;
using NuclearOption.SavedMission;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>V-9 gunship (VL-49) and V-0 CAS on the Q mark.</summary>
    internal static class GaSupport
    {
        private const string HqCall = "HQ";
        private const string GunCall = "蜻蜓一号";
        private const string CasCall = "龙火一号";

        private static readonly Bird Gun = new Bird(true);
        private static readonly Bird Cas = new Bird(false);
        private static readonly List<WeaponMount> Opt = new List<WeaponMount>(16);
        private static readonly List<AircraftDefinition> Pool = new List<AircraftDefinition>(24);
        private static float _hqAt;
        private static string _hqLine;
        private static float _inAt;
        private static string _inCall;
        private static string _inLine;

        private sealed class Bird
        {
            internal readonly bool Gunship;
            internal Aircraft Ac;
            internal Vector3 Mark;
            internal bool Warped;
            internal float WaitUntil;
            internal float CoolAt;
            internal float DoneAt;
            internal float NextFire;
            internal float Orbit;
            internal readonly List<int> Known = new List<int>(16);

            internal Bird(bool gunship)
            {
                Gunship = gunship;
            }
        }

        internal static bool IsMine(Aircraft ac)
        {
            if (ac == null)
                return false;
            return object.ReferenceEquals(ac, Gun.Ac) || object.ReferenceEquals(ac, Cas.Ac);
        }

        internal static void Clear()
        {
            Home(Gun);
            Home(Cas);
            Gun.WaitUntil = 0f;
            Cas.WaitUntil = 0f;
            _hqAt = 0f;
            _inAt = 0f;
        }

        internal static void Tick()
        {
            TickRadio();
            Fly(Gun);
            Fly(Cas);
            if (!GaMode.Active)
                return;
            if (GaMode.Downed || GaMode.Dead)
                return;
        }

        internal static string Hint()
        {
            if (Gun.Ac != null && Gun.Warped)
                return "GUNSHIP  inbound";
            if (Cas.Ac != null && Cas.Warped)
                return "CAS  inbound";
            return null;
        }

        internal static bool RequestGunship()
        {
            return Request(Gun);
        }

        internal static bool RequestCas()
        {
            return Request(Cas);
        }

        internal static bool SteerAim(Aircraft ac, out Vector3 aim, out float hold, out float bank)
        {
            aim = Vector3.zero;
            hold = 300f;
            bank = 12f;
            if (ac == null)
                return false;
            if (object.ReferenceEquals(ac, Gun.Ac) && Gun.Warped)
                return AimGun(Gun, ac, out aim, out hold, out bank);
            if (object.ReferenceEquals(ac, Cas.Ac) && Cas.Warped)
                return AimCas(Cas, ac, out aim, out hold, out bank);
            return false;
        }

        private static bool Request(Bird b)
        {
            Vector3 mark;
            if (!GaFps.TryGetMark(out mark))
            {
                GaMode.Deny("MARK WITH Q", "先用 Q 标记");
                GaRadio.Say(GaRadio.PlayerCall(), "Negative — no mark. Q a target first.", false, 10);
                return false;
            }
            if (Time.unscaledTime < b.CoolAt)
            {
                GaMode.Deny("SUPPORT WAIT", "支援冷却中");
                return false;
            }
            if (b.Ac != null)
            {
                GaMode.Deny("ALREADY INBOUND", "支援已在途中");
                return false;
            }
            Airbase ab = GaHangar.FieldAirbase();
            AircraftDefinition def = b.Gunship ? GaChute.Def(DropKind.Vl49) : PickCas(ab);
            if (def == null || ab == null)
            {
                GaMode.Deny("NO ASSET", "没有可用支援飞机");
                return false;
            }
            Loadout load = b.Gunship ? null : AgLoadout(def, ab);
            Snapshot(b);
            try
            {
                if (!ab.CanSpawnAircraft(def))
                {
                    GaMode.Deny("HANGER BUSY", "机库繁忙");
                    return false;
                }
                Airbase.TrySpawnResult result = ab.TrySpawnAircraft(
                    null, def, new LiveryKey(0), load, 1f);
                if (!result.Allowed)
                {
                    GaMode.Deny("CAN'T LAUNCH", "无法起飞支援");
                    return false;
                }
            }
            catch
            {
                GaMode.Deny("CAN'T LAUNCH", "无法起飞支援");
                return false;
            }
            b.Mark = mark;
            b.Warped = false;
            b.Ac = null;
            b.WaitUntil = Time.unscaledTime + 14f;
            b.CoolAt = Time.unscaledTime + 55f;
            b.DoneAt = 0f;
            b.Orbit = 0f;
            GaMode.Deny(b.Gunship ? "GUNSHIP CALLED" : "CAS CALLED",
                b.Gunship ? "已呼叫炮艇" : "已呼叫近距空援");
            _hqLine = "Support request received. Assets inbound.";
            _hqAt = Time.unscaledTime + 1.35f;
            return true;
        }

        private static void TickRadio()
        {
            float now = Time.unscaledTime;
            if (_hqAt > 0.01f && now >= _hqAt)
            {
                _hqAt = 0f;
                GaRadio.Say(HqCall, _hqLine, false, 10);
            }
            if (_inAt > 0.01f && now >= _inAt)
            {
                _inAt = 0f;
                GaRadio.Say(_inCall, _inLine, false, 10);
            }
        }

        private static void Fly(Bird b)
        {
            if (b.Ac == null && b.WaitUntil > 0.01f)
            {
                if (Time.unscaledTime < b.WaitUntil)
                    Grab(b);
                else
                {
                    b.WaitUntil = 0f;
                    GaMode.Deny("NO SUPPORT AIRCRAFT", "支援机未能起飞");
                }
            }
            if (b.Ac == null)
                return;
            try
            {
                if (b.Ac.disabled)
                {
                    b.Ac = null;
                    return;
                }
            }
            catch
            {
                b.Ac = null;
                return;
            }
            if (!b.Warped)
                Warp(b);
            Kick(b.Ac);
            Vector3 live;
            if (GaFps.TryGetMark(out live))
                b.Mark = live;
            if (b.Warped && b.DoneAt > 0.01f && Time.unscaledTime >= b.DoneAt)
            {
                Home(b);
                return;
            }
            if (b.Warped)
                Shoot(b);
        }

        private static void Grab(Bird b)
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
                if (object.ReferenceEquals(ac, GaAirLift.Ac) || GaSupply.IsMine(ac) || IsMine(ac))
                    continue;
                int id = ac.GetInstanceID();
                if (b.Known.Contains(id))
                    continue;
                DropKind kind = GaChute.KindOf(ac);
                if (b.Gunship)
                {
                    if (kind != DropKind.Vl49)
                        continue;
                }
                else if (kind != DropKind.None)
                    continue;
                try
                {
                    if (hq != null && ac.NetworkHQ != null
                        && !object.ReferenceEquals(ac.NetworkHQ, hq))
                        continue;
                }
                catch { continue; }
                b.Ac = ac;
                b.WaitUntil = 0f;
                return;
            }
        }

        private static void Snapshot(Bird b)
        {
            b.Known.Clear();
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return;
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i] as Aircraft;
                if (ac == null)
                    continue;
                b.Known.Add(ac.GetInstanceID());
            }
        }

        private static void Warp(Bird b)
        {
            if (b.Ac == null)
                return;
            Vector3 dir = Vector3.forward;
            Transform pawn = GaMode.PawnRoot;
            Vector3 from = b.Ac.transform.position;
            Airbase ab = GaHangar.FieldAirbase();
            if (ab != null)
            {
                try { from = ab.center != null ? ab.center.position : ab.transform.position; }
                catch { }
            }
            dir = b.Mark - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f && pawn != null)
            {
                dir = b.Mark - pawn.position;
                dir.y = 0f;
            }
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();
            float back = b.Gunship ? 780f : 2400f;
            float up = b.Gunship ? 260f : 920f;
            float spd = b.Gunship ? 42f : 165f;
            Vector3 start = b.Mark - dir * back + Vector3.up * up;
            try
            {
                b.Ac.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir, Vector3.up));
                if (b.Ac.rb != null)
                {
                    b.Ac.rb.velocity = dir * spd;
                    b.Ac.rb.angularVelocity = Vector3.zero;
                }
                b.Ac.SetGear(false);
                b.Ac.SetFlightAssist(true);
            }
            catch { }
            b.Warped = true;
            b.DoneAt = Time.unscaledTime + (b.Gunship ? 48f : 38f);
            _inCall = b.Gunship ? GunCall : CasCall;
            _inLine = b.Gunship
                ? "Dragonfly One inbound. Starting gunship support."
                : "Dragonfire One inbound. Starting the CAS run.";
            _inAt = Time.unscaledTime + 0.45f;
        }

        private static bool AimGun(Bird b, Aircraft ac, out Vector3 aim, out float hold, out float bank)
        {
            Vector3 pos = ac.transform.position;
            b.Orbit += Time.deltaTime * 0.42f;
            Vector3 side = new Vector3(Mathf.Cos(b.Orbit), 0f, Mathf.Sin(b.Orbit));
            Vector3 dest = b.Mark + side * 210f;
            Vector3 dir = dest - pos;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1f)
                dir = ac.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();
            float agl = 240f;
            try { agl = ac.radarAlt; }
            catch { }
            aim = pos + dir * 420f;
            aim.y = pos.y;
            hold = Mathf.Max(agl, 240f);
            bank = 12f;
            return true;
        }

        private static bool AimCas(Bird b, Aircraft ac, out Vector3 aim, out float hold, out float bank)
        {
            Vector3 pos = ac.transform.position;
            Vector3 dir = b.Mark - pos;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist < 0.5f)
                dir = ac.transform.forward;
            else
                dir /= dist;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();
            float agl = 900f;
            try { agl = ac.radarAlt; }
            catch { }
            float ahead = dist > 380f ? 2600f : 2200f;
            aim = pos + dir * ahead;
            if (dist < 380f)
                aim = b.Mark + dir * 1800f;
            aim.y = pos.y;
            hold = Mathf.Max(agl, 880f);
            if (hold > agl + 40f)
                hold = agl + 40f;
            bank = 8f;
            return true;
        }

        private static void Kick(Aircraft ac)
        {
            if (ac == null)
                return;
            try
            {
                Pilot[] pilots = ac.pilots;
                if (pilots == null || pilots.Length == 0)
                    return;
                Pilot p = pilots[0];
                if (p == null)
                    return;
                if (p.currentState is AIPilotLandingState)
                {
                    if (p.AICombatState == null)
                        p.AICombatState = new AIPilotCombatModes(ac);
                    if (p.AICombatState != null)
                        p.SwitchState(p.AICombatState);
                }
            }
            catch { }
        }

        private static void Home(Bird b)
        {
            Aircraft ac = b.Ac;
            b.Ac = null;
            b.Warped = false;
            if (ac == null)
                return;
            try
            {
                Pilot[] pilots = ac.pilots;
                Pilot p = (pilots != null && pilots.Length > 0) ? pilots[0] : null;
                if (p == null)
                    return;
                if (p.AILandingState == null)
                    p.AILandingState = new AIPilotLandingState();
                p.SwitchState(p.AILandingState);
            }
            catch { }
        }

        private static void Shoot(Bird b)
        {
            if (b.Ac == null || Time.time < b.NextFire)
                return;
            Vector3 pos = b.Ac.transform.position;
            Vector3 flat = b.Mark - pos;
            flat.y = 0f;
            float reach = b.Gunship ? 420f : 2100f;
            if (flat.sqrMagnitude > reach * reach)
                return;
            Unit tgt = NearestHostile(b.Mark, b.Gunship ? 220f : 380f);
            List<WeaponStation> stations = null;
            try { stations = b.Ac.weaponStations; }
            catch { }
            if (stations == null || stations.Count == 0)
                return;
            try
            {
                if (tgt != null && b.Ac.weaponManager != null)
                    b.Ac.weaponManager.AddTargetList(tgt);
            }
            catch { }
            GlobalPosition aim = b.Mark.ToGlobalPosition();
            bool fired = false;
            for (int i = 0; i < stations.Count; i++)
            {
                WeaponStation st = stations[i];
                if (st == null)
                    continue;
                WeaponInfo info = null;
                try { info = st.WeaponInfo; }
                catch { }
                if (info != null && (info.cargo || info.troops || info.sling))
                    continue;
                try { st.SetTurretVector(b.Mark); }
                catch { }
                if (tgt != null)
                {
                    try { st.SetStationTargets(new PersistentID[] { tgt.persistentID }); }
                    catch { }
                }
                bool missile = info != null && (info.missile || info.bomb || info.glideBomb);
                try
                {
                    if (missile)
                        st.LaunchMount(b.Ac, tgt, aim);
                    else if (tgt != null)
                        st.Fire(b.Ac, tgt);
                    else
                        st.RemoteFireSingle(b.Ac);
                    fired = true;
                }
                catch { }
                if (missile && fired)
                    break;
            }
            b.NextFire = Time.time + (b.Gunship ? 0.22f : 0.85f);
        }

        private static Unit NearestHostile(Vector3 mark, float radius)
        {
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return null;
            FactionHQ hq = GaPerfFrameGate.Hq();
            float r2 = radius * radius;
            Unit best = null;
            float bestD = r2;
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i];
                if (u == null || u.disabled)
                    continue;
                if (u is PilotDismounted)
                    continue;
                if (IsMine(u as Aircraft) || GaSupply.IsMine(u as Aircraft))
                    continue;
                if (object.ReferenceEquals(u, GaAirLift.Ac))
                    continue;
                try
                {
                    if (hq != null && u.NetworkHQ != null
                        && object.ReferenceEquals(u.NetworkHQ, hq))
                        continue;
                }
                catch { continue; }
                Vector3 p;
                try { p = u.GetCenterOfMass(); }
                catch { p = u.transform.position; }
                float d = (p - mark).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = u;
                }
            }
            return best;
        }

        private static AircraftDefinition PickCas(Airbase ab)
        {
            Pool.Clear();
            try
            {
                Encyclopedia enc = Encyclopedia.i;
                if (enc == null || enc.aircraft == null || ab == null)
                    return null;
                foreach (AircraftDefinition d in enc.aircraft)
                {
                    if (d == null)
                        continue;
                    try
                    {
                        if (d.NotAllowed(true))
                            continue;
                    }
                    catch { }
                    string blob = (d.jsonKey ?? "") + " " + (d.unitName ?? "") + " " + (d.code ?? "");
                    DropKind kind = GaChute.KindFromName(blob);
                    if (kind != DropKind.None)
                        continue;
                    string l = blob.ToLowerInvariant();
                    if (l.IndexOf("helo") >= 0 || l.IndexOf("ibis") >= 0 || l.IndexOf("uh-90") >= 0)
                        continue;
                    try
                    {
                        if (!ab.CanSpawnAircraft(d))
                            continue;
                    }
                    catch { continue; }
                    float ag = 0f;
                    try { ag = d.roleIdentity.antiSurface; }
                    catch { }
                    if (ag < 0.12f && !HasAgOptions(d))
                        continue;
                    Pool.Add(d);
                }
            }
            catch { }
            if (Pool.Count == 0)
                return null;
            return Pool[UnityEngine.Random.Range(0, Pool.Count)];
        }

        private static bool HasAgOptions(AircraftDefinition d)
        {
            if (d == null || d.unitPrefab == null)
                return false;
            WeaponManager wm = d.unitPrefab.GetComponentInChildren<WeaponManager>(true);
            if (wm == null || wm.hardpointSets == null)
                return false;
            for (int i = 0; i < wm.hardpointSets.Length; i++)
            {
                HardpointSet set = wm.hardpointSets[i];
                if (set == null || set.weaponOptions == null)
                    continue;
                for (int w = 0; w < set.weaponOptions.Count; w++)
                {
                    if (AgScore(set.weaponOptions[w]) > 1f)
                        return true;
                }
            }
            return false;
        }

        private static Loadout AgLoadout(AircraftDefinition def, Airbase ab)
        {
            Loadout lo = new Loadout();
            if (lo.weapons == null)
                lo.weapons = new List<WeaponMount>(12);
            if (def == null || def.unitPrefab == null)
                return null;
            WeaponManager wm = def.unitPrefab.GetComponentInChildren<WeaponManager>(true);
            if (wm == null || wm.hardpointSets == null)
                return null;
            FactionHQ hq = null;
            try { hq = ab != null ? ab.CurrentHQ : GaPerfFrameGate.Hq(); }
            catch { }
            bool any = false;
            for (int i = 0; i < wm.hardpointSets.Length; i++)
            {
                HardpointSet set = wm.hardpointSets[i];
                WeaponMount pick = null;
                float best = -1f;
                try
                {
                    WeaponChecker.GetAvailableWeaponsNonAlloc(null, set, ab, hq, false, Opt);
                    for (int w = 0; w < Opt.Count; w++)
                    {
                        float s = AgScore(Opt[w]);
                        if (s > best)
                        {
                            best = s;
                            pick = Opt[w];
                        }
                    }
                }
                catch
                {
                    if (set != null && set.weaponOptions != null)
                    {
                        for (int w = 0; w < set.weaponOptions.Count; w++)
                        {
                            float s = AgScore(set.weaponOptions[w]);
                            if (s > best)
                            {
                                best = s;
                                pick = set.weaponOptions[w];
                            }
                        }
                    }
                }
                lo.weapons.Add(pick);
                if (pick != null && best > 0.5f)
                    any = true;
            }
            if (!any)
                return null;
            return lo;
        }

        private static float AgScore(WeaponMount m)
        {
            if (m == null)
                return -1f;
            if (m.Cargo || m.Troops || m.countermeasure || m.radar || m.tailHook || m.slingloadHook)
                return -1f;
            WeaponInfo info = m.info;
            if (info == null)
                return 0f;
            if (info.cargo || info.troops || info.sling || info.jammer)
                return -1f;
            if (info.nuclear)
                return 0.05f;
            float ag = 0f;
            try { ag = info.effectiveness.antiSurface; }
            catch { }
            float s = ag * 12f;
            if (info.bomb) s += 5f;
            if (info.glideBomb) s += 4.5f;
            if (info.missile && ag > 0.2f) s += 4f;
            if (info.gun && ag > 0.15f) s += 2f;
            s += info.blastDamage * 0.012f;
            try { s += info.effectiveness.antiRadar * 1.5f; }
            catch { }
            return s;
        }
    }
}
