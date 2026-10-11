using System.Collections.Generic;
using UnityEngine;

namespace GroundArsenal
{
    [DefaultExecutionOrder(250)]
    internal sealed class GaDropBot : MonoBehaviour, IDamageable
    {
        internal GaKitId Kit;
        internal bool Friendly;
        internal float Hp = 80f;
        internal int Slot;
        internal string Callsign = "UNKNOWN";
        internal bool Downed;
        internal bool HurtByPlayer;
        internal ushort NetId;
        internal bool Puppet;
        internal float SeenAt;
        internal Unit RideHost;
        internal int RideSeat;
        internal int RideGun = -1;
        internal float RideNextFire;

        private CharacterController _cc;
        private Vector3 _vel;
        private float _vert = -2f;
        private bool _falling = true;
        private bool _open;
        private float _born;
        private float _nextShot;
        private float _nextFrag;
        private GameObject _canopy;
        private ArmorProperties _armor;
        private Vector3 _form;
        private bool _contacted;
        private float _downUntil;
        private float _reviveHold;
        private float _nextRadio;
        private float _aimUntil;
        private Vector3 _aimCache;
        private bool _haveAim;
        private float _orbit;
        private float _peekUntil;
        private int _strafeDir = 1;
        private Vector3 _lastPos;
        private static ushort _netClock = 1;
        private static readonly string[] Tag = new string[]
        {
            "ALPHA", "BRAVO", "CHARLIE", "DELTA", "ECHO", "FOXTROT", "GOLF", "HOTEL"
        };
        private static readonly string[] KitTag = new string[]
        {
            "ASLT", "MED", "ENGI", "SUP", "SPEC", "AT", "SNP", "AA", "TSF"
        };

        internal bool Falling
        {
            get { return _falling; }
        }

        internal void Boot(bool friendly, Vector3 inherit, int slot)
        {
            Boot(friendly, inherit, slot, true, GaClass.Random());
        }

        internal void Boot(bool friendly, Vector3 inherit, int slot, bool falling, GaKitId kit)
        {
            Friendly = friendly;
            Slot = slot;
            Kit = kit;
            Callsign = MakeCall(friendly, slot, kit);
            Downed = false;
            HurtByPlayer = false;
            Hp = GaClass.MaxHp(kit) * (friendly ? 0.92f : 0.78f);
            _contacted = false;
            _vel = inherit;
            if (_vel.y > 2f)
                _vel.y = 2f;
            _born = Time.unscaledTime;
            _falling = falling;
            if (!falling)
            {
                _open = false;
                _vert = -2f;
            }
            float form = kit == GaKitId.Tsf ? 14f : 2.2f;
            _form = new Vector3(((slot % 3) - 1) * form, 0f, form - (slot % 2) * (form * 0.7f));
            if (NetId == 0)
            {
                _netClock++;
                if (_netClock == 0)
                    _netClock = 1;
                NetId = _netClock;
            }
            _orbit = slot * 1.7f;
            _strafeDir = (slot & 1) == 0 ? 1 : -1;
            _nextFrag = Time.time + UnityEngine.Random.Range(4f, 10f);
            _cc = gameObject.AddComponent<CharacterController>();
            if (kit == GaKitId.Tsf)
                GaPhys.TuneTsf(_cc);
            else
                GaPhys.TuneWalker(_cc);
            _armor = new ArmorProperties();
            if (kit == GaKitId.Tsf)
            {
                _armor.pierceArmor = 220f;
                _armor.blastArmor = 48f;
                _armor.pierceTolerance = 28f;
            }
            else
            {
                _armor.pierceArmor = 18f + GaClass.Armor(kit) * 1.4f;
                _armor.blastArmor = 6f + GaClass.Armor(kit) * 0.35f;
                _armor.pierceTolerance = 6f;
            }
            GaPhys.Hitbox(gameObject);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            GaPhys.Strip(body);
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            Renderer r = body.GetComponent<Renderer>();
            Color kitCol = GaClass.Tint(kit);
            if (!friendly)
                kitCol = Color.Lerp(kitCol, new Color(0.72f, 0.22f, 0.16f), 0.55f);
            else
                kitCol = Color.Lerp(kitCol, new Color(0.35f, 0.55f, 0.38f), 0.35f);
            if (r != null)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                GaBody.SkinCapsule(r, kitCol);
            }
            if (kit == GaKitId.Tsf)
            {
                if (r != null)
                    r.enabled = false;
                body.SetActive(false);
                GaTsf.AttachBot(transform);
            }
            else
            {
                GaBody.Attach(transform, friendly);
                if (GaBody.Has(transform) && r != null)
                    r.enabled = false;
                GaBody.Tick(transform, 0f, false);
                AttachGun();
            }
            _lastPos = transform.position;
        }

