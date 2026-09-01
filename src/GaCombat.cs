using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace GroundArsenal
{
    internal static class GaCombat
    {
        internal const string PrefHotbar = "GroundArsenal.Hotbar";
        internal const string PrefCarry = "GroundArsenal.Carry";

        private static Mesh[] _famMesh;
        private static Texture2D[] _famTex;
        private static Material[] _famMat;
        private static bool _tried;
        private static GameObject _viewRoot;
        private static GameObject[] _famGo;
        private static Material _unlitTex;
        private static Material _unlitCol;

        internal static int[] Hotbar = DefaultHotbar();
        internal static int Slot;
        internal static int[] Mag = new int[GaCatalog.Count];
        internal static int[] Reserve = new int[GaCatalog.Count];
        internal static float NextShotAt;
        internal static float ReloadUntil;
        internal static float Ads;
        internal static string Status = "";

        private class Bolt
        {
            internal Vector3 P;
            internal Vector3 V;
            internal float Life;
            internal float Dmg;
            internal float Pen;
            internal Color Col;
        }

        private static readonly List<Bolt> Bolts = new List<Bolt>(32);

        internal static GaGun Current
        {
            get { return GaCatalog.Get(Hotbar[Slot]); }
        }

        internal static int[] DefaultHotbar()
        {
            return new int[] { 0, 18, 56, 80, 132, 196, 228, 240, 268 };
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
            int id = 18;
            try { id = PlayerPrefs.GetInt(PrefCarry, 18); }
            catch { }
            if (id < 0 || id >= GaCatalog.Count)
                id = 18;
            return id;
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
            ShowFam(Current);
        }

        private static void StartReload(GaGun g)
        {
            if (g == null)
                return;
            if (Reserve[g.Id] <= 0 || Mag[g.Id] >= g.Mag)
                return;
            ReloadUntil = Time.time + (g.Fire == 4 ? 2.4f : 1.6f);
            int need = g.Mag - Mag[g.Id];
            int take = need;
            if (take > Reserve[g.Id])
                take = Reserve[g.Id];
            Mag[g.Id] += take;
            Reserve[g.Id] -= take;
        }

        private static void Shoot(Transform eye, GaGun g)
        {
            Mag[g.Id]--;
            float interval = 60f / Mathf.Max(30f, g.Rpm);
            NextShotAt = Time.time + interval;
            int pellets = g.Fire == 3 ? 8 : 1;
            float spread = g.Spread * (1f - Ads * 0.7f);
            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = eye.forward;
                dir += eye.right * UnityEngine.Random.Range(-spread, spread);
                dir += eye.up * UnityEngine.Random.Range(-spread, spread);
                dir.Normalize();
                if (g.Fire == 4)
                    SpawnBolt(eye.position + eye.forward * 0.4f, dir * 90f, g);
                else
                    Hitscan(eye.position, dir, g);
            }
            Status = Mag[g.Id].ToString() + " / " + Reserve[g.Id].ToString();
        }

        private static void Hitscan(Vector3 origin, Vector3 dir, GaGun g)
        {
            RaycastHit hit;
            float dist = g.Range > 10f ? g.Range : 80f;
            if (Physics.Raycast(origin, dir, out hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                HurtHit(hit, g);
                GaFx.Spark(hit.point, hit.normal, g.Tracer);
                GaFx.Tracer(origin, hit.point, g.Tracer);
            }
            else
                GaFx.Tracer(origin, origin + dir * dist, g.Tracer);
        }

        private static void SpawnBolt(Vector3 p, Vector3 v, GaGun g)
        {
            Bolt b = new Bolt();
            b.P = p;
            b.V = v;
            b.Life = 4f;
            b.Dmg = g.Damage;
            b.Pen = g.Pen;
            b.Col = g.Tracer;
            Bolts.Add(b);
        }

        private static void TickBolts(float dt)
        {
            for (int i = Bolts.Count - 1; i >= 0; i--)
            {
                Bolt b = Bolts[i];
                Vector3 np = b.P + b.V * dt;
                RaycastHit hit;
                if (Physics.Linecast(b.P, np, out hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    GaGun dummy = Current;
                    HurtHit(hit, dummy);
                    GaFx.Spark(hit.point, hit.normal, b.Col);
                    Bolts.RemoveAt(i);
                    continue;
                }
                GaFx.Tracer(b.P, np, b.Col);
                b.P = np;
                b.Life -= dt;
                if (b.Life <= 0f)
                    Bolts.RemoveAt(i);
            }
        }

        private static void HurtHit(RaycastHit hit, GaGun g)
        {
            if (g == null || hit.collider == null)
                return;
            float pierce = g.Pen;
            float blast = g.Fire == 4 ? g.Damage * 0.4f : 0f;
            float impact = g.Damage;
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
            }
        }

        internal static void ClearBolts()
        {
            Bolts.Clear();
        }

        internal static void AttachViews(Transform eye)
        {
            DestroyViews();
            if (eye == null)
                return;
            EnsureAssets();
            _viewRoot = new GameObject("GaView");
            _viewRoot.transform.SetParent(eye, false);
            _viewRoot.transform.localPosition = Vector3.zero;
            _viewRoot.transform.localRotation = Quaternion.identity;
            _famGo = new GameObject[GaCatalog.FamilyCount];
            Vector3[] pos = new Vector3[]
            {
                new Vector3(0.16f, -0.13f, 0.28f),
                new Vector3(0.16f, -0.13f, 0.30f),
                new Vector3(0.16f, -0.14f, 0.30f),
                new Vector3(0.18f, -0.15f, 0.32f),
                new Vector3(0.20f, -0.17f, 0.36f),
                new Vector3(0.20f, -0.17f, 0.36f),
                new Vector3(0.20f, -0.16f, 0.34f),
                new Vector3(0.20f, -0.16f, 0.34f),
                new Vector3(0.20f, -0.16f, 0.36f),
                new Vector3(0.20f, -0.17f, 0.38f),
                new Vector3(0.20f, -0.17f, 0.40f),
                new Vector3(0.18f, -0.18f, 0.36f),
                new Vector3(0.16f, -0.18f, 0.36f),
                new Vector3(0.16f, -0.22f, 0.40f),
                new Vector3(0.12f, -0.22f, 0.36f),
                new Vector3(0.18f, -0.15f, 0.32f)
            };
            float[] len = new float[]
            {
                0.28f, 0.32f, 0.34f, 0.42f, 0.50f, 0.50f, 0.48f, 0.52f,
                0.54f, 0.56f, 0.58f, 0.55f, 0.46f, 0.62f, 0.55f, 0.50f
            };
            for (int i = 0; i < GaCatalog.FamilyCount; i++)
            {
                Material mat = _famMat != null && i < _famMat.Length && _famMat[i] != null
                    ? _famMat[i] : _unlitCol;
                _famGo[i] = MakeView("Fam" + i, MeshAt(i), mat, pos[i], len[i]);
                _famGo[i].SetActive(false);
            }
            ShowFam(Current);
        }

        internal static void DestroyViews()
        {
            if (_viewRoot != null)
                UnityEngine.Object.Destroy(_viewRoot);
            _viewRoot = null;
            _famGo = null;
        }

        private static void ShowFam(GaGun g)
        {
            if (_famGo == null)
                return;
            int fam = g != null ? g.Family : 0;
            for (int i = 0; i < _famGo.Length; i++)
            {
                if (_famGo[i] != null)
                    _famGo[i].SetActive(i == fam);
            }
            if (g != null && _famGo[fam] != null)
            {
                Renderer r = _famGo[fam].GetComponent<Renderer>();
                if (r != null && r.material != null)
                    r.material.color = Color.Lerp(Color.white, g.Tint, 0.35f);
            }
        }

        private static GameObject MakeView(string name, Mesh mesh, Material mat, Vector3 local, float wantLen)
        {
            GameObject go;
            if (mesh != null)
            {
                go = new GameObject(name);
                MeshFilter mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                float sc = Fit(mesh, wantLen);
                go.transform.localScale = Vector3.one * sc;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                Collider c = go.GetComponent<Collider>();
                if (c != null)
                    UnityEngine.Object.Destroy(c);
                go.transform.localScale = new Vector3(0.05f, 0.05f, wantLen);
                Renderer r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            go.transform.SetParent(_viewRoot.transform, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            return go;
        }

        private static float Fit(Mesh mesh, float wantLen)
        {
            if (mesh == null || wantLen < 0.05f)
                return 1f;
            Vector3 sz = mesh.bounds.size;
            float longest = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
            if (longest < 0.01f)
                return 1f;
            return wantLen / longest;
        }

        private static Mesh MeshAt(int i)
        {
            if (_famMesh == null || i < 0 || i >= _famMesh.Length)
                return null;
            return _famMesh[i];
        }

        private static void EnsureAssets()
        {
            if (_tried)
                return;
            _tried = true;
            Shader sh = Shader.Find("Unlit/Texture");
            if (sh == null)
                sh = Shader.Find("Unlit/Color");
            if (sh == null)
                sh = Shader.Find("Standard");
            _unlitTex = new Material(sh);
            Shader col = Shader.Find("Unlit/Color");
            if (col == null)
                col = sh;
            _unlitCol = new Material(col);
            _unlitCol.color = new Color(0.18f, 0.18f, 0.19f);
            _famMesh = new Mesh[GaCatalog.FamilyCount];
            _famTex = new Texture2D[GaCatalog.FamilyCount];
            _famMat = new Material[GaCatalog.FamilyCount];
            string dir = GaObj.ResolveDir();
            int nMesh = 0;
            int nTex = 0;
            for (int i = 0; i < GaCatalog.FamilyCount; i++)
            {
                string stem = "fam_" + i.ToString("00");
                if (!string.IsNullOrEmpty(dir))
                {
                    _famMesh[i] = GaObj.Load(Path.Combine(dir, stem + ".obj"));
                    _famTex[i] = GaObj.LoadPng(Path.Combine(dir, stem + ".png"));
                }
                if (_famMesh[i] != null)
                    nMesh++;
                if (_famTex[i] != null)
                {
                    nTex++;
                    _famMat[i] = new Material(_unlitTex);
                    _famMat[i].mainTexture = _famTex[i];
                    _famMat[i].color = Color.white;
                }
            }
            if (GaPlugin.Log != null)
                GaPlugin.Log.LogInfo("GroundArsenal meshes " + nMesh + "/" + GaCatalog.FamilyCount
                    + " tex " + nTex);
        }
    }
}
