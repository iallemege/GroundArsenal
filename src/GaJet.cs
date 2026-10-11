using UnityEngine;
using UnityEngine.Rendering;

namespace GroundArsenal
{
    /// <summary>Spec Ops jetpack: Space toggles, Shift climbs, Ctrl drops.</summary>
    internal static class GaJet
    {
        internal static bool On;
        internal static float Fuel = 1f;

        private const float DrainAir = 0.085f;
        private const float DrainThrust = 0.12f;
        private const float Regen = 0.22f;
        private const float Climb = 14f;
        private const float Drop = -12f;
        private const float MaxAgl = 92f;
        private static GameObject _pack;
        private static GameObject _glow;
        private static Material _metal;
        private static Material _hot;
        private static Transform _pawn;
        private static bool _kick;

        internal static bool Owns()
        {
            return GaMode.Active && !GaMode.Dead && !GaMode.Downed
                && !GaRide.Busy && !GaChute.Falling
                && GaClass.Picked == GaKitId.SpecOps;
        }

        internal static void Clear()
        {
            On = false;
            Fuel = 1f;
            DestroyPack();
            _pawn = null;
        }

        internal static void Attach(Transform pawn)
        {
            DestroyPack();
            _pawn = pawn;
            On = false;
            Fuel = 1f;
            if (pawn == null || GaClass.Picked != GaKitId.SpecOps)
                return;
            _pack = new GameObject("GaJetPack");
            _pack.transform.SetParent(pawn, false);
            _pack.transform.localPosition = new Vector3(0f, 1.18f, -0.22f);
            _pack.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            _pack.transform.localScale = Vector3.one;
            MakeCan(_pack.transform, new Vector3(-0.11f, 0f, 0f));
            MakeCan(_pack.transform, new Vector3(0.11f, 0f, 0f));
            _glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _glow.name = "GaJetGlow";
            GaPhys.Strip(_glow);
            _glow.transform.SetParent(_pack.transform, false);
            _glow.transform.localPosition = new Vector3(0f, -0.28f, 0.02f);
            _glow.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
            MeshRenderer gr = _glow.GetComponent<MeshRenderer>();
            if (gr != null)
            {
                gr.sharedMaterial = HotMat();
                gr.shadowCastingMode = ShadowCastingMode.Off;
                gr.receiveShadows = false;
            }
            _glow.SetActive(false);
        }

        internal static bool TryToggle()
        {
            if (!Owns())
                return false;
            if (GaHud.BlocksLook || GaRadio.Open)
                return false;
            if (!Input.GetKeyDown(KeyCode.Space) && !GaVr.JumpDown)
                return false;
            if (On)
            {
                Cut();
                GaMode.Deny("JETPACK OFF", "喷气背包关闭");
            }
            else
            {
                if (Fuel < 0.08f)
                {
                    GaMode.Deny("JETPACK DRY", "喷气背包没燃料");
                    return true;
                }
                On = true;
                _kick = true;
                GaFps.Stance = 0;
                GaMode.Deny("JETPACK ON", "喷气背包启动");
            }
            return true;
        }

        internal static void Cut()
        {
            On = false;
            _kick = false;
            if (_glow != null)
                _glow.SetActive(false);
        }

