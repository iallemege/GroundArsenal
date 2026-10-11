using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>H: buy a ground vehicle and spawn it beside the player.</summary>
    internal static class GaGarage
    {
        internal static bool Open;

        private static readonly List<VehicleDefinition> Stock = new List<VehicleDefinition>(48);
        private static int _index;
        private static float _nextList;
        private static float _nextBuy;
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
            if (Input.GetKeyDown(KeyCode.H))
            {
                if (Open)
                {
                    Close();
                    return;
                }
                if (GaHud.CatalogOpen || GaHangar.FieldOpen || GaRadio.Open || GaRide.Busy)
                    return;
                Open = true;
                GaShop.Close();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
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
            GUI.enabled = true;
            Rect r = new Rect(Screen.width * 0.5f - 280f, 90f, 560f, 420f);
            GaFxUi.Push(a, new Vector2(Screen.width * 0.5f, 110f), 26f);
            GUI.Window(0x47415648, r, DrawWin, "BUY VEHICLE  H/Enter spawn nearby  Esc close");
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
            Stock.Clear();
        }

        private static void DrawWin(int id)
        {
            GUILayout.Label("FUNDS  " + GaFunds.Read().ToString("0.0") + "M   spawn 14m ahead", _lab);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(300f));
            for (int i = 0; i < Stock.Count; i++)
            {
                VehicleDefinition d = Stock[i];
                if (d == null)
                    continue;
                string lab = d.unitName;
                try { lab = d.vehicleType + "  " + lab + "  " + d.value.ToString("0.0") + "M"; }
                catch { }
                if (GUILayout.Button(lab, i == _index ? _btnOn : _btn, GUILayout.Height(28f)))
                    _index = i;
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("SPAWN NEXT TO ME", GUILayout.Height(36f)))
                Buy();
        }

        private static void Refresh()
        {
            Stock.Clear();
            Encyclopedia enc = null;
            try { enc = Encyclopedia.i; }
            catch { enc = null; }
            if (enc == null || enc.vehicles == null)
                return;
            for (int i = 0; i < enc.vehicles.Count; i++)
            {
                VehicleDefinition d = enc.vehicles[i];
                if (d == null || d.unitPrefab == null)
                    continue;
                try
                {
                    if (d.NotAllowed(true))
                        continue;
                }
                catch { }
                Stock.Add(d);
            }
            Stock.Sort(CompareName);
            if (_index >= Stock.Count)
                _index = 0;
        }

        private static int CompareName(VehicleDefinition a, VehicleDefinition b)
        {
            string na = a != null ? a.unitName : "";
            string nb = b != null ? b.unitName : "";
            if (na == null) na = "";
            if (nb == null) nb = "";
            return string.CompareOrdinal(na, nb);
        }

        private static void Buy()
        {
            if (_index < 0 || _index >= Stock.Count)
            {
                GaMode.Deny("No vehicles listed.", "没有可选车辆。");
                return;
            }
            if (Time.unscaledTime < _nextBuy)
            {
                GaMode.Deny("Vehicle shop cooling down.", "载具购买冷却中。");
                return;
            }
            if (!HostOk())
            {
                GaMode.Deny("Host only.", "仅房主可购买车辆。");
                return;
            }
            VehicleDefinition def = Stock[_index];
            if (def == null || def.unitPrefab == null)
            {
                GaMode.Deny("Missing prefab.", "载具预制体缺失。");
                return;
            }
            float cost = 0f;
            try { cost = def.value; }
            catch { cost = 0f; }
            if (cost < 0f)
                cost = 0f;
            if (!GaFunds.CanPay(cost))
            {
                GaMode.Deny("Not enough funds (" + cost.ToString("0.0") + "M).",
                    "资金不足 (" + cost.ToString("0.0") + "M)。");
                return;
            }
            Vector3 pos;
            Quaternion rot;
            if (!FindPad(def, out pos, out rot))
            {
                GaMode.Deny("No ground nearby.", "附近没有落地点。");
                return;
            }
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
            if (!GaFunds.TryPay(cost))
            {
                GaMode.Deny("Not enough funds.", "资金不足。");
                return;
            }
            Spawner sp = null;
            try { sp = NetworkSceneSingleton<Spawner>.i; }
            catch { sp = null; }
            if (sp == null)
            {
                Refund(player, cost);
                GaMode.Deny("Spawner missing.", "无法生成载具。");
                return;
            }
            GroundVehicle gv = null;
            try
            {
                gv = sp.SpawnVehicle(
                    def.unitPrefab,
                    pos.ToGlobalPosition(),
                    rot,
                    Vector3.zero,
                    GaPerfFrameGate.Hq(),
                    null,
                    1f,
                    true,
                    player);
            }
            catch (System.Exception ex)
            {
                Refund(player, cost);
                GaMode.Deny(ex.Message, ex.Message);
                return;
            }
            if (gv == null)
            {
                Refund(player, cost);
                GaMode.Deny("Spawn refused.", "无法生成载具。");
                return;
            }
            _nextBuy = Time.unscaledTime + 4f;
            Close();
            string name = def.unitName;
            if (string.IsNullOrEmpty(name))
                name = "VEHICLE";
            GaMode.Deny(name + " spawned. T board.", name + " 已刷出  T上车");
        }

        private static bool FindPad(VehicleDefinition def, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            Vector3 at = GaMode.EyePos;
            Transform pawn = GaMode.PawnRoot;
            if (pawn != null)
                at = pawn.position;
            Transform eye = GaMode.Eye;
            Vector3 fwd = Vector3.forward;
            if (eye != null)
                fwd = eye.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f)
                fwd = Vector3.forward;
            fwd.Normalize();
            rot = Quaternion.LookRotation(fwd, Vector3.up);
            float dist = 12f;
            try
            {
                float len = def.length;
                if (len > 4f)
                    dist = 8f + len * 0.55f;
                if (dist > 22f)
                    dist = 22f;
                if (dist < 10f)
                    dist = 10f;
            }
            catch { }
            Vector3 probe = at + Vector3.up * 12f;
            RaycastHit hit;
            Vector3 ground = at;
            if (GaPhys.RayDown(probe, 4200f, out hit))
                ground = hit.point;
            else if (!GaPhys.RayDown(at + Vector3.up * 2f, 80f, out hit))
                return false;
            else
                ground = hit.point;
            pos = ground + fwd * dist;
            if (GaPhys.RayDown(pos + Vector3.up * 40f, 90f, out hit))
                pos = hit.point;
            float lift = 0.35f;
            try { lift += def.spawnOffset.y; }
            catch { }
            if (lift < 0.2f)
                lift = 0.2f;
            pos.y += lift;
            return true;
        }

        private static bool HostOk()
        {
            try
            {
                if (NetworkManagerNuclearOption.i == null)
                    return false;
                if (NetworkManagerNuclearOption.i.Server == null)
                    return false;
                return NetworkManagerNuclearOption.i.Server.Active;
            }
            catch
            {
                return false;
            }
        }

        private static void Refund(Player player, float cost)
        {
            if (player == null || cost <= 0.001f)
                return;
            try { player.AddAllocation(cost); }
            catch { }
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
