using System;
using System.Collections.Generic;
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

        private static GUIStyle _chipBtn;
        private static bool _styles;
        /// <summary>Hangar infantry visor. Chip defaults off so aircraft-select clicks work.</summary>
        private static bool _menuOpen;
        internal static bool DeathPick;
        internal static bool FieldOpen;
        private static int _fieldFrame;
        private static AircraftSelectionMenu _menu;
        private static float _menuAt;
        private static Airbase _ab;
        private static float _abAt;

        /// <summary>
        /// Aircraft-select in a hangar, not the faction join screen and not cockpit/map.
        /// </summary>
        internal static bool JoinMenuOpen()
        {
            try
            {
                GameplayUI ui = SceneSingleton<GameplayUI>.i;
                if (ui != null && JoinMenuField != null)
                {
                    GameObject join = JoinMenuField.GetValue(ui) as GameObject;
                    if (join != null && join.activeInHierarchy)
                        return true;
                }
            }
            catch { }
            return false;
        }

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
                if (JoinMenuOpen())
                    return false;
            }
            catch { }
            return true;
        }

        internal static bool MenuOpen
        {
            get { return _menuOpen; }
        }

        internal static void TickKeys()
        {
            if (GaMode.Active)
            {
                if (!FieldOpen)
                    return;
            }
            else if (!_menuOpen && !DeathPick)
                return;
            if (!GaMode.Active && !HangarSelectOpen() && !DeathPick)
                return;
            for (int i = 0; i < GaClass.Count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    GaClass.Set((GaKitId)i);
                    GaCombat.ApplyClassKit();
                    GaCombat.SavePrefs();
                    return;
                }
            }
        }

        internal static void Draw()
        {
            if (GaMode.Active)
            {
                if (FieldOpen)
                    DrawSpawnVisor(true);
                return;
            }
            bool hangar = HangarSelectOpen();
            if (!hangar && !DeathPick)
            {
                _menuOpen = false;
                return;
            }
            if (DeathPick && !hangar)
                _menuOpen = true;
            DrawSpawnVisor(DeathPick);
            DrawChip();
            if (GaHud.CatalogOpen)
                ArmorCatalogService.Draw();
        }

        private static void DrawSpawnVisor(bool force)
        {
            Ensure();
            float a = GaFxUi.Smooth(GaFxUi.Hangar);
            if (force && a < 0.2f)
                a = 1f;
            if (a > 0.02f)
            {
                GaFxUi.Dim(a * 0.42f);
                Color prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, a);
                GaHud.DrawVisorFrame();
                GUI.color = prev;
                GaSpawnUi.Draw(a);
            }
        }

        internal static void ToggleField()
        {
            if (_fieldFrame == Time.frameCount)
                return;
            _fieldFrame = Time.frameCount;
            if (FieldOpen)
                CloseField();
            else
                OpenField();
        }

        internal static void OpenField()
        {
            if (!GaMode.Active || GaMode.Dead)
                return;
            FieldOpen = true;
            GaHud.ArsenalOpen = false;
            GaHud.CatalogOpen = false;
            GaShop.Close();
            try
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            catch { }
        }

        internal static void CloseField()
        {
            if (!FieldOpen)
                return;
            FieldOpen = false;
            if (!GaMode.Active || GaMode.Dead || GaMode.Downed)
                return;
            try
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            catch { }
        }

        internal static void OpenDeath()
        {
            DeathPick = true;
            _menuOpen = true;
            GaConfig.SetEnabled(true);
            try { CursorManager.SetFlag(CursorFlags.SelectionMenu, true); }
            catch { }
            AircraftSelectionMenu menu = CachedMenu();
            if (menu != null)
            {
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
        }

        internal static void ClearDeath()
        {
            DeathPick = false;
        }

        private static void DrawChip()
        {
            float chipW = 188f;
            float chipH = 28f;
            Rect chip = new Rect(18f, 8f, chipW, chipH);
            int prevDepth = GUI.depth;
            GUI.depth = -997;
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = _menuOpen
                ? new Color(0.35f, 0.28f, 0.08f, 1f)
                : new Color(0.08f, 0.16f, 0.12f, 1f);
            string lab = _menuOpen ? "Infantry | ON" : "Infantry | OFF";
            if (GUI.Button(chip, lab, _chipBtn))
            {
                _menuOpen = !_menuOpen;
                GaConfig.SetEnabled(_menuOpen);
                if (!_menuOpen)
                {
                    if (GaHud.CatalogOpen)
                        GaHud.CloseMenus();
                }
            }
            GUI.backgroundColor = prevBg;
            GUI.depth = prevDepth;
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
            Vector3 start = pos + Vector3.up * 10f;
            RaycastHit hit;
            if (GaPhys.RayDown(start, 80f, out hit))
                pos = hit.point + Vector3.up * 0.08f;
            else
                pos = pos + Vector3.up * 0.2f;
            return true;
        }

        internal static void CloseUi()
        {
            CloseField();
            ClearDeath();
            AircraftSelectionMenu menu = CachedMenu();
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
            Hangar[] all = GaPerfFrameGate.Hangars();
            if (all == null || all.Length == 0)
                return null;
            Camera cam = GaPerfFrameGate.Cam();
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

        internal static bool MenuAirbaseIsCarrier(Airbase ab)
        {
            return IsCarrierAirbase(ab);
        }

        internal static bool PlayerOnCarrierDeck()
        {
            if (!GaMode.Active)
                return false;
            RaycastHit hit;
            if (!GaPhys.RayDown(GaMode.EyePos, 40f, out hit))
                return false;
            try
            {
                return IsCarrierShip(hit.collider.GetComponentInParent<Ship>());
            }
            catch
            {
                return false;
            }
        }

        internal static Airbase PickedBase;

        internal static void PickBase(Airbase ab)
        {
            PickedBase = ab;
        }

        internal static bool TryForceSpawn(GaForceKind kind, out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero;
            yaw = 0f;
            if (PickedBase != null && !PickedBase.disabled)
            {
                bool car = IsCarrierAirbase(PickedBase);
                if (kind == GaForceKind.Sea)
                {
                    if (car && SnapHangar(PickedBase, out pos, out yaw))
                        return true;
                }
                else if (!car && SnapHangar(PickedBase, out pos, out yaw))
                    return true;
            }
            if (kind == GaForceKind.Sea)
                return TrySeaSpawn(out pos, out yaw);
            return TryLandFieldSpawn(out pos, out yaw);
        }

        internal static bool MenuIsCarrier()
        {
            return IsCarrierAirbase(CurrentAirbase());
        }

        private static bool TryLandFieldSpawn(out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero;
            yaw = 0f;
            Airbase ab = NearestFriendlyAirbase(false);
            if (ab == null)
                ab = NearestFriendlyAirbase(true);
            return SnapHangar(ab, out pos, out yaw);
        }

        private static bool TrySeaSpawn(out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero;
            yaw = 0f;
            Airbase ab = NearestFriendlyAirbase(true);
            if (SnapHangar(ab, out pos, out yaw))
                return true;
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return false;
            FactionHQ hq = GaPerfFrameGate.Hq();
            Ship best = null;
            float bestD = 1e12f;
            Vector3 at = Vector3.zero;
            Camera cam = GaPerfFrameGate.Cam();
            if (cam != null)
                at = cam.transform.position;
            for (int i = 0; i < all.Count; i++)
            {
                Ship s = all[i] as Ship;
                if (s == null || s.disabled || !IsCarrierShip(s))
                    continue;
                try
                {
                    if (hq != null && s.NetworkHQ != null && !object.ReferenceEquals(s.NetworkHQ, hq))
                        continue;
                }
                catch { }
                float d = (s.transform.position - at).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            if (best == null)
                return false;
            Vector3 p = best.transform.position + best.transform.up * 18f;
            RaycastHit hit;
            if (GaPhys.RayDown(p, 80f, out hit))
                pos = hit.point + Vector3.up * 0.08f;
            else
                pos = best.transform.position + Vector3.up * 12f;
            yaw = best.transform.eulerAngles.y;
            return true;
        }

        private static bool SnapHangar(Airbase ab, out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero;
            yaw = 0f;
            if (ab == null)
                return false;
            Hangar hangar = null;
            Hangar[] hs = null;
            try { hs = ab.GetComponentsInChildren<Hangar>(true); }
            catch { hs = null; }
            if (hs != null)
            {
                for (int i = 0; i < hs.Length; i++)
                {
                    if (hs[i] != null && !hs[i].Disabled)
                    {
                        hangar = hs[i];
                        break;
                    }
                }
            }
            if (hangar == null)
            {
                Vector3 p;
                try { p = ab.center != null ? ab.center.position : ab.transform.position; }
                catch { p = ab.transform.position; }
                Vector3 start = p + Vector3.up * 12f;
                RaycastHit hit;
                if (GaPhys.RayDown(start, 80f, out hit))
                    pos = hit.point + Vector3.up * 0.08f;
                else
                    pos = p + Vector3.up * 0.2f;
                yaw = ab.transform.eulerAngles.y;
                return true;
            }
            Transform t = null;
            try { t = hangar.GetSpawnTransform(); }
            catch { t = hangar.transform; }
            if (t == null)
                t = hangar.transform;
            pos = t.position;
            yaw = t.eulerAngles.y;
            Vector3 lift = pos + Vector3.up * 10f;
            RaycastHit hit2;
            if (GaPhys.RayDown(lift, 80f, out hit2))
                pos = hit2.point + Vector3.up * 0.08f;
            else
                pos = pos + Vector3.up * 0.2f;
            return true;
        }

        private static Airbase NearestFriendlyAirbase(bool carrier)
        {
            Airbase[] abs = GaPerfFrameGate.Airbases();
            if (abs == null)
                return null;
            FactionHQ hq = GaPerfFrameGate.Hq();
            Vector3 at = Vector3.zero;
            Camera cam = GaPerfFrameGate.Cam();
            if (cam != null)
                at = cam.transform.position;
            Airbase best = null;
            float bestD = 1e12f;
            for (int i = 0; i < abs.Length; i++)
            {
                Airbase ab = abs[i];
                if (ab == null || ab.disabled)
                    continue;
                if (IsCarrierAirbase(ab) != carrier)
                    continue;
                try
                {
                    if (hq != null && ab.CurrentHQ != null && !object.ReferenceEquals(ab.CurrentHQ, hq))
                        continue;
                }
                catch { }
                Vector3 p;
                try { p = ab.center != null ? ab.center.position : ab.transform.position; }
                catch { p = ab.transform.position; }
                float d = (p - at).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = ab;
                }
            }
            return best;
        }

        private static int _onId;
        private static float _onAt;
        private static bool _onVal;

        internal static bool OnCarrier(Aircraft ac)
        {
            if (ac == null)
                return false;
            int id = ac.GetInstanceID();
            if (id == _onId && Time.unscaledTime < _onAt)
                return _onVal;
            _onId = id;
            _onAt = Time.unscaledTime + 0.28f;
            _onVal = OnCarrierNow(ac);
            return _onVal;
        }

        private static bool OnCarrierNow(Aircraft ac)
        {
            if (ac == null)
                return false;
            try
            {
                if (IsCarrierShip(ac.GetComponentInParent<Ship>()))
                    return true;
            }
            catch { }
            RaycastHit hit;
            if (GaPhys.RayDown(ac.transform.position, 36f, out hit))
            {
                try
                {
                    if (IsCarrierShip(hit.collider.GetComponentInParent<Ship>()))
                        return true;
                }
                catch { }
            }
            try
            {
                Airbase[] abs = GaPerfFrameGate.Airbases();
                if (abs != null)
                {
                    Vector3 at = ac.transform.position;
                    for (int i = 0; i < abs.Length; i++)
                    {
                        Airbase ab = abs[i];
                        if (ab == null || ab.disabled || !IsCarrierAirbase(ab))
                            continue;
                        Vector3 p;
                        try { p = ab.center != null ? ab.center.position : ab.transform.position; }
                        catch { continue; }
                        Vector3 d = at - p;
                        d.y = 0f;
                        if (d.sqrMagnitude < 160000f && Mathf.Abs(at.y - p.y) < 40f)
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool IsCarrierAirbase(Airbase ab)
        {
            if (ab == null)
                return false;
            try
            {
                Unit u;
                if (ab.TryGetAttachedUnit(out u) && IsCarrierShip(u as Ship))
                    return true;
            }
            catch { }
            try
            {
                if (IsCarrierShip(ab.GetComponentInParent<Ship>()))
                    return true;
            }
            catch { }
            return NameLooksCarrier(ab.name);
        }

        private static bool IsCarrierShip(Ship ship)
        {
            if (ship == null)
                return false;
            string blob = ship.name;
            try
            {
                ShipDefinition sd = ship.definition as ShipDefinition;
                if (sd != null)
                    blob = blob + " " + (sd.jsonKey ?? "") + " " + (sd.unitName ?? "");
            }
            catch { }
            return NameLooksCarrier(blob);
        }

        private static bool NameLooksCarrier(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            string l = s.ToLowerInvariant();
            if (l.IndexOf("destroyer", System.StringComparison.Ordinal) >= 0)
                return false;
            return l.IndexOf("carrier", System.StringComparison.Ordinal) >= 0
                || l.IndexOf("hyperion", System.StringComparison.Ordinal) >= 0;
        }

        internal static Airbase FieldAirbase()
        {
            if (PickedBase != null && !PickedBase.disabled && !IsCarrierAirbase(PickedBase))
                return PickedBase;
            Airbase ab = CurrentAirbase();
            if (ab != null && !IsCarrierAirbase(ab))
                return ab;
            return NearestFriendlyAirbase(false);
        }

        private static Airbase CurrentAirbase()
        {
            if (Time.unscaledTime < _abAt)
                return _ab;
            _abAt = Time.unscaledTime + 0.45f;
            AircraftSelectionMenu menu = CachedMenu();
            if (menu != null && AirbaseField != null)
            {
                try
                {
                    _ab = AirbaseField.GetValue(menu) as Airbase;
                    return _ab;
                }
                catch { }
            }
            _ab = null;
            return null;
        }

        private static AircraftSelectionMenu CachedMenu()
        {
            if (Time.unscaledTime < _menuAt && _menu != null)
                return _menu;
            _menuAt = Time.unscaledTime + 0.45f;
            _menu = UnityEngine.Object.FindObjectOfType<AircraftSelectionMenu>();
            return _menu;
        }

        private static void Ensure()
        {
            if (_styles)
                return;
            _styles = true;
            _chipBtn = new GUIStyle(GUI.skin.button);
            _chipBtn.fontSize = 12;
            _chipBtn.fontStyle = FontStyle.Bold;
            _chipBtn.alignment = TextAnchor.MiddleLeft;
            _chipBtn.clipping = TextClipping.Overflow;
            _chipBtn.padding = new RectOffset(10, 8, 4, 4);
            _chipBtn.normal.textColor = new Color(0.8f, 0.95f, 0.85f, 0.98f);
        }
    }
}
