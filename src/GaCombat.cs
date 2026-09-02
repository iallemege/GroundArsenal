using System;
using System.Collections.Generic;
using UnityEngine;

namespace GroundArsenal
{
    internal static class GaCombat
    {
        internal const string PrefHotbar = "GroundArsenal.Hotbar";
        internal const string PrefCarry = "GroundArsenal.Carry";

        internal static int[] Hotbar = DefaultHotbar();
        internal static int Slot;
        internal static int[] Mag = new int[GaCatalog.Count];
        internal static int[] Pool = new int[GaCatalog.CalCount];
        internal static float NextShotAt;
        internal static float ReloadUntil;
        internal static float Ads;
        internal static float Zoom;
        internal static string Status = "";
        internal static float FlashUntil;
        internal static bool AssistOn;
        internal static Vector3 AssistWorld;
        internal static Unit LockTgt;
        internal static float LockAmt;
        private static int _reloadId = -1;
        private static int _reloadTake;
        private static int _burstLeft;
        private static int _burstSlot = -1;
        private static float _bloom;
        private static int _tracerN;
        private static int _lockGun = -1;

        private class Bolt
        {
            internal GameObject Go;
            internal Vector3 V;
            internal float Life;
            internal float Dmg;
            internal float Pen;
            internal Color Col;
            internal GaGun Gun;
            internal bool Rocket;
            internal float Drop;
            internal float Flown;
            internal GameObject PathGo;
            internal bool Homing;
            internal Unit Target;
            internal float Turn;
        }

        private static readonly List<Bolt> Bolts = new List<Bolt>(32);

        internal static GaGun Current
        {
            get { return GaCatalog.Get(Hotbar[Slot]); }
        }

        internal static int[] DefaultHotbar()
        {
            return new int[]
            {
                GaCatalog.FindBody("rev_body.obj"),
                GaCatalog.FindBody("pistol_body.obj"),
                GaCatalog.FindBody("q_SubmachineGun_1.obj"),
                GaCatalog.FindBody("q_Shotgun_1.obj"),
                GaCatalog.FindBody("q_M16.obj"),
                GaCatalog.FindBody("q_AK47.obj"),
                GaCatalog.FindBody("q_SniperRifle_1.obj"),
                GaCatalog.FindBody("q_RPG7.obj"),
                GaCatalog.FindBody("at4.obj")
            };
        }

        internal static void LoadPrefs()
        {
            try
            {
                string s = PlayerPrefs.GetString(PrefHotbar, "");
                if (!string.IsNullOrEmpty(s))
                {
                    string[] p = s.Split(',');
                    for (int i = 0; i < Hotbar.Length && i < p.Length; i++)
                    {
                        int id;
                        if (int.TryParse(p[i], out id) && id >= 0 && id < GaCatalog.Count)
                            Hotbar[i] = id;
                    }
                }
            }
            catch { }
        }

        internal static void SavePrefs()
        {
            try
            {
                string s = "";
                for (int i = 0; i < Hotbar.Length; i++)
                {
                    if (i > 0) s += ",";
                    s += Hotbar[i].ToString();
                }
                PlayerPrefs.SetString(PrefHotbar, s);
                PlayerPrefs.SetInt(PrefCarry, Hotbar[0]);
                PlayerPrefs.Save();
            }
            catch { }
        }

        internal static int LoadCarry()
        {
            int id = GaCatalog.FindBody("pistol_body.obj");
            try { id = PlayerPrefs.GetInt(PrefCarry, id); }
            catch { }
            if (id < 0 || id >= GaCatalog.Count || !UnlockedId(id))
                id = FirstUnlocked();
            return id;
        }

        internal static float KitCostM()
        {
            bool[] seen = new bool[GaCatalog.Count];
            float sum = 0f;
            for (int i = 0; i < Hotbar.Length; i++)
            {
                int id = Hotbar[i];
                if (id < 0 || id >= GaCatalog.Count || seen[id])
                    continue;
                seen[id] = true;
                sum += GaCatalog.CostM(GaCatalog.Get(id));
            }
            return sum;
        }

        internal static float CarryCostM()
        {
            return GaCatalog.CostM(GaCatalog.Get(LoadCarry()));
        }

        internal static void ApplyHangarKit()
        {
            LoadPrefs();
            SanitizeHotbar();
            FillAmmo(true);
            Slot = 0;
        }

        internal static void ApplyCarryOnly()
        {
            LoadPrefs();
            int carry = LoadCarry();
            if (!UnlockedId(carry))
                carry = FirstUnlocked();
            Hotbar[0] = carry;
            for (int i = 1; i < Hotbar.Length; i++)
                Hotbar[i] = carry;
            FillAmmo(false);
            Slot = 0;
        }

        internal static void SanitizeHotbar()
        {
            int fallback = FirstUnlocked();
            for (int i = 0; i < Hotbar.Length; i++)
            {
                if (!UnlockedId(Hotbar[i]))
                    Hotbar[i] = fallback;
            }
        }