        internal static bool Fly(ref float vertVel, ref Vector3 world, Vector3 pos, bool grounded, float dt)
        {
            if (!On)
            {
                if (grounded && Fuel < 1f)
                {
                    Fuel += Regen * dt;
                    if (Fuel > 1f)
                        Fuel = 1f;
                }
                if (_glow != null)
                    _glow.SetActive(false);
                return false;
            }
            if (!Owns() || Fuel <= 0.001f)
            {
                Cut();
                if (Fuel <= 0.001f)
                    GaMode.Deny("JETPACK DRY", "喷气背包没燃料");
                return false;
            }
            if (_kick)
            {
                _kick = false;
                if (vertVel < 8f)
                    vertVel = 8f;
            }
            bool up = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                || (GaVr.Active && GaVr.SprintHeld);
            bool down = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            float drain = DrainAir;
            if (up || down)
                drain = DrainThrust;
            Fuel -= drain * dt;
            if (Fuel < 0f)
                Fuel = 0f;
            float want = 0f;
            if (up)
                want = Climb;
            else if (down)
                want = Drop;
            if (up)
            {
                float agl = GaPhys.Agl(pos, 160f);
                if (agl > MaxAgl)
                    want = 0f;
            }
            float step = (up || down ? 38f : 28f) * dt;
            vertVel = Mathf.MoveTowards(vertVel, want, step);
            float air = new Vector3(world.x, 0f, world.z).magnitude;
            if (air < 0.05f)
            {
                Vector3 hold = new Vector3(world.x, 0f, world.z);
                world = hold;
            }
            else
            {
                Vector3 flat = new Vector3(world.x, 0f, world.z);
                flat = flat.normalized * Mathf.Max(air, GaClass.Walk(GaKitId.SpecOps) * 1.15f);
                world.x = flat.x;
                world.z = flat.z;
            }
            if (_glow != null)
            {
                _glow.SetActive(true);
                float pulse = 0.14f + (up ? 0.08f : 0f) + 0.03f * Mathf.Sin(Time.time * 22f);
                _glow.transform.localScale = new Vector3(pulse, pulse, pulse);
            }
            return true;
        }

        internal static string Hint()
        {
            if (!Owns())
                return null;
            int pct = Mathf.RoundToInt(Fuel * 100f);
            if (On)
                return "JETPACK  SHIFT up  CTRL down  SPACE off  FUEL " + pct.ToString() + "%";
            if (Fuel < 0.08f)
                return "JETPACK DRY  " + pct.ToString() + "%";
            return "SPACE  JETPACK  FUEL " + pct.ToString() + "%";
        }

        internal static void Draw()
        {
            if (!Owns() || !GaPerfFrameGate.Paint())
                return;
            string msg = Hint();
            if (string.IsNullOrEmpty(msg))
                return;
            float w = 560f;
            float x = Screen.width * 0.5f - w * 0.5f;
            float y = Screen.height * 0.78f;
            Texture2D px = GaFxUi.Px;
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.04f, 0.62f);
            GUI.DrawTexture(new Rect(x, y, w, 22f), px);
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            GUI.DrawTexture(new Rect(x + 8f, y + 16f, w - 16f, 4f), px);
            GUI.color = On
                ? new Color(1f, 0.55f, 0.12f, 0.95f)
                : new Color(0.55f, 0.82f, 0.95f, 0.9f);
            GUI.DrawTexture(new Rect(x + 8f, y + 16f, (w - 16f) * Fuel, 4f), px);
            GUI.color = On ? new Color(1f, 0.78f, 0.28f, 1f) : new Color(0.85f, 0.9f, 0.95f, 0.95f);
            GUI.Label(new Rect(x, y - 2f, w, 18f), msg, GUI.skin.label);
            GUI.color = prev;
        }

        private static void MakeCan(Transform parent, Vector3 local)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "GaJetCan";
            GaPhys.Strip(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f);
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            if (r == null)
                return;
            r.sharedMaterial = MetalMat();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static Material MetalMat()
        {
            if (_metal != null)
                return _metal;
            _metal = MakeMat(new Color(0.12f, 0.13f, 0.14f, 1f), "GaJetMetal");
            return _metal;
        }

        private static Material HotMat()
        {
            if (_hot != null)
                return _hot;
            _hot = MakeMat(new Color(1f, 0.45f, 0.08f, 1f), "GaJetHot");
            return _hot;
        }

        private static Material MakeMat(Color col, string name)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null)
                sh = Shader.Find("Unlit/Color");
            if (sh == null)
                sh = Shader.Find("Sprites/Default");
            if (sh == null)
                sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null)
                return null;
            Material mat = new Material(sh);
            mat.name = name;
            mat.color = col;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", col);
            return mat;
        }

        private static void DestroyPack()
        {
            if (_pack != null)
                UnityEngine.Object.Destroy(_pack);
            _pack = null;
            _glow = null;
        }
    }
}
