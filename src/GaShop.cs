using System.Collections.Generic;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace GroundArsenal
{
    internal static class GaShop
    {
        internal static bool Open;

        private static readonly List<AircraftDefinition> Planes = new List<AircraftDefinition>(24);
        private static Airbase _base;
        private static int _index;
        private static float _nextList;
        private static GUIStyle _btn;
        private static GUIStyle _btnOn;
        private static GUIStyle _lab;
        private static bool _styles;
        private static Vector2 _scroll;

        internal static void Tick()
        {
            if (!GaMode.Active || GaMode.Dead)
            {
                Open = false;
                return;
            }
            if (Input.GetKeyDown(KeyCode.B))
            {
                if (!GaForce.AllowBuyNearPlayer())
                {
                    if (GaForce.Kind == GaForceKind.Land)
                        GaMode.Deny("LAND cannot buy aircraft.", "陆军不能买飞机。");
                    else if (GaForce.Kind == GaForceKind.Sea)
                        GaMode.Deny("SEA buys aircraft on a carrier.", "海军只能在航母上买飞机。");
                    else
                        GaMode.Deny("No hangar nearby.", "附近没有机库。");
                    return;
                }
                Open = !Open;
                if (Open)
                    GaGarage.Close();
                Cursor.lockState = Open ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = Open;
                if (Open)
                    Refresh();
            }
            if (!Open)
                return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }
            if (Time.unscaledTime >= _nextList)
            {
                _nextList = Time.unscaledTime + 0.8f;
                Refresh();
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                Buy();
        }

        internal static void Draw()
        {
            if (!Open)
                return;
            float a = GaFxUi.Smooth(GaFxUi.Shop);
            if (a < 0.02f)
                return;
            Ensure();
            GaFxUi.Dim(a);
            bool was = GUI.enabled;
            GUI.enabled = Open;
            Rect r = new Rect(Screen.width * 0.5f - 280f, 90f, 560f, 420f);
            GaFxUi.Push(a, new Vector2(Screen.width * 0.5f, 110f), 26f);
            GUI.Window(0x47415348, r, DrawWin, "BUY AIRCRAFT  B/Enter spawn  Esc close");
            GaFxUi.Pop();
            GUI.enabled = was;
        }

        internal static void Close()
        {
            Open = false;
            if (GaMode.Active && !GaHud.BlocksLook)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        internal static void Clear()
        {
            Open = false;
            Planes.Clear();
            _base = null;
        }

        private static void DrawWin(int id)
        {
            GUILayout.Label(_base != null ? _base.name : "NO AIRBASE", _lab);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(300f));
            for (int i = 0; i < Planes.Count; i++)
            {
                AircraftDefinition d = Planes[i];
                if (d == null)
                    continue;
                string lab = d.unitName;
                try { lab = lab + "  " + d.value.ToString("0.0") + "M"; }
                catch { }
                if (GUILayout.Button(lab, i == _index ? _btnOn : _btn, GUILayout.Height(28f)))
                    _index = i;
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("SPAWN SELECTED", GUILayout.Height(36f)))
                Buy();
        }

        private static void Refresh()
        {
            Planes.Clear();
            _base = PickBase();
            if (_base == null)
                return;
            List<AircraftDefinition> got = null;
            try { got = _base.GetAvailableAircraft(); }
            catch { got = null; }
            if (got == null)
                return;
            for (int i = 0; i < got.Count; i++)
            {
                if (got[i] != null)
                    Planes.Add(got[i]);
            }
            if (_index >= Planes.Count)
                _index = 0;
        }

        private static Airbase PickBase()
        {
            Airbase[] all = GaPerfFrameGate.Airbases();
            if (all == null)
                return null;
            Vector3 at = GaMode.EyePos;
            Airbase best = null;
            float bestD = 2.5e7f;
            for (int i = 0; i < all.Length; i++)
            {
                Airbase ab = all[i];
                if (ab == null || ab.disabled)
                    continue;
                if (!GaForce.AllowBuyAt(ab))
                    continue;
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

        private static void Buy()
        {
            if (_base == null || _index < 0 || _index >= Planes.Count)
            {
                GaMode.Deny("No aircraft listed.", "没有可选飞机。");
                return;
            }
            AircraftDefinition def = Planes[_index];
            Player player = null;
            try
            {
                if (!GameManager.GetLocalPlayer(out player) || player == null)
                {
                    GaMode.Deny("No local player.", "没有玩家。");
                    return;
                }
            }
            catch
            {
                GaMode.Deny("No local player.", "没有玩家。");
                return;
            }
            try
            {
                if (!_base.CanSpawnAircraft(def))
                {
                    GaMode.Deny("Hangar busy.", "机库不可用。");
                    return;
                }
                Airbase.TrySpawnResult result = _base.TrySpawnAircraft(player, def, new LiveryKey(0), new Loadout(), 0.85f);
                if (!result.Allowed)
                {
                    GaMode.Deny("Spawn refused.", "无法生成飞机。");
                    return;
                }
                Close();
                GaMode.Deny("Aircraft marked. Walk up, T to board.",
                    "飞机已出动。走近按 T 登机。");
            }
            catch (System.Exception ex)
            {
                GaMode.Deny(ex.Message, ex.Message);
            }
        }

        private static void Ensure()
        {
            if (_styles)
                return;
            _styles = true;
            _lab = new GUIStyle(GUI.skin.label);
            _lab.fontSize = 14;
            _lab.wordWrap = true;
            _btn = new GUIStyle(GUI.skin.button);
            _btn.fontSize = 13;
            _btn.alignment = TextAnchor.MiddleLeft;
            _btnOn = new GUIStyle(_btn);
            _btnOn.fontStyle = FontStyle.Bold;
            _btnOn.normal.textColor = new Color(1f, 0.9f, 0.3f);
        }
    }
}
