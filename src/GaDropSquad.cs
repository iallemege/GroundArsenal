using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NuclearOption.SavedMission;
using UnityEngine;

namespace GroundArsenal
{
    internal static class GaDropSquad
    {
        internal const int FriendCount = 3;
        internal const int EnemyPerShip = 3;
        internal const int EnemyCap = 12;
        internal static bool HoldPos;
        internal static Vector3 HoldAt;
        internal static int FriendsAlive;
        internal static int HostilesAlive;
        internal static readonly List<GaDropBot> Hostiles = new List<GaDropBot>(16);

        private static readonly List<GaDropBot> Bots = new List<GaDropBot>(16);
        private static readonly Dictionary<int, float> Dropped = new Dictionary<int, float>(16);
        private static float _nextWatch;
        private static int _kitClock;
        private static float _nextLift;

        internal static void PlayerJumped(Aircraft ac, Vector3 inherit)
        {
            MarkDropped(ac);
            SpawnFrom(ac, inherit, true, FriendCount);
            TryOpposingDrop();
        }

        internal static void PlayerDeployed(Vector3 at, float yaw)
        {
            if (Count(true) >= FriendCount)
                return;
            int n = FriendCount - Count(true);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < n; i++)
            {
                Vector3 off = rot * new Vector3(((i % 3) - 1) * 2.4f, 0f, 2.2f + (i / 3) * 1.8f);
                Vector3 pos = at + off;
                RaycastHit hit;
                if (GaPhys.RayDown(pos + Vector3.up * 4f, 12f, out hit))
                    pos = hit.point + Vector3.up * 0.08f;
                GameObject go = new GameObject("GaDropFriend");
                go.transform.position = pos;
                go.transform.rotation = rot;
                GaDropBot bot = go.AddComponent<GaDropBot>();
                bot.Boot(true, Vector3.zero, i, false, NextKit());
                Bots.Add(bot);
            }
            FillEnemyGround();
        }

        internal static void HealFriendlies(Vector3 at, float radius, float amt)
        {
            float r2 = radius * radius;
            for (int i = 0; i < Bots.Count; i++)
            {
                GaDropBot b = Bots[i];
                if (b == null || !b.Friendly || b.Hp <= 0f)
                    continue;
                if ((b.transform.position - at).sqrMagnitude > r2)
                    continue;
                b.Hp += amt;
                if (b.Hp > 80f)
                    b.Hp = 80f;
            }
        }

        [SkipLocalsInit]
        internal static void Tick()
        {
            if (GaMode.IsMenuScene())
            {
                ClearAll();
                return;
            }
            if (!SimulatesWorld())
            {
                TickVisualOnly();
                return;
            }
            GaAirLift.TickHostiles();
            float dt = Time.deltaTime;
            Vector3 origin = GaMode.Active ? GaMode.EyePos : Vector3.zero;
            int frame = Time.frameCount;
            bool hitch = GaPerfFrameGate.SkipPolish();
            int friends = 0;
            int foes = 0;
            for (int i = Bots.Count - 1; i >= 0; i--)
            {
                GaDropBot b = Bots[i];
                if (b == null)
                {
                    Bots.RemoveAt(i);
                    continue;
                }
                if (b.Hp > 0f && !b.Downed)
                {
                    if (b.Friendly)
                        friends++;
                    else
                        foes++;
                }
                int lod = 0;
                if (!b.Falling && origin.sqrMagnitude > 0.01f)
                {
                    float d2 = (b.transform.position - origin).sqrMagnitude;
                    if (d2 > 25600f)
                        lod = 2;
                    else                     if (d2 > 6400f)
                        lod = 1;
                }
                if (!b.Friendly)
                {
                    if (hitch && lod > 0 && ((i + frame) & 1) != 0)
                        continue;
                    if (lod >= 2 && ((i + frame) & 1) != 0)
                        continue;
                }
                b.Tick(dt, lod);
            }
            FriendsAlive = friends;
            HostilesAlive = foes;
            Hostiles.Clear();
            for (int i = 0; i < Bots.Count; i++)
            {
                GaDropBot b = Bots[i];
                if (b != null && !b.Friendly && b.Hp > 0f && !b.Downed)
                    Hostiles.Add(b);
            }
            if (Time.unscaledTime < _nextWatch || hitch)
                return;
            _nextWatch = Time.unscaledTime + 0.55f;
            WatchHostiles();
            TrySpawnEnemyLift();
            FillEnemyGround();
        }

