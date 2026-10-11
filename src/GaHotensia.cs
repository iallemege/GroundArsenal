using System.Runtime.CompilerServices;
using UnityEngine;

namespace GroundArsenal
{
    internal enum GaHotPhase
    {
        Off = 0,
        Session = 1,
        Brand = 2,
        Link = 3,
        Live = 4
    }

    /// <summary>HOTENSIA tactical visor: session TTY, brand, OHMD load bar, glass, battery, 10 km spots.</summary>
    internal static class GaHotensia
    {
        internal const string Brand = "Oritasy HOTENSIA\u2122";
        internal static GaHotPhase Phase;
        internal static float Battery = 1f;

        private const float BrandSec = 1.35f;
        private const float LinkSec = 3.20f;
        private const float DrainPerSec = 0.011f;
        private const float ChargePerSec = 0.028f;

        private static float _phaseT;
        private static bool _sessionDone;
        private static int _boot;
        private static int _typed;
        private static float _nextGlyph;
        private static int _blinkLeft;
        private static bool _cursorOn;
        private static string _l1 = "";
        private static string _l2 = "";
        private static Texture2D _px;
        private static Texture2D _glass;
        private static Texture2D _spec;
        private static Material _gl;
        private static GUIStyle _tty;
        private static GUIStyle _brand;
        private static GUIStyle _sub;
        private static GUIStyle _bit;
        private static GUIStyle _ok;
        private static GUIStyle _warn;
        private static bool _styles;
        private static readonly string[] Bit = new string[14];
        private static readonly int[] BitK = new int[14];
        private static int _bitN;
        private static int _bitSeq;
        private static float _bitNext;

        internal static bool Live
        {
            get { return Phase == GaHotPhase.Live; }
        }

        internal static bool HidesHud
        {
            get
            {
                return Phase == GaHotPhase.Session
                    || Phase == GaHotPhase.Brand
                    || Phase == GaHotPhase.Link;
            }
        }

        internal static void Clear()
        {
            if (Phase == GaHotPhase.Session)
                _sessionDone = true;
            Phase = GaHotPhase.Off;
            _boot = 0;
            _typed = 0;
            _l1 = "";
            _l2 = "";
            _bitN = 0;
        }

        internal static void OnSpawn()
        {
            Battery = 1f;
            _bitN = 0;
            if (_sessionDone)
            {
                Phase = GaHotPhase.Off;
                return;
            }
            Enter(GaHotPhase.Session);
            _boot = 0;
            _typed = 0;
            _l1 = "";
            _l2 = "";
            _cursorOn = true;
            _nextGlyph = Time.unscaledTime + 0.22f;
        }

        internal static void Tick()
        {
            if (GaTsf.Owns())
            {
                if (Phase != GaHotPhase.Off)
                    Clear();
                Charge(Time.unscaledDeltaTime);
                return;
            }
            if (!GaMode.Active || GaMode.Dead)
            {
                if (Phase == GaHotPhase.Live || Phase == GaHotPhase.Brand || Phase == GaHotPhase.Link)
                    Phase = GaHotPhase.Off;
                Charge(Time.unscaledDeltaTime);
                return;
            }
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.05f)
                dt = 0.05f;
            if (Phase == GaHotPhase.Session)
                TickSession();
            else if (Phase == GaHotPhase.Brand && Age() >= BrandSec)
                Enter(GaHotPhase.Link);
            else if (Phase == GaHotPhase.Link && Age() >= LinkSec)
            {
                Enter(GaHotPhase.Live);
                _bitN = 0;
                _bitSeq = 0;
                _bitNext = Time.unscaledTime;
            }
            else if (Phase == GaHotPhase.Live)
            {
                Battery -= DrainPerSec * dt;
                if (Battery <= 0f)
                {
                    Battery = 0f;
                    Phase = GaHotPhase.Off;
                    GaMode.Deny("HOTENSIA  battery empty", "HOTENSIA 电量耗尽");
                }
                else
                    TickBit();
            }
            else
                Charge(dt);
            if (!Input.GetKeyDown(KeyCode.X) || GaHud.BlocksLook || GaRadio.Open)
                return;
            if (Phase == GaHotPhase.Session)
                return;
            if (Phase == GaHotPhase.Brand || Phase == GaHotPhase.Link)
                return;
            if (Phase == GaHotPhase.Live)
            {
                Phase = GaHotPhase.Off;
                return;
            }
            if (Battery < 0.06f)
            {
                GaMode.Deny("HOTENSIA  low battery", "HOTENSIA 电量不足");
                return;
            }
            Enter(GaHotPhase.Brand);
        }

