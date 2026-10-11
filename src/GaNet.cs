using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mirage;
using Mirage.Serialization;
using NuclearOption.Networking;
using UnityEngine;

namespace GroundArsenal
{
    internal struct GaBlob
    {
        public byte Kind;
        public ulong A;
        public ulong B;
        public uint Team;
        public float X;
        public float Y;
        public float Z;
        public float W;
        public byte K;
        public byte F;
        public byte G;
        public byte H;
        public ushort N;
        public byte[] Extra;
    }

    internal static class GaNet
    {
        internal const byte KindPlayer = 1;
        internal const byte KindBots = 2;
        internal const byte KindHit = 3;
        internal const byte KindLeave = 4;

        internal static bool PeerPlay;
        internal static bool IsHost;
        internal static ulong LocalWho;
        internal static uint LocalTeam;

        private static bool _codec;
        private static NetworkClient _cli;
        private static NetworkServer _srv;
        private static float _nextPeer;
        private static float _nextPlayer;
        private static float _nextBots;
        private static readonly byte[] BotBuf = new byte[512];
        private static readonly Dictionary<ulong, GaNetPawn> Avatars =
            new Dictionary<ulong, GaNetPawn>(8);
        private static readonly List<ulong> DeadKeys = new List<ulong>(8);

        internal static void Tick()
        {
            Ensure();
            RefreshPeers();
            if (!PeerPlay)
            {
                if (Avatars.Count > 0)
                    ClearAvatars();
                return;
            }
            if (GaMode.Active)
                MaybeSendPlayer();
            if (IsHost && GaDropSquad.SimulatesWorld())
                MaybeSendBots();
            PruneAvatars();
        }

        internal static void LeaveInfantry()
        {
            if (!PeerPlay)
                return;
            GaBlob msg = default(GaBlob);
            msg.Kind = KindLeave;
            msg.A = Who();
            Push(msg, true);
        }

        internal static void Shutdown()
        {
            LeaveInfantry();
            ClearAvatars();
        }

        internal static GaNetPawn FromHit(Collider col)
        {
            if (col == null)
                return null;
            return col.GetComponentInParent<GaNetPawn>();
        }

        internal static GaNetPawn NearestEnemy(Vector3 from)
        {
            return Nearest(from, false, false);
        }

        internal static GaNetPawn NearestAllyPlayer(Vector3 from)
        {
            return Nearest(from, true, false);
        }

        internal static GaNetPawn NearestDownedAlly(Vector3 from, float maxM)
        {
            GaNetPawn best = null;
            float bestD = maxM * maxM;
            foreach (KeyValuePair<ulong, GaNetPawn> kv in Avatars)
            {
                GaNetPawn p = kv.Value;
                if (p == null || !p.Ally || !p.Downed)
                    continue;
                float d = (p.transform.position - from).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }
            return best;
        }

        internal static void ReportBotHit(ushort id, float pierce, float blast, float impact)
        {
            if (id == 0)
                return;
            if (IsHost)
            {
                GaDropSquad.HurtNet(id, pierce, blast, impact);
                return;
            }
            GaBlob msg = default(GaBlob);
            msg.Kind = KindHit;
            msg.A = Who();
            msg.N = id;
            msg.X = pierce;
            msg.Y = blast;
            msg.Z = impact;
            Push(msg, true);
        }

        internal static void ReportPlayerHit(ulong who, float pierce, float blast, float impact)
        {
            if (who == 0 || who == LocalWho)
                return;
            GaBlob msg = default(GaBlob);
            msg.Kind = KindHit;
            msg.A = Who();
            msg.B = who;
            msg.X = pierce;
            msg.Y = blast;
            msg.Z = impact;
            Push(msg, true);
        }

        private static GaNetPawn Nearest(Vector3 from, bool ally, bool downed)
        {
            GaNetPawn best = null;
            float bestD = 1e12f;
            foreach (KeyValuePair<ulong, GaNetPawn> kv in Avatars)
            {
                GaNetPawn p = kv.Value;
                if (p == null || p.Ally != ally)
                    continue;
                if (p.Downed != downed && downed)
                    continue;
                if (p.Downed && !downed)
                    continue;
                float d = (p.transform.position - from).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }
            return best;
        }