        internal static int FirstUnlocked()
        {
            int pistol = GaCatalog.FindBody("pistol_body.obj");
            if (UnlockedId(pistol))
                return pistol;
            for (int i = 0; i < GaCatalog.Count; i++)
            {
                if (UnlockedId(i))
                    return i;
            }
            return 0;
        }

        internal static bool UnlockedId(int id)
        {
            return Unlocked(GaCatalog.Get(id));
        }

        internal static bool Unlocked(GaGun g)
        {
            if (g == null)
                return false;
            return g.Rank <= PlayerRank();
        }

        internal static int PlayerRank()
        {
            int r = 0;
            try
            {
                NuclearOption.Networking.Player p = null;
                if (GameManager.GetLocalPlayer(out p) && p != null)
                    r = p.PlayerRank;
            }
            catch { }
            if (r < 0)
                r = 0;
            return r;
        }

        private static void FillAmmo(bool allSlots)
        {
            for (int i = 0; i < GaCatalog.Count; i++)
                Mag[i] = 0;
            for (int i = 0; i < GaCatalog.CalCount; i++)
                Pool[i] = 0;
            bool[] seen = new bool[GaCatalog.Count];
            int n = allSlots ? Hotbar.Length : 1;
            for (int i = 0; i < n; i++)
            {
                GaGun g = GaCatalog.Get(Hotbar[i]);
                if (g == null || seen[g.Id])
                    continue;
                seen[g.Id] = true;
                Mag[g.Id] = g.Mag;
                AddPool(g.CalId, g.Reserve);
            }
        }

        internal static int PoolOf(GaGun g)
        {
            if (g == null)
                return 0;
            int c = g.CalId;
            if (c < 0 || c >= Pool.Length)
                return 0;
            return Pool[c];
        }

        internal static int AddPool(int cal, int amt)
        {
            if (cal < 0 || cal >= Pool.Length || amt == 0)
                return 0;
            int cap = GaCatalog.MagSizeForCal(cal) * 12;
            if (cal == 7 || cal == 8 || cal == 9)
                cap = 8;
            int before = Pool[cal];
            int next = before + amt;
            if (next < 0)
                next = 0;
            if (next > cap)
                next = cap;
            Pool[cal] = next;
            return next - before;
        }

        internal static void GiveGunAmmo(GaGun g)
        {
            if (g == null)
                return;
            Mag[g.Id] = g.Mag;
            int have = PoolOf(g);
            if (have < g.Reserve)
                AddPool(g.CalId, g.Reserve - have);
        }

        internal static int MagState(GaGun g)
        {
            if (g == null || g.Mag <= 0)
                return 0;
            int n = Mag[g.Id];
            if (n <= 0)
                return 0;
            if (n * 2 <= g.Mag)
                return 1;
            return 2;
        }

        internal static void Tick(Transform eye, float dt)
        {
            if (eye == null)
                return;
            TickBolts(dt);
            GaView.Tick();
            if (GaHud.BlocksLook)
                return;
            TickHipAssist(eye, dt);
            WheelAndKeys();
            if (_burstSlot != Slot)
            {
                _burstLeft = 0;
                _burstSlot = Slot;
            }
            _bloom = Mathf.MoveTowards(_bloom, 0f, dt * (Ads > 0.5f ? 1.8f : 1.05f));
            bool adsWant = CanAds() && Input.GetMouseButton(1);
            Ads = Mathf.MoveTowards(Ads, adsWant ? 1f : 0f, dt * 7f);
            PollZoom(adsWant);
            TickLock(eye, dt);
            if (Time.time < ReloadUntil)
            {
                Status = GaMode.Zh ? "装填" : "RELOAD";
                _burstLeft = 0;
                return;
            }
            FinishReload();
            GaGun g = Current;
            if (g == null)
                return;
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartReload(g);
                return;
            }
            bool fire = false;
            if (g.Fire == 2)
            {
                if (Input.GetMouseButtonDown(0) && _burstLeft <= 0)
                    _burstLeft = 3;
                fire = _burstLeft > 0;
            }
            else if (g.Fire == 1 || g.Fire == 5)
                fire = Input.GetMouseButton(0);
            else
                fire = Input.GetMouseButtonDown(0);
            if (!fire || Time.time < NextShotAt)
                return;
            if (Mag[g.Id] <= 0)
            {
                StartReload(g);
                return;
            }
            if (IsRbs(g) && !LockReady)
            {
                Status = "NO LOCK";
                return;
            }
            bool tap = Input.GetMouseButtonDown(0);
            Shoot(eye, g, tap);
            if (g.Fire == 2 && _burstLeft > 0)
                _burstLeft--;
            GaView.PlayFire();
            Vector3 muzzle = MuzzlePos(eye);
            float flashScale = 1f;
            bool wideFlash = false;
            if (g.Fire == 3 || g.Family == 5)
            {
                flashScale = 1.4f;
                wideFlash = true;
            }
            else if (g.Family == 0)
                flashScale = 0.62f;
            else if (g.Family == 8 || g.Family == 10)
                flashScale = 1.35f;
            else if (g.Fire == 4)
            {
                flashScale = 1.75f;
                wideFlash = true;
            }
            GaFx.Muzzle(muzzle, eye.forward, flashScale, wideFlash);
            FlashUntil = Time.time + 0.048f;
        }

