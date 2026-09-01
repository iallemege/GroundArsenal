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

        private static Rect _win = new Rect(18f, 110f, 420f, 280f);
        private static GUIStyle _body;
        private static GUIStyle _btn;
        private static GUIStyle _btnOn;
        private static bool _styles;
        private static Vector2 _scroll;

        internal static void Draw()
        {
            if (GaMode.Active)
                return;
            if (!SelectionOpen())
                return;
            Ensure();
            _win = GUI.Window(0x47414847, _win, DrawWindow,
                GaMode.Zh ? "Ground Arsenal 步兵" : "Ground Arsenal infantry");
        }

        private static bool SelectionOpen()
        {
            AircraftSelectionMenu menu = UnityEngine.Object.FindObjectOfType<AircraftSelectionMenu>();
            if (menu == null)
                return false;
            try
            {
                if (!menu.gameObject.activeInHierarchy)
                    return false;
            }
            catch { return false; }
            return true;
        }

        private static void DrawWindow(int id)
        {
            GUILayout.Label(GaMode.Zh
                ? "部署带 9 把热键枪。机上 hop-out（U）只带随身（槽 1）。G 在战场打开 300 武库。"
                : "Deploy with 9 hotbar guns. Aircraft hop-out (U) carries slot 1 only. G opens the 300-gun arsenal in the field.",
                _body);
            GaCombat.LoadPrefs();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(140f));
            for (int i = 0; i < GaCombat.Hotbar.Length; i++)
            {
                GaGun g = GaCatalog.Get(GaCombat.Hotbar[i]);
                string row = (i + 1).ToString() + "  " + GaCatalog.Name(g, GaMode.Zh)
                    + "  PEN " + Mathf.RoundToInt(g.Pen).ToString();
                GUILayout.Label(row, i == 0 ? _btnOn : _body);
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button(GaMode.Zh ? "部署步兵（300 武库 / 9 热键）" : "DEPLOY INFANTRY (300 arsenal / 9 hotbar)",
                GUILayout.Height(36f)))
            {
                GaCombat.SavePrefs();
                GaMode.HangarQueued = true;
            }
            GUILayout.Label(GaMode.Zh
                ? "进场后按 G 从 300 种里换热键。左轮到火箭筒、科幻枪都在。"
                : "Press G in the field to swap any of 300 guns onto the hotbar.",
                _body);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
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
            _body.fontSize = 13;
            _body.wordWrap = true;
            _body.normal.textColor = Color.white;
            _btn = new GUIStyle(GUI.skin.button);
            _btnOn = new GUIStyle(_btn);
            _btnOn.fontStyle = FontStyle.Bold;
            _btnOn.normal.textColor = new Color(1f, 0.9f, 0.3f);
        }
    }
}
