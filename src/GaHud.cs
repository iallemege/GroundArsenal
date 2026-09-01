using UnityEngine;

namespace GroundArsenal
{
    internal static class GaHud
    {
        internal static bool ArsenalOpen;
        internal static bool BlocksLook
        {
            get { return ArsenalOpen; }
        }

        private static GUIStyle _label;
        private static GUIStyle _small;
        private static GUIStyle _btn;
        private static GUIStyle _btnOn;
        private static Texture2D _px;
        private static Vector2 _scroll;
        private static Rect _win = new Rect(40f, 40f, 640f, 520f);
        private static int _filterFam = -1;
        private static bool _styles;

        internal static void Tick()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                ArsenalOpen = !ArsenalOpen;
                Cursor.lockState = ArsenalOpen ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = ArsenalOpen;
            }
            if (Input.GetKeyDown(KeyCode.Escape) && ArsenalOpen)
            {
                ArsenalOpen = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        internal static void CloseMenus()
        {
            ArsenalOpen = false;
        }

        internal static void Draw()
        {
            Ensure();
            GaGun g = GaCombat.Current;
            float hp = GaMode.Hp / GaMode.MaxHp;
            GUI.color = new Color(0.8f, 0.15f, 0.12f, 0.85f);
            GUI.DrawTexture(new Rect(18f, Screen.height - 28f, 180f * hp, 12f), _px);
            GUI.color = Color.white;
            string n = g != null ? GaCatalog.Name(g, GaMode.Zh) : "?";
            string ammo = "";
            if (g != null)
                ammo = GaCombat.Mag[g.Id].ToString() + " / " + GaCombat.Reserve[g.Id].ToString();
            string line = (GaCombat.Slot + 1).ToString() + "  " + n + "  " + ammo;
            GUI.Label(new Rect(18f, Screen.height - 52f, 720f, 22f), line, _label);
            string fam = "";
            if (g != null)
            {
                fam = GaMode.Zh
                    ? GaCatalog.FamilyZh[g.Family] + "  " + GaCatalog.FireName(g.Fire, true)
                    : GaCatalog.FamilyEn[g.Family] + "  " + GaCatalog.FireName(g.Fire, false);
            }
            GUI.Label(new Rect(18f, Screen.height - 74f, 720f, 20f), fam, _small);
            float x = 18f;
            for (int i = 0; i < GaCombat.Hotbar.Length; i++)
            {
                GaGun hg = GaCatalog.Get(GaCombat.Hotbar[i]);
                string t = (i + 1).ToString();
                GUI.color = i == GaCombat.Slot ? new Color(1f, 0.85f, 0.2f) : Color.white;
                GUI.Label(new Rect(x, Screen.height - 98f, 70f, 18f), t, _small);
                GUI.color = Color.white;
                x += 22f;
            }
            GUI.Label(new Rect(18f, 12f, 720f, 20f),
                GaMode.Zh
                    ? "G 武库   1-9 / 滚轮换枪   U 上下机   WASD 移动"
                    : "G arsenal   1-9 / wheel   U board   WASD move",
                _small);
            if (Time.unscaledTime < GaMode.DenyUntil && !string.IsNullOrEmpty(GaMode.DenyMsg))
                GUI.Label(new Rect(18f, 36f, 720f, 22f), GaMode.DenyMsg, _label);
            if (ArsenalOpen)
            {
                _win = GUI.Window(0x47415253, _win, DrawArsenal,
                    GaMode.Zh ? "Ground Arsenal — 300 武器" : "Ground Arsenal — 300 guns");
            }
        }

        private static void DrawArsenal(int id)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GaMode.Zh ? "全部" : "All",
                _filterFam < 0 ? _btnOn : _btn, GUILayout.Width(64f)))
                _filterFam = -1;
            for (int f = 0; f < GaCatalog.FamilyCount; f++)
            {
                if (GUILayout.Button(GaMode.Zh ? GaCatalog.FamilyZh[f] : GaCatalog.FamilyEn[f],
                    _filterFam == f ? _btnOn : _btn, GUILayout.Height(22f)))
                    _filterFam = f;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(GaMode.Zh
                ? "点击把枪放进当前热键槽 " + (GaCombat.Slot + 1).ToString()
                : "Click to put a gun in hotbar slot " + (GaCombat.Slot + 1).ToString(),
                _small);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(400f));
            for (int i = 0; i < GaCatalog.Count; i++)
            {
                GaGun g = GaCatalog.All[i];
                if (_filterFam >= 0 && g.Family != _filterFam)
                    continue;
                bool on = false;
                for (int h = 0; h < GaCombat.Hotbar.Length; h++)
                {
                    if (GaCombat.Hotbar[h] == g.Id)
                        on = true;
                }
                string row = (g.Id + 1).ToString("000") + "  " + GaCatalog.Name(g, GaMode.Zh)
                    + "  " + GaCatalog.FireName(g.Fire, GaMode.Zh)
                    + "  MAG " + g.Mag.ToString()
                    + "  DMG " + Mathf.RoundToInt(g.Damage).ToString()
                    + "  PEN " + Mathf.RoundToInt(g.Pen).ToString();
                if (GUILayout.Button(row, on ? _btnOn : _btn, GUILayout.Height(22f)))
                {
                    GaCombat.Hotbar[GaCombat.Slot] = g.Id;
                    GaCombat.Mag[g.Id] = g.Mag;
                    GaCombat.Reserve[g.Id] = g.Reserve;
                    GaCombat.SavePrefs();
                }
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private static void Ensure()
        {
            if (_styles)
                return;
            _styles = true;
            _label = new GUIStyle(GUI.skin.label);
            _label.fontSize = 16;
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label);
            _small.fontSize = 13;
            _small.normal.textColor = new Color(0.85f, 0.9f, 0.95f);
            _btn = new GUIStyle(GUI.skin.button);
            _btn.fontSize = 12;
            _btn.alignment = TextAnchor.MiddleLeft;
            _btnOn = new GUIStyle(_btn);
            _btnOn.normal.textColor = new Color(1f, 0.9f, 0.3f);
            _btnOn.fontStyle = FontStyle.Bold;
            _px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }
    }
}
