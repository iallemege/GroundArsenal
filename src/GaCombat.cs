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
        internal static int[] Reserve = new int[GaCatalog.Count];
        internal static float NextShotAt;
        internal static float ReloadUntil;
        internal static float Ads;
        internal static string Status = "";
        private static int _reloadId = -1;
        private static int _reloadTake;

        private class Bolt
        {
            internal GameObject Go;
            internal Vector3 V;
            internal float Life;
            internal float Dmg;
            internal float Pen;
            internal Color Col;
            internal GaGun Gun;
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
            if (id < 0 || id >= GaCatalog.Count)
                id = 18;
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
            FillAmmo(true);
            Slot = 0;
        }

        internal static void ApplyCarryOnly()
        {
            LoadPrefs();
            int carry = LoadCarry();
            Hotbar[0] = carry;
            for (int i = 1; i < Hotbar.Length; i++)
                Hotbar[i] = carry;
            FillAmmo(false);
            Slot = 0;
        }

        private static void FillAmmo(bool allSlots)
        {
            for (int i = 0; i < GaCatalog.Count; i++)
            {
                Mag[i] = 0;
                Reserve[i] = 0;
            }
            int n = allSlots ? Hotbar.Length : 1;
            for (int i = 0; i < n; i++)
            {
                GaGun g = GaCatalog.Get(Hotbar[i]);
                Mag[g.Id] = g.Mag;
                Reserve[g.Id] = g.Reserve;
            }
        }

        internal static void Tick(Transform eye, float dt)
        {
            if (eye == null)
                return;
            TickBolts(dt);
            GaView.Tick();
            if (GaHud.BlocksLook)
                return;
            WheelAndKeys();
            float wantAds = Input.GetMouseButton(1) ? 1f : 0f;
            Ads = Mathf.MoveTowards(Ads, wantAds, dt * 6f);
            if (Time.time < ReloadUntil)
            {
                Status = GaMode.Zh ? "装填" : "RELOAD";
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
            if (g.Fire == 1 || g.Fire == 5)
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
            Shoot(eye, g);
            GaView.PlayFire();
            Vector3 muzzle = eye.position + eye.forward * 0.42f + eye.right * 0.14f - eye.up * 0.08f;
            GaFx.Muzzle(muzzle);
        }

        private static void WheelAndKeys()
        {
            float w = Input.GetAxis("Mouse ScrollWheel");
            if (w > 0.01f)
                Slot = (Slot + 1) % Hotbar.Length;
            else if (w < -0.01f)
                Slot = (Slot + Hotbar.Length - 1) % Hotbar.Length;
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
            if (Reserve[g.Id] <= 0 || Mag[g.Id] >= g.Mag)
                return;
            int need = g.Mag - Mag[g.Id];
            int take = need;
            if (take > Reserve[g.Id])
                take = Reserve[g.Id];
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
            Mag[_reloadId] += _reloadTake;
            Reserve[_reloadId] -= _reloadTake;
            if (Reserve[_reloadId] < 0)
                Reserve[_reloadId] = 0;
            _reloadId = -1;
            _reloadTake = 0;
        }

        private static void Shoot(Transform eye, GaGun g)
        {
            Mag[g.Id]--;
            float interval = 60f / Mathf.Max(30f, g.Rpm);
            NextShotAt = Time.time + interval;
            int pellets = g.Fire == 3 ? 8 : 1;
            float spread = g.Spread * (1f - Ads * 0.7f);
            Vector3 origin = eye.position + eye.forward * 0.35f;
            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = eye.forward;
                dir += eye.right * UnityEngine.Random.Range(-spread, spread);
                dir += eye.up * UnityEngine.Random.Range(-spread, spread);
                dir.Normalize();
                if (g.Fire == 4)
                    SpawnBolt(origin + dir * 0.7f, dir * 90f, g);
                else
                    Hitscan(origin, dir, g);
            }
            Status = Mag[g.Id].ToString() + " / " + Reserve[g.Id].ToString();
        }

        private static void Hitscan(Vector3 origin, Vector3 dir, GaGun g)
        {
            RaycastHit hit;
            float dist = g.Range > 10f ? g.Range : 80f;
            if (WeaponRay(origin, dir, dist, out hit))
            {
                HurtHit(hit, g, dir);
                GaFx.Spark(hit.point, hit.normal, g.Tracer);
                if (GaConfig.WantTracers())
                    GaFx.Tracer(origin, hit.point, g.Tracer);
            }
            else if (GaConfig.WantTracers())
                GaFx.Tracer(origin, origin + dir * dist, g.Tracer);
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
            b.Life = 4f;
            b.Dmg = g.Damage;
            b.Pen = g.Pen;
            b.Col = g.Tracer;
            b.Gun = g;
            Bolts.Add(b);
        }

        private static void TickBolts(float dt)
        {
            for (int i = Bolts.Count - 1; i >= 0; i--)
            {
                Bolt b = Bolts[i];
                if (b.Go == null)
                {
                    Bolts.RemoveAt(i);
                    continue;
                }
                Vector3 p = b.Go.transform.position;
                Vector3 np = p + b.V * dt;
                RaycastHit hit;
                if (WeaponLine(p, np, out hit))
                {
                    GaGun g = b.Gun != null ? b.Gun : Current;
                    HurtHit(hit, g, b.V.sqrMagnitude > 0.01f ? b.V.normalized : Vector3.forward);
                    GaFx.Spark(hit.point, hit.normal, b.Col);
                    GaFx.Blast(hit.point);
                    UnityEngine.Object.Destroy(b.Go);
                    Bolts.RemoveAt(i);
                    continue;
                }
                b.Go.transform.position = np;
                if (b.V.sqrMagnitude > 0.01f)
                    b.Go.transform.rotation = Quaternion.LookRotation(b.V.normalized)
                        * Quaternion.Euler(90f, 0f, 0f);
                b.Life -= dt;
                if (b.Life <= 0f)
                {
                    GaFx.Blast(np);
                    UnityEngine.Object.Destroy(b.Go);
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
            if (g.Body == "at4.obj")
                return 70f;
            if (g.Body == "q_RPG7.obj")
                return 58f;
            if (g.Body == "rbs70.obj")
                return 40f;
            if (g.Body == "g11.obj")
                return 26f;
            if (g.Family == 0)
                return 14f;
            if (g.Family == 1)
                return 10f;
            if (g.Family == 3)
                return 12f;
            if (g.Family == 4)
                return 8f;
            if (g.Family == 6)
                return 18f;
            if (g.Family == 7)
                return g.Body == "q_AK47.obj" || g.Body == "ak_body.obj" ? 22f : 18f;
            if (g.Family == 8)
                return 26f;
            if (g.Family == 10)
                return 32f;
            if (g.Kind == "HEAT")
                return 58f;
            if (g.Kind == "HE")
                return 40f;
            return 16f;
        }

        internal static float PierceHp(GaGun g)
        {
            if (g == null)
                return 4f;
            if (g.Body == "at4.obj")
                return 95f;
            if (g.Body == "q_RPG7.obj")
                return 75f;
            if (g.Body == "rbs70.obj")
                return 55f;
            if (g.Body == "g11.obj")
                return 7.5f;
            if (g.Family == 0)
                return 4f;
            if (g.Family == 1)
                return 3f;
            if (g.Family == 3)
                return 4f;
            if (g.Family == 4)
                return 3.5f;
            if (g.Family == 7 && (g.Body == "q_AK47.obj" || g.Body == "ak_body.obj"))
                return 5.5f;
            if (g.Family == 8)
                return 7f;
            if (g.Family == 10)
                return 9f;
            if (g.Kind == "HEAT")
                return 75f;
            if (g.Kind == "HE")
                return 55f;
            return 4.5f;
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

        private static void HurtHit(RaycastHit hit, GaGun g, Vector3 incoming)
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
            {
                if (Bolts[i].Go != null)
                    UnityEngine.Object.Destroy(Bolts[i].Go);
            }
            Bolts.Clear();
        }

        internal static void AttachViews(Transform eye)
        {
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