        [SkipLocalsInit]
        private static void Ensure()
        {
            if (!_codec)
            {
                try
                {
                    Writer<GaBlob>.Write = WriteBlob;
                    Reader<GaBlob>.Read = ReadBlob;
                    MessagePacker.RegisterMessage<GaBlob>();
                    _codec = true;
                }
                catch (Exception ex)
                {
                    if (GaPlugin.Log != null)
                        GaPlugin.Log.LogWarning("GaNet codec: " + ex.Message);
                    return;
                }
            }
            Player p = LocalPlayer();
            if (p == null)
                return;
            NetworkClient cli = null;
            NetworkServer srv = null;
            try { cli = p.Client; }
            catch { cli = null; }
            try { srv = p.Server; }
            catch { srv = null; }
            if (cli != null && !object.ReferenceEquals(cli, _cli))
            {
                try
                {
                    cli.MessageHandler.RegisterHandler<GaBlob>(OnClient, false);
                    _cli = cli;
                }
                catch (Exception ex)
                {
                    if (GaPlugin.Log != null)
                        GaPlugin.Log.LogWarning("GaNet client: " + ex.Message);
                }
            }
            if (srv != null && !object.ReferenceEquals(srv, _srv))
            {
                try
                {
                    srv.MessageHandler.RegisterHandler<GaBlob>(OnServer, false);
                    _srv = srv;
                }
                catch (Exception ex)
                {
                    if (GaPlugin.Log != null)
                        GaPlugin.Log.LogWarning("GaNet server: " + ex.Message);
                }
            }
        }

