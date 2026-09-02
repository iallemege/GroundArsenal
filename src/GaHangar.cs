using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GroundArsenal
{
    internal static class GaHangar
    {
        private static readonly FieldInfo AirbaseField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "airbase");
        private static readonly MethodInfo HideSelection =
            AccessTools.Method(typeof(AircraftSelectionMenu), "HideSelection");
        private static readonly FieldInfo GameplaySelMenu =
            AccessTools.Field(typeof(GameplayUI), "aircraftSelectionMenu");
        private static readonly FieldInfo JoinMenuField =
            AccessTools.Field(typeof(GameplayUI), "joinMenu");

        private static Rect _win = new Rect(18f, 80f, 508f, 560f);
        private static GUIStyle _body;
        private static GUIStyle _btn;
        private static GUIStyle _btnOn;
        private static bool _styles;
        private static Vector2 _scroll;
        private static int _pickSlot;
        private static bool _docked;

        /// <summary>
        /// Aircraft-select in a hangar, not the faction join screen and not cockpit/map.
        /// </summary>
        internal static bool HangarSelectOpen()
        {
            try
            {
                if (!CursorManager.GetFlag(CursorFlags.SelectionMenu))
                    return false;
            }
            catch
            {
                return false;
            }
            try
            {
                GameplayUI ui = SceneSingleton<GameplayUI>.i;
                if (ui != null && JoinMenuField != null)
                {
                    GameObject join = JoinMenuField.GetValue(ui) as GameObject;
                    if (join != null && join.activeInHierarchy)
                        return false;
                }
            }
            catch { }
            return true;
        }

        internal static void TickKeys()
        {
            if (!HangarSelectOpen() || GaMode.Active)
                return;
            int n = GaCombat.Hotbar.Length;
            for (int i = 0; i < n; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    _pickSlot = i;
                    return;
                }
            }
        }

        internal static void Draw()
        {
            if (GaMode.Active)
                return;
            if (!HangarSelectOpen())
            {
                _docked = false;
                return;
            }
            Ensure();
            if (!_docked)
            {
                _docked = true;
                float w = Screen.width * 0.42f;
                if (w < 620f)
                    w = 620f;
                if (w > 760f)
                    w = 760f;
                if (w > Screen.width - 24f)
                    w = Screen.width - 24f;
                float h = Screen.height - 40f;
                if (h > 900f)
                    h = 900f;
                if (h < 560f)
                    h = 560f;
                _win = new Rect(12f, 20f, w, h);
            }
            if (_win.width < 580f)
                _win.width = 580f;
            if (_win.height < 520f)
                _win.height = 520f;
            _win = GUI.Window(0x47414847, _win, DrawWindow, "Ground Arsenal infantry");
            if (GaHud.CatalogOpen)
                ArmorCatalogService.Draw();
        }

        private static void DrawWindow(int id)
        {
            try
            {
                DrawWindowBody();
            }
            catch (Exception ex)
            {
                GUILayout.Label("Infantry menu failed: " + ex.Message);
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private static void DrawWindowBody()
        {
            GaIcons.Ensure();
            GaCombat.LoadPrefs();
            GUILayout.Label(
                GaCatalog.Count.ToString()
                    + " modeled guns. Rank "
                    + GaCombat.PlayerRank().ToString()
                    + ". Keys 1-9 pick slot. Slot 1 is carry-on. Quote (') = AP table.",
                _body);
            GUILayout.Label("Hotbar slot (1-9):", _body);
            GUILayout.BeginHorizontal();
            for (int s = 0; s < GaCombat.Hotbar.Length; s++)
            {
                if (s > 0 && (s % 3) == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                string lab = (s + 1).ToString();
                if (s == 0)
                    lab = "1 CARRY";
                if (GUILayout.Button(lab, s == _pickSlot ? _btnOn : _btn, GUILayout.Height(32f)))
                    _pickSlot = s;
            }
            GUILayout.EndHorizontal();
            GaGun cur = GaCatalog.Get(GaCombat.Hotbar[_pickSlot]);
            GUILayout.Label("Slot " + (_pickSlot + 1).ToString() + ": " + GaCatalog.Name(cur, false)
                + "  PEN " + Mathf.RoundToInt(cur.Pen).ToString()
                + "  " + GaCatalog.CostM(cur).ToString("0.00") + "M", _body);
            float listH = _win.height - 340f;
            if (listH < 220f)
                listH = 220f;
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(listH));
            for (int i = 0; i < GaCatalog.Count; i++)
            {
                GaGun g = GaCatalog.All[i];
                bool on = false;
                for (int h = 0; h < GaCombat.Hotbar.Length; h++)
                {
                    if (GaCombat.Hotbar[h] == g.Id)
                        on = true;
                }
                Rect row = GUILayoutUtility.GetRect(1f, 58f, GUILayout.ExpandWidth(true));
                Texture2D ic = GaIcons.Get(g);
                if (ic != null)
                    GUI.DrawTexture(new Rect(row.x, row.y + 4f, 88f, 50f), ic, ScaleMode.ScaleToFit);
                string tag = "";
                if (!GaCombat.Unlocked(g))
                    tag = "  LOCK RANK " + g.Rank.ToString();
                else if (GaCombat.Hotbar[0] == g.Id)
                    tag = "  [CARRY]";
                else if (on)
                    tag = "  [HOTBAR]";
                string label = g.En + "  " + GaCatalog.CalLabel(g) + "  PEN " + Mathf.RoundToInt(g.Pen).ToString()
                    + "  " + GaCatalog.CostM(g).ToString("0.00") + "M" + tag;
                if (GUI.Button(new Rect(row.x + 92f, row.y, row.width - 92f, 56f), label, on ? _btnOn : _btn))
                {
                    if (!GaCombat.Unlocked(g))
                        GaMode.Deny("Rank " + g.Rank.ToString() + " required.", "Rank " + g.Rank.ToString() + " required.");
                    else
                    {
                        GaCombat.Hotbar[_pickSlot] = g.Id;
                        GaCombat.GiveGunAmmo(g);
                        GaCombat.SavePrefs();
                    }
                }
            }
            GUILayout.EndScrollView();
            float kitCost = GaCombat.KitCostM();
            float funds = GaFunds.Read();
            string dep = "DEPLOY  " + kitCost.ToString("0.00") + "M   funds " + funds.ToString("0.0") + "M";
            if (funds + 0.001f < kitCost)
                dep = "NEED " + kitCost.ToString("0.00") + "M  (have " + funds.ToString("0.0") + "M)";
            if (GUILayout.Button(dep, GUILayout.Height(44f)))
            {
                GaCombat.SavePrefs();
                if (!GaConfig.IsEnabled())
                    GaMode.Deny("Turn infantry ON first.", "Turn infantry ON first.");
                else
                    GaMode.HangarQueued = true;
            }
            if (GUILayout.Button(GaConfig.IsEnabled() ? "Infantry ENABLED  (click to disable)" : "Infantry DISABLED  (click to enable)",
                GUILayout.Height(32f)))
                GaConfig.SetEnabled(!GaConfig.IsEnabled());
            if (GUILayout.Button("Armor table", GUILayout.Height(32f)))
            {
                ArmorCatalogService.EnsureScanned();
                GaHud.CatalogOpen = true;
            }
            if (GaConfig.WantPerf())
            {
                float fps = Time.unscaledDeltaTime > 0.0001f ? (1f / Time.unscaledDeltaTime) : 0f;
                GUILayout.Label("Standalone " + GaInfo.Version + "   "
                    + Mathf.RoundToInt(fps).ToString() + " fps   sparks "
                    + GaFx.Live.ToString()
                    + (GaOritasy.HasModularArmor ? "   Oritasy AP present" : "   GA AP"), _body);
            }
        }

        internal static bool TrySpawn(out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero;
            yaw = 0f;
            Hangar hangar = FindHangar();
            if (hangar == null)
                return false;
            Transform t = null;
            try { t = hangar.GetSpawnTransform(); }
            catch { t = null; }
            if (t == null)
            {
                try { t = hangar.transform; }
                catch { t = null; }
            }
            if (t == null)
                return false;
            pos = t.position;
            yaw = t.eulerAngles.y;
            Vector3 start = pos + Vector3.up * 12f;
            RaycastHit hit;
            if (Physics.Raycast(start, Vector3.down, out hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                pos = hit.point + Vector3.up * 0.08f;
            else
                pos = pos + Vector3.up * 0.2f;
            return true;
        }

        internal static void CloseUi()
        {
            AircraftSelectionMenu menu = UnityEngine.Object.FindObjectOfType<AircraftSelectionMenu>();
            if (menu != null)
            {
                try
                {
                    if (HideSelection != null)
                        HideSelection.Invoke(menu, null);
                }
                catch { }
                try { menu.gameObject.SetActive(false); }
                catch { }
            }
            try
            {
                GameplayUI ui = SceneSingleton<GameplayUI>.i;
                if (ui != null && GameplaySelMenu != null)
                {
                    GameObject go = GameplaySelMenu.GetValue(ui) as GameObject;
                    if (go != null)
                        go.SetActive(false);
                }
            }
            catch { }
            try { CursorManager.SetFlag(CursorFlags.SelectionMenu, false); }
            catch { }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static Hangar FindHangar()
        {
            Airbase ab = CurrentAirbase();
            if (ab != null)
            {
                Hangar[] hs = null;
                try { hs = ab.GetComponentsInChildren<Hangar>(true); }
                catch { hs = null; }
                if (hs != null)
                {
                    for (int i = 0; i < hs.Length; i++)
                    {
                        if (hs[i] != null && !hs[i].Disabled)
                            return hs[i];
                    }
                }
            }
            Hangar[] all = UnityEngine.Object.FindObjectsOfType<Hangar>();
            if (all == null || all.Length == 0)
                return null;
            Camera cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            Hangar best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < all.Length; i++)
            {
                Hangar h = all[i];
                if (h == null)
                    continue;
                try
                {
                    if (h.Disabled)
                        continue;
                }
                catch { }
                float d = (h.transform.position - camPos).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = h;
                }
            }
            return best;
        }

        private static Airbase CurrentAirbase()
        {
            AircraftSelectionMenu menu = UnityEngine.Object.FindObjectOfType<AircraftSelectionMenu>();
            if (menu != null && AirbaseField != null)
            {
                try
                {
                    Airbase ab = AirbaseField.GetValue(menu) as Airbase;
                    if (ab != null)
                        return ab;
                }
                catch { }
            }
            return null;
        }

        private static void Ensure()
        {
            if (_styles)
                return;
            _styles = true;
            _body = new GUIStyle(GUI.skin.label);
            _body.fontSize = 15;
            _body.wordWrap = true;
            _body.normal.textColor = Color.white;
            _btn = new GUIStyle(GUI.skin.button);
            _btn.fontSize = 14;
            _btn.wordWrap = true;
            _btn.alignment = TextAnchor.MiddleLeft;
            _btn.clipping = TextClipping.Overflow;
            _btnOn = new GUIStyle(_btn);
            _btnOn.fontStyle = FontStyle.Bold;
            _btnOn.normal.textColor = new Color(1f, 0.9f, 0.3f);
        }
    }
}
