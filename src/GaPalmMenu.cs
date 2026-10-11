using System.Collections.Generic;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;
using UnityEngine.XR;

namespace GroundArsenal
{
    /// <summary>
    /// Left palm toward the eyes opens a hand menu: buy an aircraft, and the key list.
    /// </summary>
    internal static class GaPalmMenu
    {
        internal static bool Open;

        private static GameObject _root;
        private static TextMesh _text;
        private static readonly List<AircraftDefinition> _planes = new List<AircraftDefinition>(16);
        private static Airbase _base;
        private static int _index;
        private static float _nextList;
        private static float _nextBases;
        private static Airbase[] _bases;
        private static float _stickLock;
        private static string _status = "";

        internal static void Tick()
        {
            if (!GaVr.Active || !GaMode.Active)
            {
                Hide();
                return;
            }
            Vector3 hand;
            Quaternion rot;
            if (!LeftPalm(out hand, out rot))
            {
                Hide();
                return;
            }
            Open = true;
            if (Time.unscaledTime >= _nextList)
            {
                _nextList = Time.unscaledTime + 0.6f;
                RefreshList();
            }
            Scroll();
            if (GaVr.FireDown)
                Buy();
            GaVr.MoveWish = Vector3.zero;
            Show(hand, rot);
        }

        internal static void Clear()
        {
            Hide();
            _planes.Clear();
            _base = null;
            _bases = null;
            _nextBases = 0f;
            _status = "";
        }

        private static bool LeftPalm(out Vector3 hand, out Quaternion rot)
        {
            hand = Vector3.zero;
            rot = Quaternion.identity;
            Camera cam = GaPerfFrameGate.Cam();
            if (cam == null || cam.transform.parent == null)
                return false;
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!left.isValid)
                return false;
            Vector3 localPos;
            Quaternion localRot;
            if (!left.TryGetFeatureValue(CommonUsages.devicePosition, out localPos))
                return false;
            if (!left.TryGetFeatureValue(CommonUsages.deviceRotation, out localRot))
                return false;
            Transform rig = cam.transform.parent;
            hand = rig.TransformPoint(localPos);
            rot = rig.rotation * localRot;
            Vector3 toHead = cam.transform.position - hand;
            float dist = toHead.magnitude;
            if (dist < 0.12f || dist > 0.7f)
                return false;
            Vector3 palm = rot * Vector3.down;
            if (Vector3.Dot(palm, toHead / dist) < 0.62f)
                return false;
            Vector3 ahead = hand - cam.transform.position;
            if (Vector3.Dot(cam.transform.forward, ahead) < 0.02f)
                return false;
            return true;
        }