        private GameObject _held;
        private Transform _hand;

        private void AttachGun()
        {
            int[] guns = GaClass.Guns(Kit);
            if (guns == null || guns.Length == 0)
                return;
            int id = -1;
            for (int i = 0; i < guns.Length; i++)
            {
                if (guns[i] >= 0)
                {
                    id = guns[i];
                    break;
                }
            }
            if (id < 0)
                return;
            GaGun g = GaCatalog.Get(id);
            if (g == null)
                return;
            try
            {
                _held = GaView.CreateSquadRifle(g);
            }
            catch
            {
                _held = null;
            }
            if (_held == null)
                return;
            _held.transform.SetParent(transform, false);
            _held.transform.localScale = Vector3.one;
            _hand = null;
            PoseGun();
        }

        private Transform _deck;
        private Vector3 _deckLocal;
        private void LateUpdate()
        {
            if (_held == null || Downed)
                return;
            if (GaPerfFrameGate.SkipPolish())
                return;
            PoseGun();
        }

        private void PoseGun()
        {
            if (_held == null)
                return;
            Transform body = transform.Find("VanillaPilotBody");
            GaBody.FollowHeld(_held, transform, body != null ? body : transform, ref _hand);
        }

        private void ShowGun(bool on)
        {
            if (_held != null)
                _held.SetActive(on);
        }

        internal void Tick(float dt, int lod)
        {
            if (_cc == null)
                return;
            if (Downed)
            {
                TickDown(dt);
                GaBody.Tick(transform, 0f, true);
                ShowGun(false);
                return;
            }
            if (Hp <= 0f)
                return;
            if (Puppet)
            {
                TickPuppet(dt, lod);
                return;
            }
            if (RideHost != null)
            {
                TickRide(dt, lod);
                return;
            }
            if (_falling)
                Fall(dt);
            else
                Fight(dt, lod);
        }

        internal void Board(Unit host, int seat, int gun)
        {
            RideHost = host;
            RideSeat = seat;
            RideGun = gun;
            RideNextFire = 0f;
            if (_cc != null)
                _cc.enabled = false;
            ShowGun(false);
        }

        internal void ClearRide()
        {
            RideHost = null;
            RideSeat = 0;
            RideGun = -1;
            if (_cc != null)
                _cc.enabled = true;
            if (!Downed)
                ShowGun(true);
        }

        private void TickRide(float dt, int lod)
        {
            if (RideHost == null || RideHost.disabled || !GaRide.Busy
                || !object.ReferenceEquals(RideHost, GaRide.Host))
            {
                ClearRide();
                if (!_falling)
                    Fight(dt, lod);
                return;
            }
            GaRide.StickBot(this);
            if (lod < 2 && Kit != GaKitId.Tsf)
            {
                if (!GaBody.Has(transform))
                    GaBody.Attach(transform, Friendly);
                GaBody.Tick(transform, 0f, false, true);
            }
            ShowGun(false);
        }

        private void TickPuppet(float dt, int lod)
        {
            Vector3 p = transform.position;
            float spd = (p - _lastPos).magnitude / (dt > 0.0001f ? dt : 0.016f);
            _lastPos = p;
            if (lod < 2 && Kit != GaKitId.Tsf)
            {
                if (!GaBody.Has(transform))
                    GaBody.Attach(transform, Friendly);
                GaBody.Tick(transform, spd, false);
            }
        }

