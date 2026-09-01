using UnityEngine;

namespace GroundArsenal
{
    internal static class GaHud
    {
        internal static bool ArsenalOpen;
        internal static bool CatalogOpen;
        internal static bool BlocksLook
        {
            get { return ArsenalOpen || CatalogOpen; }
        }

        private static GUIStyle _label;
        private static GUIStyle _small;
        private static GUIStyle _hud;
        private static GUIStyle _tiny;
        private static GUIStyle _btn;
        private static GUIStyle _btnOn;
        private static Texture2D _px;
        private static Vector2 _scroll;
        private static Rect _win = new Rect(40f, 40f, 720f, 560f);
        private static int _filterFam = -1;
        private static bool _styles;
        private static string _flash;
        private static float _flashUntil;

        internal static void FlashHit(string msg)
        {
            _flash = msg;
            _flashUntil = Time.unscaledTime + 1.8f;
        }

        internal static void Tick()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                ArsenalOpen = !ArsenalOpen;
                if (ArsenalOpen)
                    CatalogOpen = false;
                Cursor.lockState = BlocksLook ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = BlocksLook;
            }
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Quote))
            {
                CatalogOpen = !CatalogOpen;
                if (CatalogOpen)
                {
                    ArsenalOpen = false;
                    ArmorCatalogService.EnsureScanned();
                }
                Cursor.lockState = BlocksLook ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = BlocksLook;
            }
            if (Input.GetKeyDown(KeyCode.Escape) && BlocksLook)
            {
                CloseMenus();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        internal static void CloseMenus()
        {
            ArsenalOpen = false;
            CatalogOpen = false;
        }

        internal static void Draw()
        {
            Ensure();
            DrawSuitPanel();
            DrawSysRail();
            DrawHelmetReticle();
            DrawWeaponBar();
            DrawPerf();
            if (Time.unscaledTime < _flashUntil && !string.IsNullOrEmpty(_flash))
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.38f, 440f, 28f), _flash, _label);
            if (Time.unscaledTime < GaMode.DenyUntil && !string.IsNullOrEmpty(GaMode.DenyMsg))
                GUI.Label(new Rect(18f, 176f, 720f, 22f), GaMode.DenyMsg, _label);
            if (ArsenalOpen)
            {
                _win = GUI.Window(0x47415253, _win, DrawArsenal,
                    "Weapons  (" + GaCatalog.Count.ToString() + " modeled)");
            }
            if (CatalogOpen)
                ArmorCatalogService.Draw();
        }

        private static void DrawSuitPanel()
        {
            float x = 16f;
            float y = 36f;
            float w = 220f;
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.07f, 0.08f, 0.72f);
            GUI.DrawTexture(new Rect(x, y, w, 132f), _px);
            GUI.color = new Color(0.25f, 0.95f, 0.82f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, w, 2f), _px);
            GUI.DrawTexture(new Rect(x, y, 2f, 132f), _px);
            GUI.color = new Color(0.35f, 1f, 0.88f, 0.95f);
            GUI.Label(new Rect(x + 8f, y + 4f, w - 12f, 20f), "EXO-10  POWERED SUIT", _hud);
            float hp = GaMode.Hp / GaMode.MaxHp;
            if (hp < 0f) hp = 0f;
            if (hp > 1f) hp = 1f;
            DrawBar(x + 8f, y + 24f, w - 16f, hp,
                "INTEGRITY  " + Mathf.RoundToInt(GaMode.Hp) + "/" + Mathf.RoundToInt(GaMode.MaxHp),
                hp < 0.35f ? new Color(1f, 0.28f, 0.18f) : new Color(0.3f, 1f, 0.55f));
            float fat = GaMode.ArmorFatigue;
            DrawBar(x + 8f, y + 48f, w - 16f, 1f - fat,
                "PLATE " + GaMode.OuterEffective() + "  FAT " + Mathf.RoundToInt(fat * 100f) + "%",
                fat > 0.55f ? new Color(1f, 0.7f, 0.2f) : new Color(0.45f, 0.85f, 1f));
            float heat = GaMode.ServoHeat();
            DrawBar(x + 8f, y + 72f, w - 16f, heat,
                "SERVO  " + Mathf.RoundToInt(heat * 100f) + "%",
                heat >= 0.72f ? new Color(1f, 0.4f, 0.1f) : new Color(0.95f, 0.8f, 0.25f));
            GUI.color = new Color(0.65f, 0.9f, 0.85f, 0.85f);
            GUI.Label(new Rect(x + 8f, y + 100f, w - 16f, 22f),
                "SPD " + GaMode.MoveSpeed.ToString("0.0") + " m/s   PWR "
                    + Mathf.RoundToInt(GaMode.SprintFill() * 100f) + "%",
                _tiny);
            GUI.color = prev;
        }

        private static void DrawSysRail()
        {
            float w = 176f;
            float x = Screen.width - w - 18f;
            float y = 36f;
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.07f, 0.08f, 0.72f);
            GUI.DrawTexture(new Rect(x, y, w, 108f), _px);
            GUI.color = new Color(0.25f, 0.95f, 0.82f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, w, 2f), _px);
            GUI.color = new Color(0.35f, 1f, 0.88f, 0.95f);
            GUI.Label(new Rect(x + 8f, y + 4f, w - 12f, 20f), "SUIT BUS", _hud);
            GUI.color = new Color(0.6f, 0.95f, 0.85f, 0.9f);
            GUI.Label(new Rect(x + 8f, y + 26f, w - 12f, 18f), "LINK  ACTIVE", _tiny);
            GUI.Label(new Rect(x + 8f, y + 46f, w - 12f, 18f), "OPTICS  STAB", _tiny);
            GUI.Label(new Rect(x + 8f, y + 66f, w - 12f, 18f),
                "SERVO  " + (GaMode.ServoHeat() >= 0.72f ? "LIMIT" : "READY"), _tiny);
            GUI.Label(new Rect(x + 8f, y + 86f, w - 12f, 18f),
                "PLATE  " + GaMode.OuterEffective() + " / " + GaMode.InnerArmor, _tiny);
            GUI.color = prev;
        }

        private static void DrawHelmetReticle()
        {
            if (GaCombat.Ads >= 0.22f)
                return;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color prev = GUI.color;
            GUI.color = new Color(0.25f, 0.95f, 0.82f, 0.72f);
            GUI.DrawTexture(new Rect(cx - 14f, cy - 1f, 9f, 2f), _px);
            GUI.DrawTexture(new Rect(cx + 5f, cy - 1f, 9f, 2f), _px);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 14f, 2f, 9f), _px);
            GUI.DrawTexture(new Rect(cx - 1f, cy + 5f, 2f, 9f), _px);
            RaycastHit hit;
            float rng = 0f;
            if (Physics.Raycast(GaMode.EyePos, GaMode.EyeFwd, out hit, 2400f, ~0, QueryTriggerInteraction.Ignore))
                rng = hit.distance;
            GUI.color = new Color(0.45f, 1f, 0.88f, 0.75f);
            GUI.Label(new Rect(cx - 40f, cy + 26f, 80f, 14f),
                rng > 0.4f ? ("RNG " + Mathf.RoundToInt(rng).ToString() + "m") : "RNG ----",
                _tiny);
            GUI.color = prev;
        }

        private static void DrawWeaponBar()
        {
            GaGun g = GaCombat.Current;
            string n = g != null ? g.En : "?";
            string ammo = "";
            if (g != null)
                ammo = GaCombat.Mag[g.Id].ToString() + " / " + GaCombat.Reserve[g.Id].ToString();
            GUI.color = Color.white;
            GUI.Label(new Rect(18f, Screen.height - 52f, 720f, 22f),
                (GaCombat.Slot + 1).ToString() + "  " + n + "  " + ammo, _label);
            if (g != null)
            {
                GUI.Label(new Rect(18f, Screen.height - 74f, 720f, 20f),
                    g.Kind + "  PEN " + Mathf.RoundToInt(g.Pen).ToString()
                        + "  KE " + Mathf.RoundToInt(GaCombat.Kinetic(g)).ToString()
                        + "  " + GaCatalog.CostM(g).ToString("0.00") + "M"
                        + "  " + GaCatalog.FireName(g.Fire, false), _small);
            }
            float x = 18f;
            for (int i = 0; i < GaCombat.Hotbar.Length; i++)
            {
                GUI.color = i == GaCombat.Slot ? new Color(1f, 0.85f, 0.2f) : Color.white;
                GUI.Label(new Rect(x, Screen.height - 98f, 70f, 18f), (i + 1).ToString(), _small);
                x += 22f;
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(18f, 12f, 900f, 20f),
                "G arsenal   ' armor table   1-9 / wheel   U board   Shift sprint   Space jump", _small);
        }

        private static void DrawPerf()
        {
            if (!GaConfig.WantPerf())
                return;
            Ensure();
            float fps = Time.unscaledDeltaTime > 0.0001f ? (1f / Time.unscaledDeltaTime) : 0f;
            string line = "GA " + GaInfo.Version
                + "  " + Mathf.RoundToInt(fps).ToString() + " fps"
                + "  sparks " + GaFx.Live
                + "  standalone"
                + (GaOritasy.HasModularArmor ? "  Oritasy-AP" : "  GA-AP");
            GUI.color = new Color(0.55f, 1f, 0.75f, 0.85f);
            GUI.Label(new Rect(18f, Screen.height - 118f, 720f, 18f), line, _tiny);
            GUI.color = Color.white;
        }

        private static void DrawBar(float x, float y, float w, float fill, string label, Color col)
        {
            if (fill < 0f) fill = 0f;
            if (fill > 1f) fill = 1f;
            Color prev = GUI.color;
            GUI.color = new Color(0.08f, 0.1f, 0.1f, 0.85f);
            GUI.DrawTexture(new Rect(x, y + 14f, w, 6f), _px);
            GUI.color = col;
            GUI.DrawTexture(new Rect(x, y + 14f, w * fill, 6f), _px);
            GUI.color = new Color(0.8f, 0.95f, 0.9f, 0.9f);
            GUI.Label(new Rect(x, y, w, 18f), label, _tiny);
            GUI.color = prev;
        }

        private static void DrawArsenal(int id)
        {
            GaIcons.Ensure();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("All", _filterFam < 0 ? _btnOn : _btn, GUILayout.Width(64f)))
                _filterFam = -1;
            for (int f = 0; f < GaCatalog.FamilyCount; f++)
            {
                if (!GaCatalog.FamilyHas(f))
                    continue;
                if (GUILayout.Button(GaCatalog.FamilyEn[f],
                    _filterFam == f ? _btnOn : _btn, GUILayout.Height(22f)))
                    _filterFam = f;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Click a picture to put that modeled gun on hotbar slot "
                + (GaCombat.Slot + 1).ToString(), _small);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(420f));
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
                Rect row = GUILayoutUtility.GetRect(1f, 52f, GUILayout.ExpandWidth(true));
                Texture2D ic = GaIcons.Get(g);
                if (ic != null)
                    GUI.DrawTexture(new Rect(row.x, row.y + 2f, 96f, 48f), ic, ScaleMode.ScaleToFit);
                string label = g.En + "  " + g.Kind + "  PEN " + Mathf.RoundToInt(g.Pen).ToString()
                    + "  " + GaCatalog.CostM(g).ToString("0.00") + "M"
                    + "  " + GaCatalog.FireName(g.Fire, false)
                    + "  MAG " + g.Mag.ToString();
                if (GUI.Button(new Rect(row.x + 100f, row.y, row.width - 100f, 50f),
                    label, on ? _btnOn : _btn))
                {
                    GaCombat.Hotbar[GaCombat.Slot] = g.Id;
                    GaCombat.Mag[g.Id] = g.Mag;
                    GaCombat.Reserve[g.Id] = g.Reserve;
                    GaCombat.SavePrefs();
                    GaCombat.RefreshView();
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
            _label.alignment = TextAnchor.MiddleCenter;
            _small = new GUIStyle(GUI.skin.label);
            _small.fontSize = 13;
            _small.normal.textColor = new Color(0.85f, 0.9f, 0.95f);
            _hud = new GUIStyle(GUI.skin.label);
            _hud.fontSize = 13;
            _hud.fontStyle = FontStyle.Bold;
            _hud.normal.textColor = new Color(0.35f, 1f, 0.88f);
            _tiny = new GUIStyle(GUI.skin.label);
            _tiny.fontSize = 11;
            _tiny.normal.textColor = new Color(0.7f, 0.95f, 0.88f);
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