        internal static void InviteTo(Unit host)
        {
            if (host == null)
                return;
            for (int i = 0; i < Bots.Count; i++)
            {
                GaDropBot b = Bots[i];
                if (b == null || !b.Friendly || b.Downed || b.Hp <= 0f || b.Falling)
                    continue;
                GaRide.TryBoardBot(b);
            }
        }

        internal static void DespawnFriendlies()
        {
            for (int i = Bots.Count - 1; i >= 0; i--)
            {
                GaDropBot b = Bots[i];
                if (b == null || !b.Friendly)
                    continue;
                UnityEngine.Object.Destroy(b.gameObject);
                Bots.RemoveAt(i);
            }
        }

        internal static void ClearAll()
        {
            for (int i = 0; i < Bots.Count; i++)
            {
                if (Bots[i] != null)
                    UnityEngine.Object.Destroy(Bots[i].gameObject);
            }
            Bots.Clear();
            Dropped.Clear();
            HoldPos = false;
            Hostiles.Clear();
            _nextLift = 0f;
            FriendsAlive = 0;
            HostilesAlive = 0;
        }

        internal static GaDropBot NearestHostile(Vector3 from)
        {
            return Nearest(from, false);
        }

        internal static GaDropBot NearestFriendly(Vector3 from)
        {
            return Nearest(from, true);
        }

        internal static void OrderHold()
        {
            HoldPos = true;
            Transform root = GaMode.PawnRoot;
            HoldAt = root != null ? root.position : GaMode.EyePos;
        }

        internal static void OrderFollow()
        {
            HoldPos = false;
        }