        [SkipLocalsInit]
        internal static void Draw()
        {
            if (!GaMode.Active || GaMode.Dead)
                return;
            Ensure();
            if (Phase == GaHotPhase.Session)
            {
                DrawSession();
                return;
            }
            if (Phase == GaHotPhase.Brand)
            {
            DrawVisor(0.72f, 0.55f,
                new Color(0.08f, 0.18f, 0.38f), new Color(0.22f, 0.48f, 0.95f));
            DrawBrand();
                return;
            }
            if (Phase == GaHotPhase.Link)
            {
            DrawVisor(0.72f, 0.55f,
                new Color(0.08f, 0.18f, 0.38f), new Color(0.22f, 0.48f, 0.95f));
            DrawLoadBar();
                return;
            }
            if (Phase != GaHotPhase.Live)
                return;
            DrawVisor(0.38f, 0.22f,
                new Color(0.08f, 0.18f, 0.38f), new Color(0.22f, 0.48f, 0.95f));
            DrawChrome();
            DrawBit();
            GaSpot.Draw();
        }

        private static void Enter(GaHotPhase p)
        {
            Phase = p;
            _phaseT = Time.unscaledTime;
        }

        private static float Age()
        {
            return Time.unscaledTime - _phaseT;
        }

        private static void Charge(float dt)
        {
            Battery += ChargePerSec * dt;
            if (Battery > 1f)
                Battery = 1f;
        }

        private static void TickSession()
        {
            float now = Time.unscaledTime;
            if (_boot == 0)
            {
                if (now < _nextGlyph)
                    return;
                _boot = 1;
                _typed = 0;
                _nextGlyph = now;
            }
            if (_boot == 1)
            {
                const string w = "INFANTRY";
                if (now < _nextGlyph)
                    return;
                if (_typed < w.Length)
                {
                    _typed++;
                    _l1 = w.Substring(0, _typed);
                    _nextGlyph = now + 0.07f;
                    return;
                }
                _boot = 2;
                _blinkLeft = 4;
                _cursorOn = true;
                _nextGlyph = now + 0.22f;
                return;
            }
            if (_boot == 2)
            {
                if (now < _nextGlyph)
                    return;
                _cursorOn = !_cursorOn;
                _blinkLeft--;
                _nextGlyph = now + 0.22f;
                if (_blinkLeft <= 0)
                {
                    _boot = 3;
                    _cursorOn = true;
                    _nextGlyph = now + 0.05f;
                }
                return;
            }
            if (_boot == 3)
            {
                if (now < _nextGlyph)
                    return;
                if (_l1.Length > 0)
                {
                    _l1 = _l1.Substring(0, _l1.Length - 1);
                    _nextGlyph = now + 0.045f;
                    return;
                }
                _boot = 4;
                _typed = 0;
                _nextGlyph = now + 0.08f;
                return;
            }
            if (_boot == 4)
            {
                const string w = "GROUND";
                if (now < _nextGlyph)
                    return;
                if (_typed < w.Length)
                {
                    _typed++;
                    _l1 = w.Substring(0, _typed);
                    _nextGlyph = now + 0.07f;
                    return;
                }
                _boot = 5;
                _typed = 0;
                _nextGlyph = now + 0.16f;
                return;
            }
            if (_boot == 5)
            {
                const string w = "ARSENAL";
                if (now < _nextGlyph)
                    return;
                if (_typed < w.Length)
                {
                    _typed++;
                    _l2 = w.Substring(0, _typed);
                    _nextGlyph = now + 0.07f;
                    return;
                }
                _boot = 6;
                _nextGlyph = now + 0.85f;
                _cursorOn = true;
                return;
            }
            if (now < _nextGlyph)
                return;
            _sessionDone = true;
            Phase = GaHotPhase.Off;
        }

