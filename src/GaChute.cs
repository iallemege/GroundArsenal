using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace GroundArsenal
{
    /// <summary>
    /// Cargo-bay jump: MC-260 Chimera, UH-90 Ibis, VL-49 Tarantula.
    /// </summary>
    internal static class GaChute
    {
        internal static bool Falling;
        internal static bool Open;
        internal static bool CargoJump;
        internal static bool SoftLand;

        private static AircraftDefinition _chimera;
        private static AircraftDefinition _vl49;
        private static AircraftDefinition _uh90;
        private static Vector3 _vel;
        private static float _born;
        private static GameObject _canopy;
        private static float _agl = -1f;
        private static Renderer _srcCanopy;
        private static Renderer _srcLines;
        private static Material _cloth;
        private static bool _scanned;

        internal static bool IsDropship(Aircraft ac)
        {
            return KindOf(ac) != DropKind.None;
        }

        internal static DropKind KindOf(Aircraft ac)
        {
            if (ac == null)
                return DropKind.None;
            string blob = "";
            try
            {
                AircraftDefinition def = ac.definition;
                if (def != null)
                    blob = (def.jsonKey ?? "") + " " + (def.unitName ?? "");
            }
            catch { }
            blob += " " + ac.name;
            return KindFromName(blob);
        }

        internal static DropKind KindFromName(string s)
        {
            if (string.IsNullOrEmpty(s))
                return DropKind.None;
            string l = s.ToLowerInvariant();
            if (l.IndexOf("mc260", StringComparison.Ordinal) >= 0
                || l.IndexOf("mc-260", StringComparison.Ordinal) >= 0
                || l.IndexOf("chimera", StringComparison.Ordinal) >= 0)
                return DropKind.Chimera;
            if (l.IndexOf("uh-90", StringComparison.Ordinal) >= 0
                || l.IndexOf("uh90", StringComparison.Ordinal) >= 0
                || l.IndexOf("ibis", StringComparison.Ordinal) >= 0
                || l.IndexOf("utilityhelo", StringComparison.Ordinal) >= 0)
                return DropKind.Uh90;
            if (l.IndexOf("vl-49", StringComparison.Ordinal) >= 0
                || l.IndexOf("vl49", StringComparison.Ordinal) >= 0
                || l.IndexOf("tarantula", StringComparison.Ordinal) >= 0
                || l.IndexOf("quadvtol", StringComparison.Ordinal) >= 0)
                return DropKind.Vl49;
            return DropKind.None;
        }

        internal static AircraftDefinition Def(DropKind kind)
        {
            if (kind == DropKind.Uh90)
            {
                if (_uh90 == null)
                    _uh90 = Scan(DropKind.Uh90);
                return _uh90;
            }
            if (kind == DropKind.Chimera)
            {
                if (_chimera == null)
                    _chimera = Scan(DropKind.Chimera);
                return _chimera;
            }
            if (kind == DropKind.Vl49)
            {
                if (_vl49 == null)
                    _vl49 = Scan(DropKind.Vl49);
                return _vl49;
            }
            return Scan(kind);
        }

        internal static AircraftDefinition PlayerLiftDef()
        {
            AircraftDefinition d = Def(DropKind.Chimera);
            if (d != null)
                return d;
            return Def(DropKind.Vl49);
        }

        internal static AircraftDefinition LiftDef()
        {
            AircraftDefinition d = PlayerLiftDef();
            if (d != null)
                return d;
            return Def(DropKind.Uh90);
        }

        internal static string LiftName()
        {
            AircraftDefinition d = PlayerLiftDef();
            if (d == null)
                d = LiftDef();
            if (d == null)
                return "LIFT";
            if (!string.IsNullOrEmpty(d.code))
                return d.code.ToUpperInvariant();
            if (!string.IsNullOrEmpty(d.unitName))
                return d.unitName.ToUpperInvariant();
            return "LIFT";
        }

        private static AircraftDefinition Scan(DropKind want)
        {
            try
            {
                Encyclopedia enc = Encyclopedia.i;
                if (enc == null || enc.aircraft == null)
                    return null;
                foreach (AircraftDefinition d in enc.aircraft)
                {
                    if (d == null)
                        continue;
                    string blob = (d.jsonKey ?? "") + " " + (d.unitName ?? "") + " " + (d.code ?? "");
                    if (KindFromName(blob) == want)
                        return d;
                }
            }
            catch { }
            return null;
        }

        internal static Vector3 ExitPoint(Aircraft ac)
        {
            return ExitPoint(ac, 0);
        }

        internal static Vector3 ExitPoint(Aircraft ac, int slot)
        {
            Transform t = ac.transform;
            Vector3 side = t.right * ((slot % 3) - 1) * 1.35f;
            DropKind k = KindOf(ac);
            if (k == DropKind.Uh90)
                return t.position - t.forward * 4.2f - t.up * 1.15f + side;
            if (k == DropKind.Vl49)
                return t.position - t.forward * 9.2f - t.up * 2.1f + side;
            if (k == DropKind.Chimera)
                return t.position - t.forward * 16.5f - t.up * 2f + side;
            return t.position + t.right * 2.2f + side * 0.4f;
        }

        internal static Vector3 ExitPoint(Aircraft ac, bool unusedChimeraFlag)
        {
            return ExitPoint(ac, 0);
        }

        internal static bool IsChimera(Aircraft ac)
        {
            return KindOf(ac) == DropKind.Chimera;
        }

        internal static void Begin(Vector3 inherit, bool cargo)
        {
            if (GaClass.Picked == GaKitId.Tsf)
            {
                Clear();
                GaTsf.EnterJump(inherit, true);
                return;
            }
            Falling = true;
            Open = false;
            CargoJump = cargo;
            SoftLand = false;
            _vel = inherit;
            if (_vel.y > 12f)
                _vel.y = 12f;
            _born = Time.unscaledTime;
            _agl = -1f;
            DestroyCanopy();
        }

        internal static void Clear()
        {
            Falling = false;
            Open = false;
            CargoJump = false;
            SoftLand = false;
            _vel = Vector3.zero;
            _agl = -1f;
            DestroyCanopy();
        }

        internal static float Agl
        {
            get { return _agl; }
        }

        internal static float ProbeAgl(Vector3 pos)
        {
            return GaPhys.Agl(pos, 8000f);
        }

        internal static void Tick(Transform pawn, CharacterController cc, float dt, ref Vector3 world, ref float vertVel)
        {
            if (!Falling || pawn == null)
                return;
            if (dt > 0.05f)
                dt = 0.05f;
            _agl = ProbeAgl(pawn.position);
            if (cc != null && cc.isGrounded && Time.unscaledTime > _born + 0.7f
                && _agl >= 0f && _agl < 5f)
            {
                Land();
                return;
            }
            if (!Open)
            {
                bool tap = Input.GetKeyDown(KeyCode.Space) || GaVr.JumpDown;
                if (tap)
                    OpenCanopy(pawn);
            }
            else
            {
                bool tap = Input.GetKeyDown(KeyCode.Space) || GaVr.JumpDown;
                if (tap)
                    CloseCanopy();
            }
            vertVel += Physics.gravity.y * dt;
            if (!Open)
            {
                if (vertVel < -62f)
                    vertVel = -62f;
                _vel.x = Mathf.MoveTowards(_vel.x, 0f, dt * 2.4f);
                _vel.z = Mathf.MoveTowards(_vel.z, 0f, dt * 2.4f);
                world.x = _vel.x + world.x * 0.15f;
                world.z = _vel.z + world.z * 0.15f;
            }
            else
            {
                vertVel = Mathf.MoveTowards(vertVel, -18f, dt * 48f);
                _vel.x = Mathf.MoveTowards(_vel.x, 0f, dt * 28f);
                _vel.z = Mathf.MoveTowards(_vel.z, 0f, dt * 28f);
                if (world.sqrMagnitude > 90f)
                    world = world.normalized * 9.5f;
                world.x = _vel.x + world.x;
                world.z = _vel.z + world.z;
            }
            world.y = vertVel;
            PoseCanopy(pawn);
        }

        internal static void Land()
        {
            bool soft = Open;
            Falling = false;
            Open = false;
            SoftLand = soft;
            DestroyCanopy();
            if (soft)
                GaMode.Deny("On the ground.", "着陆。");
        }

        internal static string Hint()
        {
            if (!Falling)
                return "";
            return PromptAction() + "   " + PromptAgl();
        }

        internal static string PromptAction()
        {
            if (!Falling)
                return "";
            return Open ? "SPACE  —  CUT AWAY" : "SPACE  —  OPEN CHUTE";
        }

        internal static string PromptAgl()
        {
            if (!Falling)
                return "";
            string agl = _agl < 0f ? "----" : Mathf.RoundToInt(_agl).ToString();
            return "AGL  " + agl + " m";
        }

        internal static string CockpitHint(Aircraft ac)
        {
            DropKind k = KindOf(ac);
            float agl = ac != null ? GaPhys.Agl(ac.transform.position, 80f) : 0f;
            if (k == DropKind.Uh90 && agl >= 12f)
                return "UH-90  T  squad jump";
            if (k == DropKind.Vl49 && agl >= 12f)
                return "VL-49  T  squad jump";
            if (k == DropKind.Chimera && agl >= 12f)
                return "MC-260  TAIL  T jump";
            if (GaHangar.OnCarrier(ac) && agl < 28f)
                return "CARRIER  T  deck deploy";
            if (GaForce.Kind == GaForceKind.Air && agl < 28f)
                return "AIRFIELD  T  hop out";
            return "";
        }

        internal static GameObject MakeCanopy(Transform pawn)
        {
            if (pawn == null)
                return null;
            GameObject go = CloneVanilla(pawn);
            if (go != null)
                return go;
            return MakeDome(pawn);
        }

        private static GameObject CloneVanilla(Transform pawn)
        {
            ScanVanilla();
            if (_srcCanopy == null)
                return null;
            Mesh mesh = MeshOf(_srcCanopy);
            if (mesh == null)
                return null;
            GameObject root = new GameObject("GaChuteCanopy");
            root.transform.SetParent(pawn, false);
            root.transform.localPosition = new Vector3(0f, 3.9f, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            GameObject cloth = ChildMesh(root.transform, "Canopy", mesh, _srcCanopy.sharedMaterials);
            if (cloth == null)
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }
            FitSpan(cloth, 7.4f);
            if (_srcLines != null)
            {
                Mesh lines = MeshOf(_srcLines);
                if (lines != null)
                    ChildMesh(root.transform, "Lines", lines, _srcLines.sharedMaterials);
            }
            return root;
        }

        private static GameObject ChildMesh(Transform parent, string name, Mesh mesh, Material[] mats)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            if (mats != null && mats.Length > 0)
                mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private static void FitSpan(GameObject go, float want)
        {
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null)
                return;
            Bounds b = mr.bounds;
            float span = Mathf.Max(b.size.x, b.size.z);
            if (span < 0.15f)
                span = 0.15f;
            if (span > 4.5f && span < 14f)
                return;
            float s = want / span;
            go.transform.localScale = Vector3.one * s;
        }

        private static Mesh MeshOf(Renderer r)
        {
            if (r == null)
                return null;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                return mf.sharedMesh;
            SkinnedMeshRenderer skin = r as SkinnedMeshRenderer;
            if (skin != null && skin.sharedMesh != null)
                return skin.sharedMesh;
            return null;
        }

        private static void ScanVanilla()
        {
            if (_scanned)
                return;
            _scanned = true;
            try
            {
                GameObject prefab = null;
                try
                {
                    if (GameAssets.i != null)
                        prefab = GameAssets.i.pilotDismounted;
                }
                catch { }
                if (GrabFrom(prefab))
                    return;
                Parachute[] all = Resources.FindObjectsOfTypeAll<Parachute>();
                if (all == null)
                    return;
                for (int i = 0; i < all.Length; i++)
                {
                    if (GrabFrom(all[i] != null ? all[i].gameObject : null))
                        return;
                }
            }
            catch { }
        }

        private static bool GrabFrom(GameObject root)
        {
            if (root == null)
                return false;
            Parachute chute = root.GetComponentInChildren<Parachute>(true);
            if (chute == null)
                return false;
            GameObject canopy = FieldGo(chute, "canopy");
            GameObject lines = FieldGo(chute, "lines");
            Renderer cloth = RendererOf(canopy);
            if (cloth == null)
                cloth = FieldRen(chute, "parachuteRenderer");
            if (cloth == null)
                cloth = chute.GetComponentInChildren<Renderer>(true);
            if (cloth == null || MeshOf(cloth) == null)
                return false;
            _srcCanopy = cloth;
            _srcLines = RendererOf(lines);
            return true;
        }

        private static GameObject FieldGo(object obj, string name)
        {
            try
            {
                FieldInfo f = AccessTools.Field(obj.GetType(), name);
                if (f == null)
                    return null;
                return f.GetValue(obj) as GameObject;
            }
            catch
            {
                return null;
            }
        }

        private static Renderer FieldRen(object obj, string name)
        {
            try
            {
                FieldInfo f = AccessTools.Field(obj.GetType(), name);
                if (f == null)
                    return null;
                return f.GetValue(obj) as Renderer;
            }
            catch
            {
                return null;
            }
        }

        private static Renderer RendererOf(GameObject go)
        {
            if (go == null)
                return null;
            return go.GetComponentInChildren<Renderer>(true);
        }

        private static GameObject MakeDome(Transform pawn)
        {
            GameObject root = new GameObject("GaChuteCanopy");
            root.transform.SetParent(pawn, false);
            root.transform.localPosition = new Vector3(0f, 4.4f, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Dome";
            GaPhys.Strip(dome);
            dome.transform.SetParent(root.transform, false);
            dome.transform.localPosition = Vector3.zero;
            dome.transform.localRotation = Quaternion.identity;
            dome.transform.localScale = new Vector3(7.2f, 1.05f, 7.2f);
            MeshRenderer mr = dome.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = ClothMat();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            return root;
        }

        private static Material ClothMat()
        {
            if (_cloth != null)
                return _cloth;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null)
                sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (sh == null)
                sh = Shader.Find("Unlit/Texture");
            if (sh == null)
                sh = Shader.Find("Unlit/Color");
            if (sh == null)
                sh = Shader.Find("Sprites/Default");
            if (sh == null)
                sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null)
                return null;
            Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color a = new Color(0.78f, 0.72f, 0.54f, 1f);
            Color b = new Color(0.58f, 0.36f, 0.24f, 1f);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float ang = Mathf.Atan2(y - 15.5f, x - 15.5f);
                    int gore = ((int)Mathf.Floor((ang + Mathf.PI) * 6f / Mathf.PI)) & 1;
                    tex.SetPixel(x, y, gore == 0 ? a : b);
                }
            }
            tex.Apply(false, true);
            Material mat = new Material(sh);
            mat.name = "GaChuteCloth";
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
            Color tint = a;
            mat.color = tint;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", tint);
            _cloth = mat;
            return _cloth;
        }

        private static void OpenCanopy(Transform pawn)
        {
            if (Open)
                return;
            Open = true;
            DestroyCanopy();
            _canopy = MakeCanopy(pawn);
            GaMode.Deny("CHUTE OPEN", "降落伞已打开");
        }

        private static void CloseCanopy()
        {
            if (!Open)
                return;
            Open = false;
            DestroyCanopy();
            GaMode.Deny("CHUTE CUT AWAY", "降落伞已收起");
        }

        private static void PoseCanopy(Transform pawn)
        {
            if (_canopy == null || pawn == null)
                return;
            _canopy.transform.localRotation = Quaternion.Euler(4f * Mathf.Sin(Time.time * 1.6f), 0f, 3f * Mathf.Cos(Time.time * 1.3f));
        }

        private static void DestroyCanopy()
        {
            if (_canopy == null)
                return;
            UnityEngine.Object.Destroy(_canopy);
            _canopy = null;
        }
    }

    internal enum DropKind
    {
        None = 0,
        Chimera = 1,
        Uh90 = 2,
        Vl49 = 3
    }
}