        internal void Revive()
        {
            Downed = false;
            Hp = GaClass.MaxHp(Kit) * 0.72f;
            HurtByPlayer = false;
            _vert = -2f;
            _falling = false;
            GaBody.Tick(transform, 0f, false);
            ShowGun(true);
        }

        private void Fall(float dt)
        {
            float agl = GaChute.ProbeAgl(transform.position);
            if (!_open)
            {
                bool want = Time.unscaledTime >= _born + 1.5f + Slot * 0.12f;
                if (agl > 0f && agl < 200f)
                    want = true;
                if (agl > 0f && agl < 26f)
                    want = false;
                if (want)
                {
                    _open = true;
                    _canopy = GaChute.MakeCanopy(transform);
                }
            }
            _vel.x = Mathf.MoveTowards(_vel.x, 0f, dt * (_open ? 16f : 4f));
            _vel.z = Mathf.MoveTowards(_vel.z, 0f, dt * (_open ? 16f : 4f));
            if (_open)
                _vert = Mathf.MoveTowards(_vert, -16f, dt * 24f);
            else
                _vert += Physics.gravity.y * dt;
            Vector3 move = new Vector3(_vel.x, _vert, _vel.z);
            CollisionFlags flags = _cc.Move(move * dt);
            if ((flags & CollisionFlags.Below) != 0 && Time.unscaledTime > _born + 0.45f)
            {
                _falling = false;
                if (!_open && _vert < -14f)
                {
                    Kill();
                    return;
                }
                _vert = -2f;
                if (_canopy != null)
                {
                    UnityEngine.Object.Destroy(_canopy);
                    _canopy = null;
                }
            }
        }