        private static void RefreshList()
        {
            _planes.Clear();
            _base = NearestFriendly();
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
                    _planes.Add(got[i]);
            }
            if (_index >= _planes.Count)
                _index = 0;
        }

        private static Airbase NearestFriendly()
        {
            FactionHQ hq = GaPerfFrameGate.Hq();
            if (Time.unscaledTime >= _nextBases || _bases == null)
            {
                _nextBases = Time.unscaledTime + 2.2f;
                _bases = GaPerfFrameGate.Airbases();
            }
            Airbase[] all = _bases;
            Airbase best = null;
            float bestD = 1e12f;
            Vector3 at = GaMode.EyePos;
            if (all == null)
                return null;
            for (int i = 0; i < all.Length; i++)
            {
                Airbase ab = all[i];
                if (ab == null || ab.disabled)
                    continue;
                if (hq != null && ab.CurrentHQ != hq)
                    continue;
                float d = (ab.transform.position - at).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = ab;
                }
            }
            return best;
        }

        private static void Scroll()
        {
            if (Time.unscaledTime < _stickLock || _planes.Count == 0)
                return;
            float y = GaVr.MoveWish.z;
            if (y > 0.55f)
            {
                _index--;
                if (_index < 0)
                    _index = _planes.Count - 1;
                _stickLock = Time.unscaledTime + 0.22f;
            }
            else if (y < -0.55f)
            {
                _index++;
                if (_index >= _planes.Count)
                    _index = 0;
                _stickLock = Time.unscaledTime + 0.22f;
            }
        }

        private static void Buy()
        {
            if (_base == null || _index < 0 || _index >= _planes.Count)
            {
                _status = "NO AIRBASE";
                return;
            }
            AircraftDefinition def = _planes[_index];
            if (!_base.CanSpawnAircraft(def))
            {
                _status = "NO HANGAR";
                return;
            }
            Player player = null;
            try
            {
                if (!GameManager.GetLocalPlayer(out player) || player == null)
                {
                    _status = "NO PLAYER";
                    return;
                }
            }
            catch
            {
                _status = "NO PLAYER";
                return;
            }
            try
            {
                Airbase.TrySpawnResult result = _base.TrySpawnAircraft(player, def, new LiveryKey(0), new Loadout(), 0.85f);
                _status = def.unitName + "  " + result;
            }
            catch (System.Exception ex)
            {
                _status = ex.Message;
            }
        }

        private static void Show(Vector3 hand, Quaternion handRot)
        {
            if (_root == null)
            {
                _root = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _root.name = "GaPalmMenu";
                GaPhys.Strip(_root);
                _root.transform.localScale = new Vector3(0.22f, 0.28f, 1f);
                Renderer rend = _root.GetComponent<Renderer>();
                if (rend != null)
                {
                    Shader sh = Shader.Find("Unlit/Color");
                    if (sh == null)
                        sh = Shader.Find("Sprites/Default");
                    if (sh != null)
                    {
                        rend.material = new Material(sh);
                        if (rend.material.HasProperty("_Color"))
                            rend.material.color = new Color(0.02f, 0.05f, 0.05f, 1f);
                    }
                }
                GameObject label = new GameObject("Label");
                label.transform.SetParent(_root.transform, false);
                label.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                label.transform.localScale = new Vector3(0.045f, 0.036f, 1f);
                _text = label.AddComponent<TextMesh>();
                _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _text.fontSize = 48;
                _text.characterSize = 0.08f;
                _text.anchor = TextAnchor.MiddleCenter;
                _text.alignment = TextAlignment.Center;
                _text.color = new Color(0.75f, 1f, 0.9f, 1f);
                MeshRenderer lr = label.GetComponent<MeshRenderer>();
                if (lr != null && _text.font != null)
                    lr.material = _text.font.material;
            }
            Camera cam = GaPerfFrameGate.Cam();
            _root.SetActive(true);
            Vector3 pos = hand + handRot * new Vector3(0f, 0.02f, 0.04f);
            _root.transform.position = pos;
            if (cam != null)
                _root.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);
            _text.text = Body();
        }

        private static string Body()
        {
            string funds = GaFunds.Read().ToString("0.0");
            string list = _base == null ? "no airbase" : _base.name;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("BUY  ").Append(funds).Append("M\n");
            sb.Append(list).Append('\n');
            int n = _planes.Count;
            if (n == 0)
                sb.Append("(none)\n");
            int start = 0;
            if (_index > 4)
                start = _index - 4;
            int end = start + 6;
            if (end > n)
                end = n;
            for (int i = start; i < end; i++)
            {
                string mark = i == _index ? "> " : "  ";
                string name = _planes[i].unitName;
                if (string.IsNullOrEmpty(name))
                    name = _planes[i].name;
                sb.Append(mark).Append(name).Append('\n');
            }
            sb.Append("stick  select   RT  buy\n");
            sb.Append("Y/X drop   grip/E take\n");
            sb.Append("RT fire  LT aim  A jump\n");
            sb.Append("T board   B aircraft   G guns\n");
            sb.Append("V radio   Q spot   F frag   C crouch");
            if (!string.IsNullOrEmpty(_status))
                sb.Append('\n').Append(_status);
            return sb.ToString();
        }

        private static void Hide()
        {
            Open = false;
            if (_root != null)
                _root.SetActive(false);
        }
    }
}