        private static void TickBit()
        {
            if (Time.unscaledTime < _bitNext)
                return;
            _bitNext = Time.unscaledTime + 0.085f;
            string line;
            int k = 1;
            int m = _bitSeq % 11;
            if (m == 0)
                line = "         Starting HOTENSIA built-in test...";
            else if (m == 1)
                line = "[  OK  ] visor bus";
            else if (m == 2)
                line = "[  OK  ] optics / LOS grid 10 km";
            else if (m == 3)
                line = "[  OK  ] IFF crypto ring";
            else if (m == 4)
            {
                int hp = Mathf.RoundToInt(GaMode.Hp);
                k = hp < 40 ? 2 : 1;
                line = (k == 1 ? "[  OK  ] " : "[ WARN ] ") + "suit integrity " + hp.ToString();
            }
            else if (m == 5)
                line = "[  OK  ] servo " + Mathf.RoundToInt((1f - GaMode.ServoHeat()) * 100f).ToString() + "%";
            else if (m == 6)
            {
                int bat = Mathf.RoundToInt(Battery * 100f);
                k = bat < 22 ? 2 : 1;
                line = (k == 1 ? "[  OK  ] " : "[ WARN ] ") + "cell " + bat.ToString() + "%";
            }
            else if (m == 7)
                line = "[  OK  ] squad " + GaDropSquad.FriendsAlive.ToString() + " linked";
            else if (m == 8)
                line = "modprobe: inserting hotensia_scan.ko";
            else if (m == 9)
                line = "bitd: pass over helmet glass";
            else
                line = "[  OK  ] HOTENSIA ready";
            if (_bitN < Bit.Length)
            {
                Bit[_bitN] = line;
                BitK[_bitN] = k;
                _bitN++;
            }
            else
            {
                for (int i = 1; i < Bit.Length; i++)
                {
                    Bit[i - 1] = Bit[i];
                    BitK[i - 1] = BitK[i];
                }
                Bit[Bit.Length - 1] = line;
                BitK[Bit.Length - 1] = k;
            }
            _bitSeq++;
        }

        private static void DrawSession()
        {
            Color prev = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
            GUI.color = Color.white;
            float x = Screen.width * 0.12f;
            float y = Screen.height * 0.36f;
            GUI.Label(new Rect(x, y, 720f, 40f), _l1, _tty);
            GUI.Label(new Rect(x, y + 42f, 720f, 40f), _l2, _tty);
            bool show = _cursorOn;
            if (_boot == 1 || _boot == 4 || _boot == 5)
                show = ((int)(Time.unscaledTime * 3.6f) & 1) == 0;
            if (show)
            {
                string row = _boot >= 5 ? _l2 : _l1;
                float cy = _boot >= 5 ? y + 42f : y;
                float cx = x + row.Length * 19.2f;
                GUI.DrawTexture(new Rect(cx, cy + 6f, 14f, 26f), _px);
            }
            GUI.color = prev;
        }

        private static void DrawBrand()
        {
            float u = Age() / BrandSec;
            float a = 1f;
            if (u < 0.14f)
                a = u / 0.14f;
            else if (u > 0.78f)
                a = Mathf.Clamp01((1f - u) / 0.22f);
            LabelC(Screen.height * 0.38f, Brand, _brand, new Color(1f, 1f, 1f, a));
            LabelC(Screen.height * 0.48f, "HELMET OPTICS  /  TACTICAL ENHANCEMENT", _sub,
                new Color(0.55f, 0.82f, 1f, a * 0.92f));
        }

        private static void DrawLoadBar()
        {
            float prog = Mathf.Clamp01(Age() / LinkSec);
            LabelC(Screen.height * 0.32f, "LOADING HOTENSIA", _brand, new Color(0.55f, 0.82f, 1f, 0.96f));
            LabelC(Screen.height * 0.40f, "hotensia.service", _sub, new Color(0.45f, 0.72f, 0.95f, 0.9f));
            float bw = Screen.width * 0.42f;
            float bh = 16f;
            float bx = (Screen.width - bw) * 0.5f;
            float by = Screen.height * 0.50f;
            Color prev = GUI.color;
            GUI.color = new Color(0.04f, 0.10f, 0.18f, 0.88f);
            GUI.DrawTexture(new Rect(bx - 2f, by - 2f, bw + 4f, bh + 4f), _px);
            GUI.color = new Color(0.10f, 0.22f, 0.38f, 0.92f);
            GUI.DrawTexture(new Rect(bx, by, bw, bh), _px);
            GUI.color = new Color(0.35f, 0.72f, 1f, 0.96f);
            GUI.DrawTexture(new Rect(bx, by, bw * prog, bh), _px);
            GUI.color = prev;
            int pct = Mathf.RoundToInt(prog * 100f);
            LabelC(Screen.height * 0.50f + 22f, pct.ToString() + "%", _sub, new Color(0.75f, 0.9f, 1f, 1f));
            string step = "mounting visor bus";
            if (prog > 0.22f)
                step = "loading hotensia_scan.ko";
            if (prog > 0.40f)
                step = "loading iff ring";
            if (prog > 0.58f)
                step = "bring-up LOS grid 10 km";
            if (prog > 0.76f)
                step = "bringing datalink up";
            if (prog > 0.92f)
                step = "HOTENSIA ready";
            LabelC(Screen.height * 0.62f, step, _sub, new Color(0.5f, 0.82f, 1f, 0.92f));
        }