        internal static Vector3 MuzzlePos(Transform eye)
        {
            if (eye == null)
                return Vector3.zero;
            if (Ads >= 0.45f)
                return eye.position + eye.forward * 0.62f;
            return eye.position + eye.forward * 0.48f + eye.right * 0.14f - eye.up * 0.08f;
        }

        internal static float BulletSpeed(GaGun g)
        {
            if (g == null)
                return 700f;
            if (g.Vel > 20f)
                return g.Vel;
            return BoltSpeed(g);
        }

        internal static float HitRange(GaGun g)
        {
            if (g == null)
                return 800f;
            if (g.Fire == 4)
            {
                if (g.Body == "rbs70.obj")
                    return 10000f;
                if (g.Body == "at4.obj")
                    return 900f;
                return 2200f;
            }
            if (g.Family == 0)
                return 850f;
            if (g.Family == 1)
                return 700f;
            if (g.Family == 2)
                return 550f;
            if (g.Family == 3)
                return 1200f;
            if (g.Family == 4 || g.Family == 5)
                return 220f;
            if (g.Family == 6)
                return 2400f;
            if (g.Family == 7)
                return 3200f;
            if (g.Family == 8)
                return 4000f;
            if (g.Family == 9)
                return 4500f;
            if (g.Family == 10)
                return 5500f;
            if (g.Family == 11)
                return 2800f;
            if (g.Family == 12)
                return 450f;
            if (g.Family == 15)
                return 3000f;
            return 2000f;
        }

        internal static float BoltSpeed(GaGun g)
        {
            if (g == null)
                return 140f;
            if (g.Body == "at4.obj")
                return 220f;
            if (g.Body == "rbs70.obj")
                return 680f;
            if (g.Body == "q_RPG7.obj")
                return 185f;
            return 140f;
        }

        internal static bool CanAds()
        {
            GaGun g = Current;
            if (g == null)
                return false;
            if (g.Fire == 4)
                return false;
            if (g.Family == 13 || g.Family == 14)
                return false;
            if (g.Body == "at4.obj" || g.Body == "q_RPG7.obj" || g.Body == "rbs70.obj")
                return false;
            return true;
        }

        internal static bool IsRpg(GaGun g)
        {
            return g != null && g.Body == "q_RPG7.obj";
        }

        internal static bool IsRbs(GaGun g)
        {
            return g != null && g.Body == "rbs70.obj";
        }

        internal static bool LockReady
        {
            get
            {
                if (LockTgt == null || LockTgt.disabled)
                    return false;
                return LockAmt >= 0.98f;
            }
        }

        internal static bool IsSniper()
        {
            GaGun g = Current;
            return g != null && g.Family == 10;
        }

        internal static bool IsG11()
        {
            GaGun g = Current;
            return g != null && g.Body == "g11.obj";
        }

        internal static float FovMul()
        {
            float tight = 0.42f;
            if (IsSniper())
                tight = Mathf.Lerp(0.16f, 0.038f, Zoom);
            else if (IsG11())
                tight = Mathf.Lerp(0.38f, 0.18f, Zoom);
            else if (Current != null && Current.Family <= 1)
                tight = Mathf.Lerp(0.72f, 0.42f, Zoom);
            else
                tight = Mathf.Lerp(0.40f, 0.16f, Zoom);
            return Mathf.Lerp(1f, tight, Ads);
        }

        internal static float LookSensMul()
        {
            if (Ads < 0.04f)
                return 1f;
            float z = 1f - Zoom * 0.72f;
            if (z < 0.18f)
                z = 0.18f;
            return Mathf.Lerp(1f, z * 0.42f, Ads);
        }

        internal static float ZoomPower()
        {
            float m = FovMul();
            if (m < 0.02f)
                m = 0.02f;
            return 1f / m;
        }

        private static void PollZoom(bool adsHeld)
        {
            if (!CanAds() || GaHud.BlocksLook)
                return;
            if (!adsHeld && Ads < 0.2f)
                return;
            float wh = Input.GetAxis("Mouse ScrollWheel");
            if (wh > 0.01f)
                Zoom = Mathf.Clamp01(Zoom + 0.11f);
            else if (wh < -0.01f)
                Zoom = Mathf.Clamp01(Zoom - 0.11f);
        }