        private static void RefreshPeers()
        {
            float now = Time.unscaledTime;
            if (now < _nextPeer)
                return;
            _nextPeer = now + 0.45f;
            Player p = LocalPlayer();
            LocalWho = Who(p);
            LocalTeam = TeamOf(p);
            IsHost = false;
            int n = 0;
            try
            {
                if (p != null)
                    IsHost = p.IsHost || p.IsServer;
            }
            catch { }
            Player[] all = null;
            try { all = UnityEngine.Object.FindObjectsOfType<Player>(); }
            catch { all = null; }
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null)
                        n++;
                }
            }
            PeerPlay = n > 1;
        }

        private static void MaybeSendPlayer()
        {
            float now = Time.unscaledTime;
            if (now < _nextPlayer)
                return;
            _nextPlayer = now + 0.10f;
            Transform root = GaMode.PawnRoot;
            if (root == null)
                return;
            GaBlob msg = default(GaBlob);
            msg.Kind = KindPlayer;
            msg.A = Who();
            msg.Team = LocalTeam;
            Vector3 pos = root.position;
            msg.X = pos.x;
            msg.Y = pos.y;
            msg.Z = pos.z;
            msg.W = GaMode.LookYaw;
            msg.K = (byte)GaClass.Picked;
            byte f = 1;
            if (GaMode.Downed)
                f |= 2;
            if (Input.GetMouseButton(0))
                f |= 4;
            if (GaMode.Sprinting)
                f |= 8;
            if (GaMode.Dead)
                f |= 16;
            msg.F = f;
            int gun = GaCombat.HeldId;
            if (gun < 0)
                gun = 0;
            if (gun > 255)
                gun = 255;
            msg.G = (byte)gun;
            int hp = Mathf.RoundToInt(GaMode.Hp);
            if (hp < 0)
                hp = 0;
            if (hp > 255)
                hp = 255;
            msg.H = (byte)hp;
            Push(msg, false);
        }

        private static void MaybeSendBots()
        {
            float now = Time.unscaledTime;
            if (now < _nextBots)
                return;
            _nextBots = now + 0.20f;
            int n = GaDropSquad.PackHostiles(BotBuf);
            if (n <= 2)
                return;
            byte[] extra = new byte[n];
            Buffer.BlockCopy(BotBuf, 0, extra, 0, n);
            GaBlob msg = default(GaBlob);
            msg.Kind = KindBots;
            msg.A = Who();
            msg.Extra = extra;
            msg.N = (ushort)n;
            Push(msg, false);
        }

        private static void Push(GaBlob msg, bool reliable)
        {
            Channel ch = reliable ? Channel.Reliable : Channel.Unreliable;
            try
            {
                if (IsHost && _srv != null && _srv.Active)
                    _srv.SendToAll(msg, true, true, ch);
                else if (_cli != null && _cli.Active)
                    _cli.Send(msg, ch);
            }
            catch (Exception ex)
            {
                if (GaPlugin.Log != null)
                    GaPlugin.Log.LogWarning("GaNet send: " + ex.Message);
            }
        }

        private static void OnServer(INetworkPlayer sender, GaBlob msg)
        {
            Relay(sender, msg);
            Apply(msg);
        }

        private static void OnClient(INetworkPlayer sender, GaBlob msg)
        {
            Apply(msg);
        }

        private static void Relay(INetworkPlayer sender, GaBlob msg)
        {
            if (_srv == null || !_srv.Active)
                return;
            Channel ch = msg.Kind == KindHit || msg.Kind == KindLeave
                ? Channel.Reliable : Channel.Unreliable;
            try
            {
                INetworkPlayer local = _srv.LocalPlayer;
                foreach (INetworkPlayer dest in _srv.AuthenticatedPlayers)
                {
                    if (dest == null)
                        continue;
                    if (object.ReferenceEquals(dest, sender))
                        continue;
                    if (local != null && object.ReferenceEquals(dest, local))
                        continue;
                    NetworkPlayer np = dest as NetworkPlayer;
                    if (np != null)
                        np.Send(msg, ch);
                }
            }
            catch { }
        }

        private static void Apply(GaBlob msg)
        {
            if (msg.Kind == KindPlayer)
                ApplyPlayer(msg);
            else if (msg.Kind == KindBots)
            {
                if (!IsHost && msg.Extra != null)
                    GaDropSquad.ApplyPacked(msg.Extra, msg.Extra.Length);
            }
            else if (msg.Kind == KindHit)
                ApplyHit(msg);
            else if (msg.Kind == KindLeave)
                Remove(msg.A);
        }

        private static void ApplyPlayer(GaBlob msg)
        {
            if (msg.A == 0 || msg.A == LocalWho)
                return;
            if ((msg.F & 32) != 0)
            {
                Remove(msg.A);
                return;
            }
            GaNetPawn pawn;
            if (!Avatars.TryGetValue(msg.A, out pawn) || pawn == null)
            {
                GameObject go = new GameObject("GaNetPawn");
                go.transform.position = new Vector3(msg.X, msg.Y, msg.Z);
                go.transform.rotation = Quaternion.Euler(0f, msg.W, 0f);
                pawn = go.AddComponent<GaNetPawn>();
                pawn.Boot(msg.A, msg.Team, (GaKitId)msg.K, msg.G);
                Avatars[msg.A] = pawn;
            }
            pawn.Push(msg);
        }

        private static void ApplyHit(GaBlob msg)
        {
            if (msg.N != 0 && IsHost)
            {
                GaDropSquad.HurtNet(msg.N, msg.X, msg.Y, msg.Z);
                return;
            }
            if (msg.B != 0 && msg.B == LocalWho && GaMode.Active)
                GaMode.ReceiveHit(msg.X, msg.Y, 0f, msg.Z, PersistentID.None, Vector3.forward);
        }

        private static void Remove(ulong who)
        {
            GaNetPawn pawn;
            if (!Avatars.TryGetValue(who, out pawn))
                return;
            Avatars.Remove(who);
            if (pawn != null)
                UnityEngine.Object.Destroy(pawn.gameObject);
        }

        private static void PruneAvatars()
        {
            float now = Time.unscaledTime;
            DeadKeys.Clear();
            foreach (KeyValuePair<ulong, GaNetPawn> kv in Avatars)
            {
                if (kv.Value == null || now - kv.Value.Seen > 4f)
                    DeadKeys.Add(kv.Key);
            }
            for (int i = 0; i < DeadKeys.Count; i++)
                Remove(DeadKeys[i]);
        }

        private static void ClearAvatars()
        {
            foreach (KeyValuePair<ulong, GaNetPawn> kv in Avatars)
            {
                if (kv.Value != null)
                    UnityEngine.Object.Destroy(kv.Value.gameObject);
            }
            Avatars.Clear();
        }

        internal static ulong Who()
        {
            return Who(LocalPlayer());
        }

        internal static ulong Who(Player p)
        {
            if (p == null)
                return 0;
            try
            {
                ulong s = p.SteamID;
                if (s != 0)
                    return s;
            }
            catch { }
            try { return 0x4E000000UL | p.NetId; }
            catch { return 0; }
        }

        internal static uint TeamOf(Player p)
        {
            if (p == null)
                return 0;
            try
            {
                FactionHQ hq = p.HQ;
                if (hq != null)
                    return hq.NetId;
            }
            catch { }
            return 0;
        }

        private static Player LocalPlayer()
        {
            Player p;
            try
            {
                if (GameManager.GetLocalPlayer(out p) && p != null)
                    return p;
            }
            catch { }
            return null;
        }

        private static void WriteBlob(NetworkWriter w, GaBlob msg)
        {
            w.WriteByte(msg.Kind);
            w.WriteUInt64(msg.A);
            w.WriteUInt64(msg.B);
            w.WriteUInt32(msg.Team);
            w.WriteSingle(msg.X);
            w.WriteSingle(msg.Y);
            w.WriteSingle(msg.Z);
            w.WriteSingle(msg.W);
            w.WriteByte(msg.K);
            w.WriteByte(msg.F);
            w.WriteByte(msg.G);
            w.WriteByte(msg.H);
            w.WriteUInt16(msg.N);
            int n = msg.Extra != null ? msg.Extra.Length : 0;
            if (n > 900)
                n = 900;
            w.WriteUInt16((ushort)n);
            if (n > 0)
                w.WriteBytes(msg.Extra, 0, n);
        }

        private static GaBlob ReadBlob(NetworkReader r)
        {
            GaBlob msg = default(GaBlob);
            msg.Kind = r.ReadByte();
            msg.A = r.ReadUInt64();
            msg.B = r.ReadUInt64();
            msg.Team = r.ReadUInt32();
            msg.X = r.ReadSingle();
            msg.Y = r.ReadSingle();
            msg.Z = r.ReadSingle();
            msg.W = r.ReadSingle();
            msg.K = r.ReadByte();
            msg.F = r.ReadByte();
            msg.G = r.ReadByte();
            msg.H = r.ReadByte();
            msg.N = r.ReadUInt16();
            int n = r.ReadUInt16();
            if (n > 0 && n <= 900)
            {
                msg.Extra = new byte[n];
                r.ReadBytes(msg.Extra, 0, n);
            }
            return msg;
        }
    }

    [DefaultExecutionOrder(250)]
    internal sealed class GaNetPawn : MonoBehaviour, IDamageable
    {
        internal ulong Who;
        internal uint Team;
        internal bool Ally;
        internal bool Downed;
        internal float Seen;
        internal GaKitId Kit;

        private GameObject _held;
        private Transform _hand;
        private int _gun = -1;
        private ArmorProperties _armor;
        private Vector3 _tgt;
        private float _yaw;
        private byte _flags;
        private float _lastFire;

        internal void Boot(ulong who, uint team, GaKitId kit, byte gun)
        {
            Who = who;
            Team = team;
            Kit = kit;
            Ally = team != 0 && team == GaNet.LocalTeam;
            Seen = Time.unscaledTime;
            _tgt = transform.position;
            _armor = new ArmorProperties();
            _armor.pierceArmor = 18f;
            _armor.blastArmor = 6f;
            CharacterController cc = gameObject.AddComponent<CharacterController>();
            GaPhys.TuneWalker(cc);
            cc.enabled = false;
            GaPhys.Hitbox(gameObject);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            GaPhys.Strip(body);
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            Renderer r = body.GetComponent<Renderer>();
            Color col = GaClass.Tint(kit);
            if (!Ally)
                col = Color.Lerp(col, new Color(0.72f, 0.22f, 0.16f), 0.45f);
            else
                col = Color.Lerp(col, new Color(0.35f, 0.55f, 0.38f), 0.35f);
            if (r != null)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                GaBody.SkinCapsule(r, col);
            }
            GaBody.Attach(transform, true);
            if (GaBody.Has(transform) && r != null)
                r.enabled = false;
            Hold(gun);
        }

        internal void Push(GaBlob msg)
        {
            Seen = Time.unscaledTime;
            _tgt = new Vector3(msg.X, msg.Y, msg.Z);
            _yaw = msg.W;
            _flags = msg.F;
            Downed = (msg.F & 2) != 0 || (msg.F & 16) != 0;
            Ally = msg.Team != 0 && msg.Team == GaNet.LocalTeam;
            Kit = (GaKitId)msg.K;
            if (msg.G != _gun)
                Hold(msg.G);
            if ((_flags & 4) != 0 && Time.unscaledTime >= _lastFire + 0.12f)
            {
                _lastFire = Time.unscaledTime;
                Vector3 o = transform.position + Vector3.up * 1.4f;
                Vector3 d = transform.forward;
                GaFx.Tracer(o, o + d * 40f, Ally
                    ? new Color(0.3f, 1f, 0.75f, 1f)
                    : new Color(1f, 0.45f, 0.12f, 1f));
            }
        }

        private void LateUpdate()
        {
            transform.position = Vector3.Lerp(transform.position, _tgt, 0.35f);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.Euler(0f, _yaw, 0f), 0.28f);
            float spd = (_tgt - transform.position).magnitude * 8f;
            GaBody.Tick(transform, Downed ? 0f : spd, Downed);
            if (_held != null && !Downed && !GaPerfFrameGate.SkipPolish())
            {
                Transform body = transform.Find("VanillaPilotBody");
                GaBody.FollowHeld(_held, transform, body != null ? body : transform, ref _hand);
                _held.SetActive(true);
            }
            else if (_held != null)
                _held.SetActive(false);
        }

        private void Hold(byte id)
        {
            _gun = id;
            if (_held != null)
            {
                UnityEngine.Object.Destroy(_held);
                _held = null;
            }
            GaGun g = GaCatalog.Get(id);
            if (g == null)
                return;
            try { _held = GaView.CreateSquadRifle(g); }
            catch { _held = null; }
            if (_held == null)
                return;
            _held.transform.SetParent(transform, false);
            _hand = null;
        }

        public void TakeDamage(float pierceDamage, float blastDamage, float amountAffected, float fireDamage, float ImpactDamage, PersistentID dealerID)
        {
            ApplyDamage(pierceDamage, blastDamage, fireDamage, ImpactDamage);
        }

        public void ApplyDamage(float pierceDamage, float blastDamage, float fireDamage, float impactDamage)
        {
            if (Ally || Downed)
                return;
            GaNet.ReportPlayerHit(Who, pierceDamage, blastDamage, impactDamage);
        }

        public void TakeShockwave(Vector3 origin, float overpressure, float blastPower)
        {
            ApplyDamage(overpressure > 4000f ? 18f : 6f, blastPower * 0.02f, 0f, 0f);
        }

        public void Detach(Vector3 velocity, Vector3 relativePos)
        {
        }

        public ArmorProperties GetArmorProperties()
        {
            return _armor;
        }

        public Unit GetUnit()
        {
            return null;
        }

        public float GetMass()
        {
            return 95f;
        }

        public Transform GetTransform()
        {
            return transform;
        }

        private void OnDestroy()
        {
            if (_held != null)
                UnityEngine.Object.Destroy(_held);
        }
    }
}