        internal static void DrawVisor(float veil, float tint, Color veilRgb, Color glassRgb)
        {
            Ensure();
            Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
            Color prev = GUI.color;
            GUI.color = new Color(glassRgb.r, glassRgb.g, glassRgb.b, tint * 0.14f + veil * 0.04f);
            GUI.DrawTexture(full, _px);
            GUI.color = new Color(veilRgb.r * 0.35f + glassRgb.r * 0.65f,
                veilRgb.g * 0.35f + glassRgb.g * 0.65f,
                veilRgb.b * 0.35f + glassRgb.b * 0.65f,
                Mathf.Max(tint, 0.62f));
            if (_glass != null)
                GUI.DrawTexture(full, _glass, ScaleMode.StretchToFill);
            GUI.color = new Color(1f, 1f, 1f, 0.34f);
            if (_spec != null)
                GUI.DrawTexture(full, _spec, ScaleMode.StretchToFill);
            GUI.color = prev;
            DrawVisorArc(full, new Color(glassRgb.r, glassRgb.g, glassRgb.b, 0.92f), 0f);
            DrawVisorArc(full, new Color(glassRgb.r, glassRgb.g, glassRgb.b, 0.42f), 0.014f);
        }

        private static void DrawChrome()
        {
            Color prev = GUI.color;
            GUI.color = new Color(0.55f, 0.85f, 1f, 0.92f);
            GUI.Label(new Rect(22f, 10f, 420f, 20f), Brand, _ok);
            int bat = Mathf.RoundToInt(Battery * 100f);
            string cell = "CELL  " + bat.ToString() + "%   X stow";
            GUI.Label(new Rect(Screen.width - 240f, 10f, 220f, 20f), cell, _ok);
            float bx = Screen.width - 236f;
            GUI.color = new Color(0.04f, 0.08f, 0.14f, 0.8f);
            GUI.DrawTexture(new Rect(bx, 32f, 200f, 8f), _px);
            GUI.color = bat < 22
                ? new Color(1f, 0.35f, 0.2f, 0.95f)
                : new Color(0.35f, 0.78f, 1f, 0.95f);
            GUI.DrawTexture(new Rect(bx, 32f, 200f * Battery, 8f), _px);
            GUI.color = prev;
        }