        private void Fight(float dt, int lod)
        {
            if (dt > 0.05f)
                dt = 0.05f;
            Vector3 threat;
            bool haveThreat = ThreatPos(out threat);
            Vector3 goal = haveThreat ? FightPos(threat) : Goal();
            Vector3 to = goal - transform.position;
            to.y = 0f;
            Vector3 wish = Vector3.zero;
            float dist = to.magnitude;
            GaDeck.Carry(transform, _cc, ref _deck, ref _deckLocal);
            bool grounded = StickGround(dt);
            if (!grounded)
            {
                _vert += Physics.gravity.y * dt;
                if (_vert < -28f)
                    _vert = -28f;
                _cc.Move(new Vector3(0f, _vert, 0f) * dt);
                GaDeck.Sample(transform, ref _deck, ref _deckLocal);
                GaBody.Tick(transform, 0f, false);
                return;
            }
            _orbit += dt * 0.9f;
            if (dist > 0.8f)
            {
                float spd = MoveSpd(dist, haveThreat);
                Vector3 dir = to / dist;
                if (haveThreat && dist < 14f && Time.unscaledTime < _peekUntil)
                    dir = Vector3.Cross(Vector3.up, dir) * _strafeDir;
                wish = dir * spd;
                if (lod < 2)
                {
                    Vector3 look = haveThreat ? threat - transform.position : to;
                    look.y = 0f;
                    if (look.sqrMagnitude > 0.04f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation,
                            Quaternion.LookRotation(look.normalized, Vector3.up), dt * 6f);
                    }
                }
            }
            _vert = -2.2f;
            wish.y = _vert;
            _cc.Move(wish * dt);
            GaDeck.Sample(transform, ref _deck, ref _deckLocal);
            if (lod < 2 && Kit != GaKitId.Tsf)
            {
                if (!GaBody.Has(transform))
                {
                    GaBody.Attach(transform, Friendly);
                    if (GaBody.Has(transform))
                    {
                        Transform cap = transform.Find("Body");
                        if (cap != null)
                        {
                            Renderer vis = cap.GetComponent<Renderer>();
                            if (vis != null)
                                vis.enabled = false;
                        }
                    }
                }
                GaBody.Tick(transform, wish.magnitude, false);
            }
            if (Friendly && Kit == GaKitId.Medic)
                TickMedic();
            if (lod < 2 && Time.time >= _nextShot)
                TryShoot();
            if (lod < 2)
                TickFrag(haveThreat, threat);
        }

        private void TickFrag(bool haveThreat, Vector3 threat)
        {
            if (!haveThreat)
                return;
            if (Kit == GaKitId.Medic || Kit == GaKitId.Sniper || Kit == GaKitId.AntiAir)
                return;
            if (Time.time < _nextFrag)
                return;
            Vector3 to = threat - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 12f || dist > 30f)
                return;
            if (Friendly)
            {
                Transform pawn = GaMode.PawnRoot;
                if (pawn != null && (pawn.position - threat).sqrMagnitude < 256f)
                    return;
            }
            Vector3 origin = transform.position + Vector3.up * 1.35f + transform.forward * 0.35f;
            Vector3 vel = (to / dist) * (11f + dist * 0.18f) + Vector3.up * 5.2f;
            GaFps.BotFrag(origin, vel);
            _nextFrag = Time.time + UnityEngine.Random.Range(7.5f, 13f);
        }

        private float MoveSpd(float dist, bool threat)
        {
            float sprint = GaClass.Sprint(Kit);
            if (!threat)
            {
                float spd = (Friendly ? 1.05f : 0.88f) * sprint;
                if (dist > 10f)
                    spd = Friendly ? 10.5f : 8.4f;
                if (dist > 24f)
                    spd += 2.4f;
                return spd;
            }
            if (Kit == GaKitId.Sniper)
                return sprint * 0.55f;
            if (Kit == GaKitId.Support)
                return sprint * 0.70f;
            if (Kit == GaKitId.Medic)
                return sprint * 0.62f;
            if (Kit == GaKitId.SpecOps)
                return sprint * 1.02f;
            return sprint * (Friendly ? 0.92f : 0.80f);
        }

        private Vector3 FightPos(Vector3 threat)
        {
            Vector3 at = transform.position;
            Vector3 to = at - threat;
            to.y = 0f;
            float d = to.magnitude;
            float want = HoldRange();
            Vector3 side = Vector3.zero;
            if (d > 0.2f)
                side = Vector3.Cross(Vector3.up, to / d) * (_strafeDir * (2.4f + Mathf.Sin(_orbit) * 2.8f));
            if (Kit == GaKitId.SpecOps)
                side *= 1.6f;
            bool los = HasLos(threat + Vector3.up * 1.2f);
            if (!los)
            {
                _peekUntil = Time.unscaledTime + 0.7f;
                return at + side + (d > 0.2f ? to.normalized * 2.2f : transform.right * 2f);
            }
            Vector3 ring;
            if (d > 0.2f)
                ring = threat + to / d * want;
            else
                ring = threat + transform.forward * want;
            ring += side;
            if (Friendly && Kit == GaKitId.Medic)
            {
                Transform root = GaMode.PawnRoot;
                if (root != null)
                    ring = Vector3.Lerp(ring, root.position + GaMode.FormRot * _form, 0.45f);
            }
            return ring;
        }

        private float HoldRange()
        {
            float r = GaClass.ShotRange(Kit);
            if (Kit == GaKitId.Sniper)
                return Mathf.Clamp(r * 0.72f, 28f, 90f);
            if (Kit == GaKitId.AntiAir)
                return Mathf.Clamp(r * 0.55f, 18f, 48f);
            if (Kit == GaKitId.AntiTank)
                return 22f;
            if (Kit == GaKitId.Support)
                return 16f;
            if (Kit == GaKitId.Assault)
                return 12f;
            if (Kit == GaKitId.SpecOps)
                return 9f;
            if (Kit == GaKitId.Tsf)
                return 80f;
            return 14f;
        }

        private bool HasLos(Vector3 to)
        {
            Vector3 o = transform.position + Vector3.up * (Kit == GaKitId.Tsf ? 12f : 1.4f);
            Vector3 d = to - o;
            float m = d.magnitude;
            if (m < 1.2f)
                return true;
            if (GaPerfFrameGate.SkipPolish())
                return true;
            RaycastHit hit;
            if (!Physics.Raycast(o, d / m, out hit, m - 0.4f, GaPhys.WorldMask, QueryTriggerInteraction.Ignore))
                return true;
            if (GaPhys.Bot(hit.collider) != null || GaPhys.Pawn(hit.collider) != null)
                return true;
            if (GaNet.FromHit(hit.collider) != null)
                return true;
            return false;
        }

        private bool StickGround(float dt)
        {
            RaycastHit hit;
            Vector3 origin = transform.position + Vector3.up * 1.02f;
            if (!Physics.Raycast(origin, Vector3.down, out hit, 6f, GaPhys.WalkMask, QueryTriggerInteraction.Ignore))
                return _cc.isGrounded;
            if (GaPhys.IsWalkerHit(hit.collider))
                return _cc.isGrounded;
            float sink = 1.02f - hit.distance;
            if (hit.distance > 1.28f)
                return false;
            if (sink > 0.06f || sink < -0.06f)
            {
                if (sink > 0.22f)
                    sink = 0.22f;
                if (sink < -0.22f)
                    sink = -0.22f;
                _cc.Move(new Vector3(0f, sink, 0f));
            }
            _vert = -2.2f;
            return true;
        }

        private void TickMedic()
        {
            if (GaFps.TryRevivePlayer(this))
                return;
            GaDropBot down = GaDropSquad.NearestDowned(transform.position, true, 2.6f);
            if (down == null)
            {
                _reviveHold = 0f;
                return;
            }
            _reviveHold += Time.deltaTime;
            if (_reviveHold < 1.5f)
                return;
            _reviveHold = 0f;
            down.Revive();
            GaRadio.Say(Callsign, "Stay with me!", false, 7);
        }

        private void TickDown(float dt)
        {
            if (Time.unscaledTime >= _downUntil)
            {
                UnityEngine.Object.Destroy(gameObject);
                return;
            }
            _vert = -4f;
            _cc.Move(new Vector3(0f, _vert, 0f) * dt);
        }

        private Vector3 Goal()
        {
            if (Friendly)
            {
                if (Kit == GaKitId.Medic)
                {
                    if (GaMode.Downed && GaMode.PawnRoot != null)
                        return GaMode.PawnRoot.position;
                    GaDropBot down = GaDropSquad.NearestDowned(transform.position, true, 80f);
                    if (down != null)
                        return down.transform.position;
                    GaNetPawn bleed = GaNet.NearestDownedAlly(transform.position, 80f);
                    if (bleed != null)
                        return bleed.transform.position;
                }
                if (GaDropSquad.HoldPos)
                    return GaDropSquad.HoldAt + _form;
                GaDropBot foe = GaDropSquad.NearestHostile(transform.position);
                if (foe != null)
                    return foe.transform.position;
                Transform root = GaMode.PawnRoot;
                if (root == null)
                    return transform.position;
                Vector3 toPlayer = root.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 48f * 48f)
                    return root.position + GaMode.FormRot * _form;
                Vector3 hunt = root.position + GaMode.FormRot * Vector3.forward * (18f + Slot * 6f)
                    + GaMode.FormRot * Vector3.right * (((Slot % 3) - 1) * 7f);
                return hunt;
            }
            Vector3 prey;
            if (NearestPrey(out prey))
                return prey;
            return transform.position;
        }

        private bool ThreatPos(out Vector3 pos)
        {
            pos = Vector3.zero;
            if (Friendly)
            {
                float best = 1e12f;
                GaDropBot foe = GaDropSquad.NearestHostile(transform.position);
                if (foe != null)
                {
                    best = (foe.transform.position - transform.position).sqrMagnitude;
                    pos = foe.transform.position;
                }
                GaNetPawn rp = GaNet.NearestEnemy(transform.position);
                if (rp != null)
                {
                    float d = (rp.transform.position - transform.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        pos = rp.transform.position;
                    }
                }
                if (best > 220f * 220f)
                    return false;
                return best < 1e11f;
            }
            return NearestPrey(out pos);
        }

        private bool NearestPrey(out Vector3 pos)
        {
            pos = Vector3.zero;
            float best = 1e12f;
            Transform local = GaMode.PawnRoot;
            if (local != null && !GaMode.Downed)
            {
                best = (local.position - transform.position).sqrMagnitude;
                pos = local.position;
            }
            GaDropBot mate = GaDropSquad.NearestFriendly(transform.position);
            if (mate != null)
            {
                float d = (mate.transform.position - transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    pos = mate.transform.position;
                }
            }
            GaNetPawn rp = GaNet.NearestAllyPlayer(transform.position);
            if (rp != null)
            {
                float d = (rp.transform.position - transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    pos = rp.transform.position;
                }
            }
            return best < 1e11f;
        }

        private void TryShoot()
        {
            Vector3 origin = transform.position + Vector3.up * 1.45f;
            Vector3 aim;
            if (!Aim(out aim))
                return;
            Vector3 dir = aim - origin;
            float dist = dir.magnitude;
            float max = GaClass.ShotRange(Kit);
            if (Kit == GaKitId.AntiAir)
                max += 40f;
            if (Kit == GaKitId.AntiTank)
                max += 10f;
            if (dist < 2f || dist > max)
                return;
            dir /= dist;
            if (!_contacted)
            {
                _contacted = true;
                GaRadio.SayContact(this);
            }
            RaycastHit hit;
            if (!Physics.Raycast(origin, dir, out hit, dist + 0.6f, GaPhys.WorldMask, QueryTriggerInteraction.Ignore))
                return;
            GaDropBot other = GaPhys.Bot(hit.collider);
            if (other != null && other.Friendly == Friendly)
                return;
            GaPawn pawn = other == null ? GaPhys.Pawn(hit.collider) : null;
            if (Friendly && pawn != null)
                return;
            GaNetPawn remote = other == null && pawn == null ? GaNet.FromHit(hit.collider) : null;
            if (remote != null && remote.Ally == Friendly)
                return;
            _nextShot = Time.time + GaClass.ShotCd(Kit);
            if (!GaPerfFrameGate.SkipPolish())
            {
                GaFx.Tracer(origin, hit.point, Friendly
                    ? new Color(0.3f, 1f, 0.75f, 1f)
                    : new Color(1f, 0.45f, 0.12f, 1f));
            }
            float dmg = GaClass.ShotDmg(Kit);
            if (other != null && other.Friendly != Friendly)
            {
                other.ApplyDamage(dmg, 0f, 0f, 3f);
                if (other.Hp <= 0f || other.Downed)
                    GaRadio.SayKill(this);
                return;
            }
            if (!Friendly && pawn != null)
            {
                pawn.ApplyDamage(dmg, 0f, 0f, 3f);
                return;
            }
            if (remote != null)
            {
                remote.ApplyDamage(dmg, 0f, 0f, 3f);
                return;
            }
            if (Kit == GaKitId.AntiTank || Kit == GaKitId.AntiAir || Kit == GaKitId.Engineer)
                HurtStruckUnit(hit.collider, dmg);
        }

        private void HurtStruckUnit(Collider col, float dmg)
        {
            if (col == null)
                return;
            Unit u = col.GetComponentInParent<Unit>();
            if (u == null || u.disabled)
                return;
            if (u is PilotDismounted)
                return;
            IDamageable d = u as IDamageable;
            if (d == null)
                d = u.GetComponent<IDamageable>();
            if (d == null)
                return;
            float blast = Kit == GaKitId.AntiTank ? dmg * 1.8f : dmg * 0.4f;
            try { d.ApplyDamage(dmg, blast, 0f, 6f); }
            catch { }
        }

        private bool Aim(out Vector3 aim)
        {
            float now = Time.unscaledTime;
            if (_haveAim && now < _aimUntil)
            {
                aim = _aimCache;
                return true;
            }
            aim = Vector3.zero;
            bool ok = false;
            if (Kit == GaKitId.AntiAir && AimAircraft(out aim))
                ok = true;
            else if (Kit == GaKitId.AntiTank && AimVehicle(out aim))
                ok = true;
            if (!ok)
            {
                Vector3 threat;
                if (ThreatPos(out threat))
                {
                    aim = threat + Vector3.up * 1.2f;
                    ok = true;
                }
            }
            if (!ok)
                return false;
            _aimCache = aim;
            _aimUntil = now + 0.18f;
            _haveAim = true;
            return true;
        }

        private bool AimAircraft(out Vector3 aim)
        {
            aim = Vector3.zero;
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return false;
            float best = 110f * 110f;
            bool hit = false;
            Vector3 at = transform.position;
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i] as Aircraft;
                if (ac == null || ac.disabled)
                    continue;
                float d = (ac.transform.position - at).sqrMagnitude;
                if (d < best && d > 36f)
                {
                    best = d;
                    aim = ac.transform.position;
                    hit = true;
                }
            }
            return hit;
        }

        private bool AimVehicle(out Vector3 aim)
        {
            aim = Vector3.zero;
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return false;
            float best = 70f * 70f;
            bool hit = false;
            Vector3 at = transform.position;
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i];
                if (u == null || u.disabled)
                    continue;
                GroundVehicle gv = u as GroundVehicle;
                Building b = gv == null ? u as Building : null;
                if (gv == null && b == null)
                    continue;
                float d = (u.transform.position - at).sqrMagnitude;
                if (d < best && d > 16f)
                {
                    best = d;
                    aim = u.transform.position + Vector3.up * 1.4f;
                    hit = true;
                }
            }
            return hit;
        }

        public void TakeDamage(float pierceDamage, float blastDamage, float amountAffected, float fireDamage, float ImpactDamage, PersistentID dealerID)
        {
            ApplyDamage(pierceDamage, blastDamage, fireDamage, ImpactDamage);
        }

        public void ApplyDamage(float pierceDamage, float blastDamage, float fireDamage, float impactDamage)
        {
            if (Downed)
                return;
            if (Puppet)
            {
                GaNet.ReportBotHit(NetId, pierceDamage, blastDamage, impactDamage);
                return;
            }
            HurtLocal(pierceDamage, blastDamage, impactDamage);
        }

        internal void HurtLocal(float pierceDamage, float blastDamage, float impactDamage)
        {
            if (Downed)
                return;
            float d = pierceDamage * 0.35f + blastDamage * 0.12f + impactDamage * 0.08f;
            if (d < 4f)
                d = 4f;
            Hp -= d;
            if (Friendly && Hp > 0f && Hp < 28f && Time.unscaledTime >= _nextRadio)
            {
                _nextRadio = Time.unscaledTime + 7f;
                GaRadio.SayMedic(this);
            }
            if (Hp <= 0f)
                Kill();
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

        private static string MakeCall(bool friendly, int slot, GaKitId kit)
        {
            int ki = (int)kit;
            if (ki < 0 || ki >= KitTag.Length)
                ki = 0;
            string who = friendly ? Tag[slot & 7] : "HOSTILE";
            return who + "-" + KitTag[ki];
        }

        private void Kill()
        {
            Hp = 0f;
            if (_canopy != null)
            {
                UnityEngine.Object.Destroy(_canopy);
                _canopy = null;
            }
            string killer = HurtByPlayer ? GaRadio.PlayerCall() : "INFANTRY";
            GaFps.KillFeed(killer, Callsign);
            if (Friendly)
                GaRadio.SquadDown(this);
            else if (HurtByPlayer)
                GaRadio.ConfirmKill();
            Downed = true;
            _downUntil = Time.unscaledTime + 22f;
            ClearRide();
            GaBody.Tick(transform, 0f, true);
            ShowGun(false);
        }

        private void OnDestroy()
        {
            GaPhys.Unregister(_cc);
            if (_canopy != null)
                UnityEngine.Object.Destroy(_canopy);
            if (_held != null)
                UnityEngine.Object.Destroy(_held);
        }
    }
}