        internal static GaDropBot NearestKit(Vector3 from, bool friendly, GaKitId kit)
        {
            GaDropBot best = null;
            float bestD = 1e12f;
            for (int i = 0; i < Bots.Count; i++)
            {
                GaDropBot b = Bots[i];
                if (b == null || b.Friendly != friendly || b.Downed || b.Hp <= 0f)
                    continue;
                if (b.Kit != kit)
                    continue;
                float d = (b.transform.position - from).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = b;
                }
            }
            return best;
        }

        internal static GaDropBot NearbyAny(Vector3 from, float maxM)
        {
            GaDropBot best = null;
            float bestD = maxM * maxM;
            for (int i = 0; i < Bots.Count; i++)
            {
                GaDropBot b = Bots[i];
                if (b == null || b.Downed || b.Hp <= 0f)
                    continue;
                float d = (b.transform.position - from).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = b;
                }
            }
            return best;
        }

        internal static GaDropBot NearestDowned(Vector3 from, bool friendly, float maxM)
        {
            GaDropBot best = null;
            float bestD = maxM * maxM;
            for (int i = 0; i < Bots.Count; i++)
            {
                GaDropBot b = Bots[i];
                if (b == null || b.Friendly != friendly || !b.Downed)
                    continue;
                float d = (b.transform.position - from).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = b;
                }
            }
            return best;
        }

        internal static void Blast(Vector3 at, float radius, float dmg)
        {
            float r2 = radius * radius;
            for (int i = 0; i < Bots.Count; i++)
            {
                GaDropBot b = Bots[i];
                if (b == null || b.Downed)
                    continue;
                float d2 = (b.transform.position - at).sqrMagnitude;
                if (d2 > r2)
                    continue;
                float fall = 1f - Mathf.Sqrt(d2) / radius;
                if (fall < 0.2f)
                    fall = 0.2f;
                b.ApplyDamage(dmg * fall, dmg * 0.4f * fall, 0f, 4f);
            }
        }

        internal static bool SkipCollider(Collider col)
        {
            if (col == null)
                return false;
            GaDropBot b = GaPhys.Bot(col);
            return b != null && b.Friendly;
        }

        private static GaDropBot Nearest(Vector3 from, bool friendly)
        {
            GaDropBot best = null;
            float bestD = 1e12f;
            for (int i = 0; i < Bots.Count; i++)
            {
                GaDropBot b = Bots[i];
                if (b == null || b.Friendly != friendly || b.Hp <= 0f)
                    continue;
                float d = (b.transform.position - from).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = b;
                }
            }
            return best;
        }

        private static int Count(bool friendly)
        {
            int n = 0;
            for (int i = 0; i < Bots.Count; i++)
            {
                if (Bots[i] != null && Bots[i].Friendly == friendly && Bots[i].Hp > 0f)
                    n++;
            }
            return n;
        }

        private static void SpawnFrom(Aircraft ac, Vector3 inherit, bool friendly, int n)
        {
            if (ac == null)
                return;
            if (!friendly && Count(false) >= EnemyCap)
                return;
            if (friendly && Count(true) >= FriendCount)
                n = FriendCount - Count(true);
            if (n <= 0)
                return;
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = GaChute.ExitPoint(ac, i + 1);
                GameObject go = new GameObject(friendly ? "GaDropFriend" : "GaDropFoe");
                go.transform.position = pos;
                go.transform.rotation = Quaternion.LookRotation(-ac.transform.forward, Vector3.up);
                GaDropBot bot = go.AddComponent<GaDropBot>();
                Vector3 v = inherit + ac.transform.right * ((i % 3) - 1) * 1.8f;
                bot.Boot(friendly, v, i, true, NextKit());
                Bots.Add(bot);
            }
        }

        private static void TryOpposingDrop()
        {
            Aircraft ship = NearestHostileDropship();
            if (ship == null)
                return;
            Vector3 inherit = Vector3.zero;
            try
            {
                if (ship.rb != null)
                    inherit = ship.rb.velocity;
            }
            catch { }
            MarkDropped(ship);
            SpawnFrom(ship, inherit, false, EnemyPerShip);
        }

        private static void TrySpawnEnemyLift()
        {
            if (!SimulatesWorld())
                return;
            if (!GaMode.Active && !GaAirLift.Flying)
                return;
            float now = Time.unscaledTime;
            if (_nextLift <= 0.01f)
                _nextLift = now + 18f;
            if (now < _nextLift)
                return;
            _nextLift = now + UnityEngine.Random.Range(42f, 78f);
            if (Count(false) >= EnemyCap)
                return;
            if (HostileLiftAirborne())
                return;
            if (UnityEngine.Random.value > 0.62f)
                return;
            Airbase ab = EnemyField();
            if (ab == null)
                return;
            AircraftDefinition uh = GaChute.Def(DropKind.Uh90);
            AircraftDefinition chim = GaChute.Def(DropKind.Chimera);
            AircraftDefinition vl = GaChute.Def(DropKind.Vl49);
            AircraftDefinition def = null;
            bool wantVl = UnityEngine.Random.value > 0.45f;
            try
            {
                if (wantVl && vl != null && ab.CanSpawnAircraft(vl))
                    def = vl;
                else if (chim != null && ab.CanSpawnAircraft(chim))
                    def = chim;
                else if (uh != null && ab.CanSpawnAircraft(uh))
                    def = uh;
                else if (vl != null && ab.CanSpawnAircraft(vl))
                    def = vl;
            }
            catch { return; }
            if (def == null)
                return;
            try
            {
                ab.TrySpawnAircraft(null, def, new LiveryKey(0), new Loadout(), 1f);
            }
            catch { }
        }

        private static bool HostileLiftAirborne()
        {
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return false;
            FactionHQ hq = GaPerfFrameGate.Hq();
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i] as Aircraft;
                if (ac == null || ac.disabled)
                    continue;
                if (!GaChute.IsDropship(ac))
                    continue;
                if (IsLocalHq(ac, hq))
                    continue;
                float agl = GaChute.ProbeAgl(ac.transform.position);
                if (agl > 40f)
                    return true;
            }
            return false;
        }

        private static Airbase EnemyField()
        {
            FactionHQ hq = GaPerfFrameGate.Hq();
            Airbase[] abs = GaPerfFrameGate.Airbases();
            if (abs == null)
                return null;
            Vector3 from = GaMode.Active ? GaMode.EyePos : Vector3.zero;
            if (GaAirLift.Flying && GaAirLift.Ac != null)
                from = GaAirLift.Ac.transform.position;
            Airbase best = null;
            float bestD = 1e18f;
            for (int i = 0; i < abs.Length; i++)
            {
                Airbase ab = abs[i];
                if (ab == null || ab.disabled)
                    continue;
                try
                {
                    if (ab.CurrentHQ == null)
                        continue;
                    if (hq != null && object.ReferenceEquals(ab.CurrentHQ, hq))
                        continue;
                    if (ab.AttachedAirbase)
                        continue;
                }
                catch { continue; }
                Vector3 p;
                try { p = ab.center != null ? ab.center.position : ab.transform.position; }
                catch { p = ab.transform.position; }
                float d = (p - from).sqrMagnitude;
                if (from.sqrMagnitude < 1f)
                    d = 0f;
                if (d < bestD)
                {
                    bestD = d;
                    best = ab;
                }
            }
            return best;
        }

        private static void WatchHostiles()
        {
            if (!SimulatesWorld())
                return;
            if (Count(false) >= EnemyCap)
                return;
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return;
            FactionHQ hq = GaPerfFrameGate.Hq();
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i] as Aircraft;
                if (ac == null || ac.disabled)
                    continue;
                if (object.ReferenceEquals(ac, GaMode.ParkedAircraft))
                    continue;
                if (!GaChute.IsDropship(ac))
                    continue;
                if (IsLocalHq(ac, hq))
                    continue;
                if (AlreadyDropped(ac))
                    continue;
                float agl = GaChute.ProbeAgl(ac.transform.position);
                if (agl < 45f || agl > 8500f)
                    continue;
                Vector3 inherit = Vector3.zero;
                try
                {
                    if (ac.rb != null)
                        inherit = ac.rb.velocity;
                }
                catch { }
                MarkDropped(ac);
                SpawnFrom(ac, inherit, false, EnemyPerShip);
                if (Count(false) >= EnemyCap)
                    return;
            }
        }

        private static float _nextField;

        private static void FillEnemyGround()
        {
            if (!SimulatesWorld())
                return;
            if (Time.unscaledTime < _nextField)
                return;
            _nextField = Time.unscaledTime + 2.4f;
            int need = EnemyCap - Count(false);
            if (need <= 0)
                return;
            Vector3 player = GaMode.Active ? GaMode.EyePos : Vector3.zero;
            FactionHQ hq = GaPerfFrameGate.Hq();
            Airbase[] abs = GaPerfFrameGate.Airbases();
            if (abs != null)
            {
                for (int i = 0; i < abs.Length && need > 0; i++)
                {
                    Airbase ab = abs[i];
                    if (ab == null || ab.disabled)
                        continue;
                    try
                    {
                        if (ab.CurrentHQ == null)
                            continue;
                        if (hq != null && object.ReferenceEquals(ab.CurrentHQ, hq))
                            continue;
                    }
                    catch { continue; }
                    Vector3 p;
                    try { p = ab.center != null ? ab.center.position : ab.transform.position; }
                    catch { p = ab.transform.position; }
                    int n = need > 4 ? 4 : need;
                    SpawnEnemyAt(p, n, player);
                    need = EnemyCap - Count(false);
                }
            }
            if (need <= 0)
                return;
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return;
            for (int i = 0; i < all.Count && need > 0; i++)
            {
                GroundVehicle v = all[i] as GroundVehicle;
                if (v == null || v.disabled)
                    continue;
                try
                {
                    if (hq != null && v.NetworkHQ != null && object.ReferenceEquals(v.NetworkHQ, hq))
                        continue;
                }
                catch { continue; }
                Vector3 p = v.transform.position;
                if (player.sqrMagnitude > 1f && (p - player).sqrMagnitude < 3600f)
                    continue;
                int n = need > 2 ? 2 : need;
                SpawnEnemyAt(p, n, player);
                need = EnemyCap - Count(false);
            }
        }

        private static void SpawnEnemyAt(Vector3 at, int n, Vector3 player)
        {
            if (n <= 0)
                return;
            if (Count(false) >= EnemyCap)
                return;
            if (Count(false) + n > EnemyCap)
                n = EnemyCap - Count(false);
            for (int k = 0; k < n; k++)
            {
                Vector3 off = new Vector3(((k % 4) - 1.5f) * 3.2f, 0f, 2.4f + (k / 2) * 2.8f);
                Vector3 pos = at + off;
                RaycastHit hit;
                if (GaPhys.RayDown(pos + Vector3.up * 8f, 22f, out hit))
                    pos = hit.point + Vector3.up * 0.08f;
                if (player.sqrMagnitude > 1f && (pos - player).sqrMagnitude < 900f)
                    continue;
                GameObject go = new GameObject("GaDropFoe");
                go.transform.position = pos;
                GaDropBot bot = go.AddComponent<GaDropBot>();
                bot.Boot(false, Vector3.zero, k, false, NextKit());
                Bots.Add(bot);
            }
        }

        internal static bool SimulatesWorld()
        {
            return !GaNet.PeerPlay || GaNet.IsHost;
        }

        internal static GaDropBot FindNet(ushort id)
        {
            if (id == 0)
                return null;
            for (int i = 0; i < Bots.Count; i++)
            {
                GaDropBot b = Bots[i];
                if (b != null && b.NetId == id)
                    return b;
            }
            return null;
        }

        internal static int PackHostiles(byte[] buf)
        {
            if (buf == null || buf.Length < 8)
                return 0;
            int n = 0;
            int at = 2;
            for (int i = 0; i < Bots.Count && at + 22 <= buf.Length; i++)
            {
                GaDropBot b = Bots[i];
                if (b == null || b.Friendly || b.Puppet)
                    continue;
                WriteU16(buf, at, b.NetId);
                WriteF32(buf, at + 2, b.transform.position.x);
                WriteF32(buf, at + 6, b.transform.position.y);
                WriteF32(buf, at + 10, b.transform.position.z);
                WriteF32(buf, at + 14, b.transform.eulerAngles.y);
                buf[at + 18] = (byte)b.Kit;
                byte f = 1;
                if (b.Downed)
                    f |= 2;
                if (b.Hp <= 0f)
                    f |= 16;
                buf[at + 19] = f;
                int hp = Mathf.RoundToInt(b.Hp);
                if (hp < 0)
                    hp = 0;
                if (hp > 255)
                    hp = 255;
                buf[at + 20] = (byte)hp;
                buf[at + 21] = (byte)b.Slot;
                at += 22;
                n++;
            }
            buf[0] = (byte)n;
            buf[1] = 0;
            return at;
        }

        internal static void ApplyPacked(byte[] buf, int len)
        {
            if (buf == null || len < 2)
                return;
            int n = buf[0];
            int at = 2;
            float now = Time.unscaledTime;
            for (int i = 0; i < n && at + 22 <= len; i++)
            {
                ushort id = ReadU16(buf, at);
                Vector3 p = new Vector3(ReadF32(buf, at + 2), ReadF32(buf, at + 6), ReadF32(buf, at + 10));
                float yaw = ReadF32(buf, at + 14);
                GaKitId kit = (GaKitId)buf[at + 18];
                byte flags = buf[at + 19];
                float hp = buf[at + 20];
                int slot = buf[at + 21];
                at += 22;
                GaDropBot b = FindNet(id);
                if (b == null)
                {
                    GameObject go = new GameObject("GaDropNet");
                    go.transform.position = p;
                    go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    b = go.AddComponent<GaDropBot>();
                    b.NetId = id;
                    b.Puppet = true;
                    b.Boot(false, Vector3.zero, slot, false, kit);
                    b.NetId = id;
                    b.Puppet = true;
                    Bots.Add(b);
                }
                b.SeenAt = now;
                b.Puppet = true;
                b.transform.position = p;
                b.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                b.Hp = hp;
                if ((flags & 2) != 0 || (flags & 16) != 0)
                {
                    if (!b.Downed)
                    {
                        b.Downed = true;
                        GaBody.Tick(b.transform, 0f, true);
                    }
                }
            }
            for (int i = Bots.Count - 1; i >= 0; i--)
            {
                GaDropBot b = Bots[i];
                if (b == null || !b.Puppet)
                    continue;
                if (now - b.SeenAt < 4f)
                    continue;
                UnityEngine.Object.Destroy(b.gameObject);
                Bots.RemoveAt(i);
            }
        }

        internal static void HurtNet(ushort id, float pierce, float blast, float impact)
        {
            GaDropBot b = FindNet(id);
            if (b == null || b.Puppet)
                return;
            b.HurtByPlayer = true;
            b.HurtLocal(pierce, blast, impact);
        }

        private static void TickVisualOnly()
        {
            float dt = Time.deltaTime;
            Vector3 origin = GaMode.Active ? GaMode.EyePos : Vector3.zero;
            int friends = 0;
            int foes = 0;
            for (int i = Bots.Count - 1; i >= 0; i--)
            {
                GaDropBot b = Bots[i];
                if (b == null)
                {
                    Bots.RemoveAt(i);
                    continue;
                }
                if (b.Hp > 0f && !b.Downed)
                {
                    if (b.Friendly)
                        friends++;
                    else
                        foes++;
                }
                int lod = 0;
                if (origin.sqrMagnitude > 0.01f)
                {
                    float d2 = (b.transform.position - origin).sqrMagnitude;
                    if (d2 > 25600f)
                        lod = 2;
                    else if (d2 > 6400f)
                        lod = 1;
                }
                b.Tick(dt, lod);
            }
            FriendsAlive = friends;
            HostilesAlive = foes;
            Hostiles.Clear();
            for (int i = 0; i < Bots.Count; i++)
            {
                GaDropBot b = Bots[i];
                if (b != null && !b.Friendly && b.Hp > 0f && !b.Downed)
                    Hostiles.Add(b);
            }
        }

        private static void WriteU16(byte[] b, int i, ushort v)
        {
            b[i] = (byte)v;
            b[i + 1] = (byte)(v >> 8);
        }

        private static ushort ReadU16(byte[] b, int i)
        {
            return (ushort)(b[i] | (b[i + 1] << 8));
        }

        private static void WriteF32(byte[] b, int i, float v)
        {
            byte[] raw = System.BitConverter.GetBytes(v);
            b[i] = raw[0];
            b[i + 1] = raw[1];
            b[i + 2] = raw[2];
            b[i + 3] = raw[3];
        }

        private static float ReadF32(byte[] b, int i)
        {
            return System.BitConverter.ToSingle(b, i);
        }

        private static GaKitId NextKit()
        {
            int i = _kitClock % GaClass.Count;
            _kitClock++;
            return (GaKitId)i;
        }

        private static void WatchEnemyFields()
        {
            FillEnemyGround();
        }

        private static Aircraft NearestHostileDropship()
        {
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return null;
            FactionHQ hq = GaPerfFrameGate.Hq();
            Vector3 at = GaMode.Active ? GaMode.EyePos : Vector3.zero;
            if (!GaMode.Active)
            {
                try
                {
                    Aircraft mine;
                    if (GameManager.GetLocalAircraft(out mine) && mine != null)
                        at = mine.transform.position;
                }
                catch { }
            }
            Aircraft best = null;
            float bestD = 1.4e8f;
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i] as Aircraft;
                if (ac == null || ac.disabled || !GaChute.IsDropship(ac))
                    continue;
                if (object.ReferenceEquals(ac, GaMode.ParkedAircraft))
                    continue;
                if (IsLocalHq(ac, hq))
                    continue;
                float d = (ac.transform.position - at).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = ac;
                }
            }
            return best;
        }

        private static bool IsLocalHq(Aircraft ac, FactionHQ hq)
        {
            if (ac == null || hq == null)
                return true;
            try
            {
                FactionHQ ship = ac.NetworkHQ;
                if (ship == null)
                    return true;
                return object.ReferenceEquals(ship, hq);
            }
            catch
            {
                return true;
            }
        }

        private static bool AlreadyDropped(Aircraft ac)
        {
            int id = ac.GetInstanceID();
            float t;
            if (!Dropped.TryGetValue(id, out t))
                return false;
            return Time.unscaledTime < t + 90f;
        }

        private static void MarkDropped(Aircraft ac)
        {
            if (ac == null)
                return;
            Dropped[ac.GetInstanceID()] = Time.unscaledTime;
        }
    }
}