        private static void DrawBit()
        {
            float w = 280f;
            float x = 16f;
            float y = 52f;
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.42f);
            GUI.DrawTexture(new Rect(x, y, w, _bitN * 13f + 8f), _px);
            GUI.color = Color.white;
            for (int i = 0; i < _bitN; i++)
            {
                GUIStyle st = BitK[i] == 2 ? _warn : _ok;
                GUI.Label(new Rect(x + 6f, y + 4f + i * 13f, w - 10f, 13f), Bit[i], st);
            }
            GUI.color = prev;
        }

        private static void LabelC(float y, string s, GUIStyle st, Color col)
        {
            Color prev = GUI.color;
            GUI.color = col;
            GUI.Label(new Rect(0f, y, Screen.width, 48f), s, st);
            GUI.color = prev;
        }

        private static void Ensure()
        {
            if (_px == null)
            {
                _px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _px.SetPixel(0, 0, Color.white);
                _px.Apply();
            }
            if (_glass == null)
                BuildGlass();
            if (_styles)
                return;
            _styles = true;
            _tty = new GUIStyle(GUI.skin.label);
            _tty.fontSize = 28;
            _tty.fontStyle = FontStyle.Bold;
            _tty.alignment = TextAnchor.MiddleLeft;
            _tty.normal.textColor = Color.white;
            _brand = new GUIStyle(GUI.skin.label);
            _brand.fontSize = 36;
            _brand.fontStyle = FontStyle.Bold;
            _brand.alignment = TextAnchor.MiddleCenter;
            _brand.normal.textColor = Color.white;
            _sub = new GUIStyle(GUI.skin.label);
            _sub.fontSize = 14;
            _sub.fontStyle = FontStyle.Bold;
            _sub.alignment = TextAnchor.MiddleCenter;
            _sub.normal.textColor = new Color(0.7f, 0.88f, 1f);
            _bit = new GUIStyle(GUI.skin.label);
            _bit.fontSize = 11;
            _bit.fontStyle = FontStyle.Bold;
            _bit.alignment = TextAnchor.MiddleLeft;
            _ok = new GUIStyle(_bit);
            _ok.normal.textColor = new Color(0.45f, 0.85f, 1f, 0.95f);
            _warn = new GUIStyle(_bit);
            _warn.normal.textColor = new Color(1f, 0.78f, 0.22f, 0.96f);
        }

        private static void BuildGlass()
        {
            int n = 256;
            _glass = new Texture2D(n, n, TextureFormat.ARGB32, false);
            _glass.wrapMode = TextureWrapMode.Clamp;
            _glass.filterMode = FilterMode.Bilinear;
            _spec = new Texture2D(n, n, TextureFormat.ARGB32, false);
            _spec.wrapMode = TextureWrapMode.Clamp;
            _spec.filterMode = FilterMode.Bilinear;
            Color[] g = new Color[n * n];
            Color[] s = new Color[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float nx = (x - c) / c;
                    float ny = (y - c) / c;
                    float se = VisorSdf(nx, ny);
                    float a;
                    if (se >= 1.03f)
                        a = 0.92f;
                    else if (se >= 0.86f)
                        a = Mathf.Lerp(0f, 0.88f, Mathf.InverseLerp(0.86f, 1.03f, se));
                    else
                        a = 0f;
                    g[y * n + x] = new Color(1f, 1f, 1f, a);
                    float hx = nx + 0.28f;
                    float hy = ny - 0.38f;
                    float spec = Mathf.Exp(-(hx * hx + hy * hy) * 7.5f);
                    float rim = 0f;
                    float dRim = Mathf.Abs(se - 1f);
                    if (dRim < 0.045f)
                        rim = 1f - dRim / 0.045f;
                    s[y * n + x] = new Color(0.85f, 0.94f, 1f, spec * 0.48f + rim * 0.55f);
                }
            }
            _glass.SetPixels(g);
            _glass.Apply(false, true);
            _spec.SetPixels(s);
            _spec.Apply(false, true);
        }

        private static float VisorSdf(float nx, float ny)
        {
            const float pinch = 0.28f;
            float wx = 0.948f * (1f + pinch * ny * ny);
            float hy = 0.938f * (1f + pinch * nx * nx);
            float ax = Mathf.Abs(nx) / wx;
            float ay = Mathf.Abs(ny) / hy;
            const float p = 3.2f;
            return Mathf.Pow(Mathf.Pow(ax, p) + Mathf.Pow(ay, p), 1f / p);
        }

        private static void DrawVisorArc(Rect r, Color c, float inset)
        {
            if (!BeginGl())
                return;
            const int n = 72;
            const float pinch = 0.28f;
            const float p = 3.2f;
            float exp = 2f / p;
            float shrink = 1f - inset * 2f;
            if (shrink < 0.4f)
                shrink = 0.4f;
            float px = 0f;
            float py = 0f;
            for (int i = 0; i <= n; i++)
            {
                float t = (i % n) / (float)n * Mathf.PI * 2f;
                float ct = Mathf.Cos(t);
                float st = Mathf.Sin(t);
                float wx = 0.948f * (1f + pinch * st * st) * shrink;
                float hy = 0.938f * (1f + pinch * ct * ct) * shrink;
                float nx = wx * Mathf.Sign(ct) * Mathf.Pow(Mathf.Abs(ct), exp);
                float ny = hy * Mathf.Sign(st) * Mathf.Pow(Mathf.Abs(st), exp);
                float x = r.x + (nx * 0.5f + 0.5f) * r.width;
                float y = r.y + (ny * 0.5f + 0.5f) * r.height;
                if (i > 0)
                {
                    GL.Color(c);
                    GL.Vertex3(px, py, 0f);
                    GL.Vertex3(x, y, 0f);
                }
                px = x;
                py = y;
            }
            EndGl();
        }

        private static bool BeginGl()
        {
            if (_gl == null)
            {
                Shader sh = Shader.Find("Hidden/Internal-Colored");
                if (sh == null)
                    sh = Shader.Find("Sprites/Default");
                if (sh == null)
                    return false;
                _gl = new Material(sh);
                _gl.hideFlags = HideFlags.HideAndDontSave;
                _gl.SetInt("_ZWrite", 0);
                _gl.SetInt("_ZTest", 8);
                _gl.SetInt("_Cull", 0);
            }
            GL.PushMatrix();
            _gl.SetPass(0);
            GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
            GL.Begin(GL.LINES);
            return true;
        }

        private static void EndGl()
        {
            GL.End();
            GL.PopMatrix();
        }
    }
}
