using UnityEngine;
using UnityEngine.UI;

namespace GroundArsenal
{
    /// <summary>Unscaled fade / slide for FPS HUD and IMGUI menus.</summary>
    internal static class GaFxUi
    {
        internal static float Hud;
        internal static float Arsenal;
        internal static float Shop;
        internal static float Radio;
        internal static float Kia;
        internal static float GunPop;
        internal static float Hangar;

        private static float _hudW = 1f;
        private static float _kiaW;
        private static float _gunW;
        private static float _gunBorn = -10f;
        private static Texture2D _px;
        private static bool _ready;
        private static Matrix4x4 _mat;
        private static Color _col;
        private static bool _pushed;

        internal static bool Busy
        {
            get
            {
                return Arsenal > 0.02f || Shop > 0.02f || Radio > 0.02f
                    || GunPop > 0.02f || Hangar > 0.02f;
            }
        }

        internal static Texture2D Px
        {
            get
            {
                Ensure();
                return _px;
            }
        }

        internal static void OnSpawn()
        {
            Hud = 0f;
            _hudW = 1f;
            Kia = 0f;
            _kiaW = 0f;
            GunPop = 0f;
            _gunW = 0f;
        }

        internal static void OnDeath()
        {
            _hudW = 0f;
            _kiaW = 1f;
        }

        internal static void OnGun()
        {
            GunPop = 1f;
            _gunW = 1f;
            _gunBorn = Time.unscaledTime;
        }

        internal static void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.05f)
                dt = 0.05f;
            Hud = Move(Hud, _hudW, dt, 3.6f);
            Arsenal = Move(Arsenal, GaHud.ArsenalOpen ? 1f : 0f, dt, 7.2f);
            Shop = Move(Shop, (GaShop.Open || GaGarage.Open) ? 1f : 0f, dt, 7.2f);
            Radio = Move(Radio, GaRadio.Open ? 1f : 0f, dt, 10f);
            Hangar = Move(Hangar,
                (GaHangar.MenuOpen || GaHangar.FieldOpen || GaHangar.DeathPick) ? 1f : 0f,
                dt, 6.4f);
            Kia = Move(Kia, _kiaW, dt, 2.6f);
            if (Time.unscaledTime > _gunBorn + 0.16f)
                _gunW = 0f;
            GunPop = Move(GunPop, _gunW, dt, 9f);
        }

        internal static float Smooth(float t)
        {
            if (t <= 0f)
                return 0f;
            if (t >= 1f)
                return 1f;
            return t * t * (3f - 2f * t);
        }

        internal static void Dim(float a)
        {
            if (a < 0.01f)
                return;
            Ensure();
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.42f * a);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
            GUI.color = prev;
        }

        internal static void FillGrad(Rect r, Color top, Color bot)
        {
            Ensure();
            const int n = 8;
            Color prev = GUI.color;
            float h = r.height / n;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                GUI.color = Color.Lerp(top, bot, t);
                GUI.DrawTexture(new Rect(r.x, r.y + i * h, r.width, h + 1f), _px);
            }
            GUI.color = prev;
        }

        internal static void Corners(float cx, float cy, float s, Color col)
        {
            Ensure();
            if (s < 8f)
                s = 8f;
            float x0 = cx - s * 0.5f;
            float y0 = cy - s * 0.5f;
            float arm = s * 0.28f;
            if (arm < 6f)
                arm = 6f;
            if (arm > 14f)
                arm = 14f;
            float th = 2f;
            Color prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(new Rect(x0, y0, arm, th), _px);
            GUI.DrawTexture(new Rect(x0, y0, th, arm), _px);
            GUI.DrawTexture(new Rect(x0 + s - arm, y0, arm, th), _px);
            GUI.DrawTexture(new Rect(x0 + s - th, y0, th, arm), _px);
            GUI.DrawTexture(new Rect(x0, y0 + s - th, arm, th), _px);
            GUI.DrawTexture(new Rect(x0, y0 + s - arm, th, arm), _px);
            GUI.DrawTexture(new Rect(x0 + s - arm, y0 + s - th, arm, th), _px);
            GUI.DrawTexture(new Rect(x0 + s - th, y0 + s - arm, th, arm), _px);
            GUI.color = prev;
        }

        internal static void LeadPip(float x, float y, Color col)
        {
            Ensure();
            Color prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(new Rect(x - 8f, y - 1.5f, 6f, 3f), _px);
            GUI.DrawTexture(new Rect(x + 2f, y - 1.5f, 6f, 3f), _px);
            GUI.DrawTexture(new Rect(x - 1.5f, y - 8f, 3f, 6f), _px);
            GUI.DrawTexture(new Rect(x - 1.5f, y + 2f, 3f, 6f), _px);
            GUI.DrawTexture(new Rect(x - 2f, y - 2f, 4f, 4f), _px);
            GUI.color = prev;
        }

        internal static void Push(float a, Vector2 pivot, float slide)
        {
            _mat = GUI.matrix;
            _col = GUI.color;
            _pushed = true;
            float s = Smooth(a);
            float sc = Mathf.Lerp(0.94f, 1f, s);
            Vector3 p = new Vector3(pivot.x, pivot.y, 0f);
            GUI.matrix = Matrix4x4.TRS(p, Quaternion.identity, Vector3.one)
                * Matrix4x4.Scale(new Vector3(sc, sc, 1f))
                * Matrix4x4.TRS(-p + new Vector3(0f, slide * (1f - s), 0f), Quaternion.identity, Vector3.one)
                * GUI.matrix;
            GUI.color = new Color(_col.r, _col.g, _col.b, _col.a * s);
        }

        internal static void Pop()
        {
            if (!_pushed)
                return;
            _pushed = false;
            GUI.matrix = _mat;
            GUI.color = _col;
        }

        internal static void AttachFade(GameObject go, float delay)
        {
            if (go == null)
                return;
            GaMenuFade f = go.GetComponent<GaMenuFade>();
            if (f != null)
                return;
            f = go.AddComponent<GaMenuFade>();
            f.Delay = delay;
            f.Dur = 0.34f;
        }

        internal static void Pulse(GameObject go)
        {
            if (go == null)
                return;
            GaMenuFade f = go.GetComponent<GaMenuFade>();
            if (f == null)
                f = go.AddComponent<GaMenuFade>();
            f.Kick();
        }

        private static float Move(float cur, float want, float dt, float spd)
        {
            return Mathf.MoveTowards(cur, want, dt * spd);
        }

        private static void Ensure()
        {
            if (_ready && _px != null)
                return;
            _ready = true;
            _px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }
    }

    internal sealed class GaMenuFade : MonoBehaviour
    {
        internal float Delay;
        internal float Dur = 0.32f;

        private float _born;
        private CanvasGroup _cg;

        private void Awake()
        {
            _born = Time.unscaledTime;
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null)
                _cg = gameObject.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;
        }

        internal void Kick()
        {
            _born = Time.unscaledTime;
            Delay = 0f;
            Dur = 0.16f;
            enabled = true;
            if (_cg == null)
                _cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _cg.alpha = 0.25f;
        }

        private void LateUpdate()
        {
            if (_cg == null)
                return;
            float u = (Time.unscaledTime - _born - Delay) / (Dur < 0.05f ? 0.05f : Dur);
            if (u < 0f)
            {
                _cg.alpha = 0f;
                return;
            }
            if (u > 1f)
                u = 1f;
            float s = u * u * (3f - 2f * u);
            _cg.alpha = s;
            if (u >= 1f)
                enabled = false;
        }
    }
}