        private static void WheelAndKeys()
        {
            bool adsHeld = CanAds() && Input.GetMouseButton(1);
            float w = Input.GetAxis("Mouse ScrollWheel");
            if (!adsHeld)
            {
                if (w > 0.01f)
                    Slot = (Slot + 1) % Hotbar.Length;
                else if (w < -0.01f)
                    Slot = (Slot + Hotbar.Length - 1) % Hotbar.Length;
            }
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    Slot = i;
            }
            GaView.ShowCurrent();
        }

        private static void StartReload(GaGun g)
        {
            if (g == null)
                return;
            if (PoolOf(g) <= 0 || Mag[g.Id] >= g.Mag)
                return;
            int need = g.Mag - Mag[g.Id];
            int take = need;
            int pool = PoolOf(g);
            if (take > pool)
                take = pool;
            if (take <= 0)
                return;
            _reloadId = g.Id;
            _reloadTake = take;
            ReloadUntil = Time.time + (g.Fire == 4 ? 2.4f : 1.7f);
            GaView.PlayReload(g);
        }

        private static void FinishReload()
        {
            if (_reloadId < 0)
                return;
            GaGun g = GaCatalog.Get(_reloadId);
            Mag[_reloadId] += _reloadTake;
            if (g != null)
                AddPool(g.CalId, -_reloadTake);
            _reloadId = -1;
            _reloadTake = 0;
        }

        private static void Shoot(Transform eye, GaGun g, bool tap)
        {
            Mag[g.Id]--;
            float interval = 60f / Mathf.Max(30f, g.Rpm);
            NextShotAt = Time.time + interval;
            int pellets = (g.Fire == 3 || g.Family == 5) ? 8 : 1;
            bool spray = g.Fire == 3 || g.Family == 5;
            float bloomAdd = 0.10f;
            if (g.Family == 3)
                bloomAdd = 0.16f;
            else if (g.Family == 4 || g.Family == 5)
                bloomAdd = 0.07f;
            else if (g.Family == 10)
                bloomAdd = 0.45f;
            else if (g.Family == 0)
                bloomAdd = 0.28f;
            else if (g.Family == 1 || g.Family == 2)
                bloomAdd = 0.20f;
            _bloom = Mathf.Min(1f, _bloom + bloomAdd);
            float spread = (g.Spread + _bloom * g.Spread * 2.4f) * (1f - Ads * 0.72f);
            if (GaMode.Sprinting)
                spread *= 1.7f;
            else if (GaMode.MoveSpeed > 1.2f)
                spread *= 1.22f;
            if (tap && !spray)
                spread = 0f;
            Vector3 origin = MuzzlePos(eye);
            float spd = BulletSpeed(g);
            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = eye.forward;
                if (spread > 0.0004f)
                {
                    dir += eye.right * UnityEngine.Random.Range(-spread, spread);
                    dir += eye.up * UnityEngine.Random.Range(-spread, spread);
                    dir.Normalize();
                }
                if (g.Fire == 4)
                    SpawnBolt(origin + dir * 0.55f, dir * spd, g);
                else
                    SpawnSlug(origin + dir * 0.12f, dir, spd, g);
            }
            Status = Mag[g.Id].ToString() + " / " + PoolOf(g).ToString();
        }

        private static void SpawnSlug(Vector3 pos, Vector3 dir, float speed, GaGun g)
        {
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();
            GameObject go = new GameObject("GaSlug");
            go.layer = 0;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(dir);
            _tracerN++;
            bool bright = (g != null && g.Family == 10)
                || (g != null && (g.Fire == 3 || g.Family == 5))
                || Ads >= 0.28f
                || (_tracerN % 2) == 0;
            float len = 2.2f;
            if (g != null && g.Family == 10)
                len = 6.5f;
            else if (Ads >= 0.28f)
                len = 5.8f;
            else if (g != null && (g.Fire == 3 || g.Family == 5))
                len = 1.4f;
            else if (bright)
                len = 3.6f;
            Color col = g != null ? g.Tracer : new Color(1f, 0.82f, 0.35f);
            GaFx.DressSlug(go, col, len, bright);
            Bolt b = new Bolt();
            b.Go = go;
            b.V = dir * speed;
            float reach = HitRange(g);
            if (speed < 20f)
                speed = 20f;
            b.Life = reach / speed + 0.35f;
            if (b.Life > 8f)
                b.Life = 8f;
            if (b.Life < 0.4f)
                b.Life = 0.4f;
            b.Dmg = g != null ? g.Damage : 12f;
            b.Pen = g != null ? g.Pen : 3f;
            b.Col = col;
            b.Gun = g;
            b.Rocket = false;
            b.Drop = g != null ? g.Drop : 0.7f;
            b.Flown = 0f;
            b.PathGo = GaFx.MakePath(pos, col, bright || Ads >= 0.28f);
            if (GaConfig.WantTracers() || Ads >= 0.28f)
                GaFx.Tracer(pos, pos + dir * (bright ? 36f : 22f), col);
            Bolts.Add(b);
        }

        private static void SpawnBolt(Vector3 p, Vector3 v, GaGun g)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "GaRocket";
            Vector3 dir = v.sqrMagnitude > 0.01f ? v.normalized : Vector3.forward;
            go.transform.position = p;
            go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(0.045f, 0.18f, 0.045f);
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                UnityEngine.Object.Destroy(col);
            GaFx.DressBolt(go, g.Tracer);
            Bolt b = new Bolt();
            b.Go = go;
            b.V = v;
            float spd = v.magnitude;
            float life = 4f;
            if (spd > 1f)
            {
                float reach = HitRange(g) / spd + 2.2f;
                if (g != null && g.Body == "rbs70.obj")
                {
                    if (reach > 18f)
                        reach = 18f;
                }
                else if (reach > 12f)
                    reach = 12f;
                if (reach < 3f)
                    reach = 3f;
                life = reach;
            }
            b.Life = life;
            b.Dmg = g.Damage;
            b.Pen = g.Pen;
            b.Col = g.Tracer;
            b.Gun = g;
            b.Rocket = true;
            b.Drop = g != null ? g.Drop : 0.25f;
            b.Flown = 0f;
            b.PathGo = GaFx.MakePath(p, g != null ? g.Tracer : new Color(1f, 0.55f, 0.12f), true);
            if (LockReady && (IsRpg(g) || IsRbs(g)))
            {
                b.Homing = true;
                b.Target = LockTgt;
                b.Turn = IsRbs(g) ? 18f : 6.2f;
            }
            Bolts.Add(b);
        }

        private static void TickBolts(float dt)
        {
            for (int i = Bolts.Count - 1; i >= 0; i--)
            {
                Bolt b = Bolts[i];
                if (b.Go == null)
                {
                    KillBolt(b);
                    Bolts.RemoveAt(i);
                    continue;
                }
                SteerBolt(b, dt);
                float drop = b.Drop > 0.01f ? b.Drop : 0f;
                if (drop > 0.001f)
                    b.V += Physics.gravity * drop * dt;
                Vector3 p = b.Go.transform.position;
                Vector3 np = p + b.V * dt;
                if (Proximate(b, np))
                {
                    GaGun g = b.Gun != null ? b.Gun : Current;
                    Vector3 incoming = b.V.sqrMagnitude > 0.01f ? b.V.normalized : Vector3.forward;
                    HurtUnit(b.Target, g, incoming);
                    GaFx.Spark(np, -incoming, b.Col);
                    if (b.Rocket)
                        GaFx.Blast(np);
                    KillBolt(b);
                    Bolts.RemoveAt(i);
                    continue;
                }
                RaycastHit hit;
                if (WeaponLine(p, np, out hit))
                {
                    GaGun g = b.Gun != null ? b.Gun : Current;
                    float flown = b.Flown + hit.distance;
                    HurtHit(hit, g, b.V.sqrMagnitude > 0.01f ? b.V.normalized : Vector3.forward, flown);
                    GaFx.Spark(hit.point, hit.normal, b.Col);
                    if (b.Rocket)
                        GaFx.Blast(hit.point);
                    KillBolt(b);
                    Bolts.RemoveAt(i);
                    continue;
                }
                b.Flown += (np - p).magnitude;
                b.Go.transform.position = np;
                GaFx.PushPath(b.PathGo, np);
                if (b.V.sqrMagnitude > 0.01f)
                {
                    Quaternion look = Quaternion.LookRotation(b.V.normalized);
                    b.Go.transform.rotation = b.Rocket
                        ? look * Quaternion.Euler(90f, 0f, 0f)
                        : look;
                }
                b.Life -= dt;
                if (b.Life <= 0f)
                {
                    if (b.Rocket)
                        GaFx.Blast(np);
                    KillBolt(b);
                    Bolts.RemoveAt(i);
                }
            }
        }

        internal static int PenGrade(GaGun g)
        {
            if (g == null)
                return 4;
            return GaArmorMath.ClampGrade(Mathf.RoundToInt(g.Pen));
        }

        internal static bool IsPureKe(GaGun g)
        {
            if (g == null)
                return true;
            return g.Kind == "KE";
        }

        internal static float Kinetic(GaGun g)
        {
            if (g == null)
                return 10f;
            return g.Damage;
        }

        internal static float PierceHp(GaGun g)
        {
            if (g == null)
                return 4f;
            if (g.Kind == "HEAT")
                return 40f + g.Pen * 3.1f;
            if (g.Kind == "HE")
                return 20f + g.Pen * 2.2f;
            return 2.0f + g.Pen * 0.95f;
        }

        internal static float BlastHp(GaGun g)
        {
            if (g == null)
                return 0f;
            if (g.Body == "at4.obj")
                return 140f;
            if (g.Body == "q_RPG7.obj")
                return 110f;
            if (g.Body == "rbs70.obj")
                return 40f;
            if (g.Kind == "HEAT")
                return 110f;
            if (g.Kind == "HE")
                return 40f;
            return 0f;
        }

        private static void ScaleVsTarget(ref float pierce, ref float blast, ref float impact, GaGun g, Transform hitXf)
        {
            if (hitXf == null)
                return;
            bool air = false;
            bool building = false;
            bool vehicle = false;
            try
            {
                if (hitXf.GetComponentInParent<GaPawn>() != null
                    || hitXf.GetComponentInParent<PilotDismounted>() != null)
                    return;
                else if (hitXf.GetComponentInParent<Aircraft>() != null)
                    air = true;
                else if (hitXf.GetComponentInParent<Building>() != null
                    || hitXf.GetComponentInParent<MapBuilding>() != null)
                    building = true;
                else if (hitXf.GetComponentInParent<GroundVehicle>() != null
                    || hitXf.GetComponentInParent<Ship>() != null)
                    vehicle = true;
            }
            catch { }
            float mul = 1f;
            if (IsPureKe(g))
            {
                if (vehicle)
                    mul = 0.03f;
                else if (air)
                    mul = 0.015f;
            }
            else if (g != null && g.Kind == "HE")
            {
                if (building)
                    mul = 0.22f;
                else if (vehicle)
                    mul = 0.35f;
                else if (air)
                    mul = 0.8f;
            }
            else
            {
                if (building)
                    mul = 0.38f;
                else if (vehicle)
                    mul = 0.62f;
                else if (air)
                    mul = 0.4f;
            }
            pierce *= mul;
            blast *= mul;
            impact *= mul;
        }

        private static float Falloff(float dist, GaGun g)
        {
            if (g == null)
                return 1f;
            if (g.Fire == 4)
                return 1f;
            float full = g.Range;
            if (full < 16f)
                full = 16f;
            if (dist <= full)
                return 1f;
            float maxR = HitRange(g);
            float span = maxR - full;
            if (span < 1f)
                span = 1f;
            float u = (dist - full) / span;
            if (u < 0f)
                u = 0f;
            if (u > 1f)
                u = 1f;
            return Mathf.Lerp(1f, 0.20f, u);
        }

        private static void KillBolt(Bolt b)
        {
            if (b == null)
                return;
            if (b.Go != null)
                UnityEngine.Object.Destroy(b.Go);
            if (b.PathGo != null)
                UnityEngine.Object.Destroy(b.PathGo);
            b.Go = null;
            b.PathGo = null;
        }

        internal static void ClearLock()
        {
            LockTgt = null;
            LockAmt = 0f;
        }

        private static void TickLock(Transform eye, float dt)
        {
            GaGun g = Current;
            if (g == null || (!IsRpg(g) && !IsRbs(g)))
            {
                ClearLock();
                _lockGun = g != null ? g.Id : -1;
                return;
            }
            if (_lockGun != g.Id)
            {
                ClearLock();
                _lockGun = g.Id;
            }
            Unit best = FindLockTarget(eye, g);
            if (best == null || best.disabled)
            {
                LockAmt = Mathf.MoveTowards(LockAmt, 0f, dt * 0.85f);
                if (LockAmt < 0.08f)
                    LockTgt = null;
                return;
            }
            if (!object.ReferenceEquals(best, LockTgt))
            {
                LockTgt = best;
                LockAmt = 0f;
            }
            bool hold = Input.GetMouseButton(1);
            float fill = IsRbs(g) ? 0.55f : 0.38f;
            if (hold)
                LockAmt = Mathf.MoveTowards(LockAmt, 1f, dt / fill);
            else
                LockAmt = Mathf.MoveTowards(LockAmt, 0f, dt * 0.42f);
        }

        private static Unit FindLockTarget(Transform eye, GaGun g)
        {
            if (eye == null)
                return null;
            Unit best = null;
            float cone = IsRbs(g) ? 32f : 28f;
            float bestAng = cone;
            float minD = IsRbs(g) ? 12f : 8f;
            float maxD = IsRbs(g) ? 12000f : 2500f;
            FactionHQ hq = null;
            try { GameManager.GetLocalHQ(out hq); }
            catch { }
            Unit linked = GaMode.LinkedUnit();
            List<Unit> all = null;
            try { all = UnitRegistry.allUnits; }
            catch { }
            if (all == null)
                return null;
            Vector3 origin = eye.position;
            Vector3 fwd = eye.forward;
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i];
                if (u == null || u.disabled)
                    continue;
                if (linked != null && object.ReferenceEquals(u, linked))
                    continue;
                if (IsFriendly(u, hq))
                    continue;
                bool rpg = IsRpg(g);
                bool rbs = IsRbs(g);
                bool air = u is Aircraft;
                bool ground = (u is GroundVehicle) || (u is Ship);
                if (rpg)
                {
                    if (!ground && !air)
                        continue;
                    if (air && UnitSpeed(u) > 18f)
                        continue;
                }
                else if (rbs)
                {
                    if (!air)
                        continue;
                }
                else
                    continue;
                Vector3 aim = AimPoint(u);
                Vector3 to = aim - origin;
                float dist = to.magnitude;
                if (dist < minD || dist > maxD)
                    continue;
                float ang = Vector3.Angle(fwd, to);
                if (ang < bestAng)
                {
                    bestAng = ang;
                    best = u;
                }
            }
            return best;
        }

        private static float UnitSpeed(Unit u)
        {
            Vector3 v = UnitVel(u);
            return v.magnitude;
        }

        private static Vector3 UnitVel(Unit u)
        {
            if (u == null)
                return Vector3.zero;
            try
            {
                if (u.rb != null)
                    return u.rb.velocity;
            }
            catch { }
            return Vector3.zero;
        }

        private static void SteerBolt(Bolt b, float dt)
        {
            if (b == null || !b.Homing)
                return;
            if (b.Target == null || b.Target.disabled)
            {
                b.Homing = false;
                return;
            }
            if (b.Go == null)
                return;
            Vector3 pos = b.Go.transform.position;
            Vector3 aim = AimPoint(b.Target) + UnitVel(b.Target) * LeadTime(b, pos);
            Vector3 to = aim - pos;
            if (to.sqrMagnitude < 0.01f)
                return;
            float spd = b.V.magnitude;
            if (IsRbs(b.Gun) && spd < 720f)
                spd = Mathf.MoveTowards(spd, 720f, 220f * dt);
            float turn = b.Turn > 0.1f ? b.Turn : 6f;
            b.V = Vector3.RotateTowards(b.V, to.normalized * spd, turn * dt, 420f * dt);
        }

        private static float LeadTime(Bolt b, Vector3 pos)
        {
            if (b == null || b.Target == null)
                return 0f;
            float spd = b.V.magnitude;
            if (spd < 40f)
                spd = 40f;
            float dist = (AimPoint(b.Target) - pos).magnitude;
            float t = dist / spd;
            if (t > 4f)
                t = 4f;
            return t;
        }

        private static bool Proximate(Bolt b, Vector3 np)
        {
            if (b == null || !b.Homing || b.Target == null || b.Target.disabled)
                return false;
            float near = IsRbs(b.Gun) ? 16f : 5.5f;
            Vector3 aim = AimPoint(b.Target);
            return (np - aim).sqrMagnitude <= near * near;
        }

        private static void HurtUnit(Unit u, GaGun g, Vector3 incoming)
        {
            if (u == null || g == null)
                return;
            float pierce = PierceHp(g);
            float blast = BlastHp(g);
            float impact = Kinetic(g);
            Transform xf = null;
            try { xf = u.transform; }
            catch { }
            ScaleVsTarget(ref pierce, ref blast, ref impact, g, xf);
            GaVehicleArmor.RememberInfantryShot(PenGrade(g), incoming, IsPureKe(g));
            PersistentID dealer = PersistentID.None;
            try
            {
                Aircraft ac;
                if (GameManager.GetLocalAircraft(out ac) && ac != null)
                    dealer = ac.persistentID;
            }
            catch { }
            UnitPart[] parts = null;
            try { parts = u.GetComponentsInChildren<UnitPart>(); }
            catch { }
            if (parts != null && parts.Length > 0 && parts[0] != null)
            {
                try { parts[0].TakeDamage(pierce, blast, 1f, 0f, impact, dealer); }
                catch { }
                return;
            }
            IDamageable dmg = null;
            try { dmg = u.GetComponentInParent<IDamageable>(); }
            catch { }
            if (dmg != null)
            {
                try { dmg.TakeDamage(pierce, blast, 1f, 0f, impact, dealer); }
                catch
                {
                    try { dmg.ApplyDamage(pierce, blast, 0f, impact); }
                    catch { }
                }
            }
        }

        private static void TickHipAssist(Transform eye, float dt)
        {
            AssistOn = false;
            if (eye == null)
                return;
            GaGun g = Current;
            if (g == null || !g.HipAssist)
                return;
            if (Ads >= 0.2f || GaMode.Sprinting || GaHud.BlocksLook)
                return;
            if (Time.time < ReloadUntil)
                return;
            Vector3 pred;
            if (!FindAssist(eye, g, out pred))
                return;
            AssistOn = true;
            AssistWorld = pred;
            float mx = Mathf.Abs(Input.GetAxis("Mouse X"));
            float my = Mathf.Abs(Input.GetAxis("Mouse Y"));
            if (mx + my > 1.6f)
                return;
            Vector3 to = pred - eye.position;
            if (to.sqrMagnitude < 0.25f)
                return;
            float ang = Vector3.Angle(eye.forward, to);
            if (ang > 7f)
                return;
            float t = 2.2f * dt * (1f - ang / 7f);
            if (t < 0.004f)
                return;
            GaMode.PullLook(to, t);
        }

        private static bool FindAssist(Transform eye, GaGun g, out Vector3 pred)
        {
            pred = Vector3.zero;
            if (eye == null)
                return false;
            Unit best = null;
            float bestScore = float.MaxValue;
            FactionHQ hq = null;
            try { GameManager.GetLocalHQ(out hq); }
            catch { }
            Unit linked = GaMode.LinkedUnit();
            List<Unit> all = null;
            try { all = UnitRegistry.allUnits; }
            catch { }
            if (all == null)
                return false;
            Vector3 origin = eye.position;
            Vector3 fwd = eye.forward;
            float spd = BulletSpeed(g);
            if (spd < 40f)
                spd = 40f;
            float drop = g != null ? g.Drop : 0.7f;
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i];
                if (u == null || u.disabled)
                    continue;
                if (linked != null && object.ReferenceEquals(u, linked))
                    continue;
                if (!(u is Aircraft) && !(u is GroundVehicle) && !(u is Ship))
                    continue;
                if (IsFriendly(u, hq))
                    continue;
                Vector3 aim = AimPoint(u);
                Vector3 to = aim - origin;
                float dist = to.magnitude;
                if (dist < 8f || dist > 280f)
                    continue;
                float ang = Vector3.Angle(fwd, to);
                if (ang > 7f)
                    continue;
                float score = dist + ang * 6f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = u;
                }
            }
            if (best == null)
                return false;
            Vector3 pos = AimPoint(best);
            Vector3 vel = Vector3.zero;
            try
            {
                if (best.rb != null)
                    vel = best.rb.velocity;
            }
            catch { }
            float dist2 = (pos - origin).magnitude;
            float t = dist2 / spd;
            if (t > 1.4f)
                t = 1.4f;
            pred = pos + vel * t + Vector3.up * (0.5f * 9.81f * drop * t * t);
            return true;
        }

        private static Vector3 AimPoint(Unit u)
        {
            if (u == null)
                return Vector3.zero;
            try
            {
                Vector3 com = u.GetCenterOfMass();
                if (com.sqrMagnitude > 0.01f)
                    return com;
            }
            catch { }
            return u.transform.position;
        }

        private static bool IsFriendly(Unit u, FactionHQ local)
        {
            if (u == null || local == null)
                return false;
            try
            {
                FactionHQ other = u.NetworkHQ;
                if (other == null)
                    return false;
                return object.ReferenceEquals(other, local);
            }
            catch
            {
                return false;
            }
        }

        private static void HurtHit(RaycastHit hit, GaGun g, Vector3 incoming, float flown)
        {
            if (g == null || hit.collider == null)
                return;
            Transform pawn = GaMode.PawnRoot;
            Transform t = hit.collider.transform;
            if (pawn != null && (t == pawn || t.IsChildOf(pawn)))
                return;
            float pierce = PierceHp(g);
            float blast = BlastHp(g);
            float impact = Kinetic(g);
            float fall = Falloff(flown, g);
            pierce *= fall;
            blast *= fall;
            impact *= fall;
            Transform xf = null;
            try { xf = hit.collider.transform; }
            catch { }
            ScaleVsTarget(ref pierce, ref blast, ref impact, g, xf);
            GaVehicleArmor.RememberInfantryShot(PenGrade(g), incoming, IsPureKe(g));
            PersistentID dealer = PersistentID.None;
            try
            {
                Aircraft ac;
                if (GameManager.GetLocalAircraft(out ac) && ac != null)
                    dealer = ac.persistentID;
            }
            catch { }
            UnitPart part = null;
            try { part = hit.collider.GetComponentInParent<UnitPart>(); }
            catch { }
            if (part != null)
            {
                try { part.TakeDamage(pierce, blast, 1f, 0f, impact, dealer); }
                catch { }
                return;
            }
            IDamageable dmg = null;
            try { dmg = hit.collider.GetComponentInParent<IDamageable>(); }
            catch { }
            if (dmg != null)
            {
                try { dmg.TakeDamage(pierce, blast, 1f, 0f, impact, dealer); }
                catch
                {
                    try { dmg.ApplyDamage(pierce, blast, 0f, impact); }
                    catch { }
                }
                return;
            }
            Unit u = null;
            try { u = hit.collider.GetComponentInParent<Unit>(); }
            catch { }
            if (u == null)
                return;
            UnitPart[] parts = null;
            try { parts = u.GetComponentsInChildren<UnitPart>(); }
            catch { }
            if (parts != null && parts.Length > 0 && parts[0] != null)
            {
                try { parts[0].TakeDamage(pierce, blast, 1f, 0f, impact, dealer); }
                catch { }
            }
        }

        private static bool WeaponRay(Vector3 origin, Vector3 dir, float range, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            if (range < 0.05f)
                return false;
            RaycastHit[] all = Physics.RaycastAll(origin, dir, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return PickHit(all, out hit);
        }

        private static bool WeaponLine(Vector3 a, Vector3 b, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            Vector3 d = b - a;
            float range = d.magnitude;
            if (range < 0.01f)
                return false;
            RaycastHit[] all = Physics.RaycastAll(a, d / range, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return PickHit(all, out hit);
        }

        private static bool PickHit(RaycastHit[] all, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            if (all == null || all.Length == 0)
                return false;
            Transform pawn = GaMode.PawnRoot;
            float best = float.MaxValue;
            bool ok = false;
            for (int i = 0; i < all.Length; i++)
            {
                Collider col = all[i].collider;
                if (col == null)
                    continue;
                Transform t = col.transform;
                if (pawn != null && (t == pawn || t.IsChildOf(pawn)))
                    continue;
                if (all[i].distance < 1.15f)
                {
                    Unit near = null;
                    try { near = col.GetComponentInParent<Unit>(); }
                    catch { }
                    IDamageable dmg = null;
                    try { dmg = col.GetComponentInParent<IDamageable>(); }
                    catch { }
                    if (near == null && dmg == null)
                        continue;
                }
                if (all[i].distance < best)
                {
                    best = all[i].distance;
                    hit = all[i];
                    ok = true;
                }
            }
            return ok;
        }

        internal static void ClearBolts()
        {
            for (int i = 0; i < Bolts.Count; i++)
                KillBolt(Bolts[i]);
            Bolts.Clear();
        }

        internal static void AttachViews(Transform eye)
        {
            Ads = 0f;
            Zoom = 0f;
            _bloom = 0f;
            _burstLeft = 0;
            AssistOn = false;
            ClearLock();
            GaView.Attach(eye);
        }

        internal static void DestroyViews()
        {
            GaView.Destroy();
        }

        internal static void RefreshView()
        {
            GaView.Refresh();
        }
    }
}
