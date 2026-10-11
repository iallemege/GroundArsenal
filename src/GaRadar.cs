using System.Collections.Generic;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>Portable PPI: aircraft / vehicles / ships around the soldier (bottom-right).</summary>
    internal static class GaRadar
    {
        internal const float RangeM = 6000f;

        private static Texture2D _px;
        private static GUIStyle _tiny;
        private static GUIStyle _bsodFace;
        private static GUIStyle _bsodBody;
        private static GUIStyle _bsodCode;
        private static float _sweep;
        private static bool _styles;

        internal static void Tick(float dt)
        {
            _sweep = Mathf.Repeat(_sweep + dt * 150f, 360f);
        }

        internal static Rect ScreenRect()
        {
            float size = 168f;
            return new Rect(Screen.width - size - 16f, 42f, size, size);
        }

        internal static void Draw()
        {
            if (!GaPerfFrameGate.Paint())
                return;
            if (!GaMode.Active || GaMode.Dead)
                return;
            if (GaOritasy.EmpStunned())
            {
                DrawBsod();
                return;
            }
            Rect box = ScreenRect();
            Ensure();
            float size = box.width;
            float x = box.x;
            float y = box.y;
            Vector2 c = new Vector2(x + size * 0.5f, y + size * 0.5f);
            float r = size * 0.42f;

            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.08f, 0.09f, 0.78f);
            GUI.DrawTexture(new Rect(x, y, size, size), _px);
            GUI.color = new Color(0.2f, 0.9f, 0.78f, 0.32f);
            DrawRing(c, r, 10);
            DrawRing(c, r * 0.66f, 7);
            DrawRing(c, r * 0.33f, 5);

            float yaw = GaMode.LookYaw;
            Vector3 origin = GaMode.EyePos;
            FactionHQ localHq = GaPerfFrameGate.Hq();
            Unit linked = GaMode.LinkedUnit();

            try
            {
                List<Unit> all = GaPerfFrameGate.PlotUnits();
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        Unit u = all[i];
                        if (u == null || u.disabled)
                            continue;
                        if (!(u is Aircraft) && !(u is GroundVehicle) && !(u is Ship))
                            continue;
                        if (linked != null && object.ReferenceEquals(u, linked))
                        {
                            Plot(c, r, origin, yaw, u.transform.position, 8f,
                                new Color(0.2f, 0.95f, 1f, 1f));
                            continue;
                        }
                        Vector3 dp = u.transform.position - origin;
                        dp.y = 0f;
                        float d2 = dp.sqrMagnitude;
                        if (d2 > 36000000f || d2 < 4f)
                            continue;
                        float dist = Mathf.Sqrt(d2);
                        float bearing = Mathf.Atan2(dp.x, dp.z) * Mathf.Rad2Deg - yaw;
                        float rn = dist / RangeM;
                        Vector2 p = Polar(c, r, bearing, rn);
                        bool hostile = IsHostile(u, localHq);
                        if (u is Aircraft)
                            GUI.color = hostile
                                ? new Color(1f, 0.28f, 0.18f, 0.95f)
                                : new Color(0.25f, 1f, 0.4f, 0.95f);
                        else if (u is Ship)
                            GUI.color = hostile
                                ? new Color(1f, 0.55f, 0.15f, 0.95f)
                                : new Color(0.3f, 0.75f, 1f, 0.95f);
                        else
                            GUI.color = hostile
                                ? new Color(1f, 0.82f, 0.2f, 0.9f)
                                : new Color(0.55f, 0.9f, 0.35f, 0.9f);
                        float s = u is Aircraft ? 5f : 4f;
                        GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), _px);
                    }
                }
            }
            catch { }

            GUI.color = new Color(0.3f, 0.95f, 0.82f, 0.5f);
            Vector2 tip = Polar(c, r, _sweep, 1f);
            DrawSeg(c, tip, 2f);
            GUI.color = new Color(0.25f, 0.95f, 0.82f, 0.9f);
            GUI.DrawTexture(new Rect(c.x - 2f, c.y - 2f, 4f, 4f), _px);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 6f, y + 2f, 140f, 16f), "SUIT RADAR  6 km", _tiny);
            GUI.color = prev;
        }

        private static void DrawBsod()
        {
            Ensure();
            Rect box = ScreenRect();
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0.47f, 0.84f, 1f);
            GUI.DrawTexture(box, _px);
            GUI.color = Color.white;
            float pad = 10f;
            GUI.Label(new Rect(box.x + pad, box.y + 6f, box.width - pad * 2f, 36f), ":(", _bsodFace);
            GUI.Label(new Rect(box.x + pad, box.y + 44f, box.width - pad * 2f, 72f),
                "Your suit radar ran into a problem and needs to restart.", _bsodBody);
            GUI.Label(new Rect(box.x + pad, box.y + box.height - 36f, box.width - pad * 2f, 16f),
                "Stop code: EMP_PULSE_FAULT", _bsodCode);
            GUI.color = prev;
        }

        private static void Plot(Vector2 c, float r, Vector3 origin, float yaw, Vector3 world, float s, Color col)
        {
            Vector3 dp = world - origin;
            dp.y = 0f;
            float dist = dp.magnitude;
            if (dist > RangeM)
                return;
            float bearing = Mathf.Atan2(dp.x, dp.z) * Mathf.Rad2Deg - yaw;
            Vector2 p = Polar(c, r, bearing, dist / RangeM);
            GUI.color = col;
            GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), _px);
        }

        private static FactionHQ LocalHq()
        {
            Unit linked = GaMode.LinkedUnit();
            if (linked != null)
            {
                try { return linked.NetworkHQ; }
                catch { }
            }
            FactionHQ hq = null;
            try { GameManager.GetLocalHQ(out hq); }
            catch { }
            return hq;
        }

        private static bool IsHostile(Unit u, FactionHQ local)
        {
            if (u == null || local == null)
                return true;
            try
            {
                FactionHQ other = u.NetworkHQ;
                if (other == null)
                    return true;
                return !object.ReferenceEquals(other, local);
            }
            catch
            {
                return true;
            }
        }

        private static Vector2 Polar(Vector2 c, float r, float bearingDeg, float rangeNorm)
        {
            float rad = bearingDeg * Mathf.Deg2Rad;
            float rr = Mathf.Clamp01(rangeNorm) * r;
            return new Vector2(c.x + Mathf.Sin(rad) * rr, c.y - Mathf.Cos(rad) * rr);
        }

        private static void DrawRing(Vector2 c, float r, int segs)
        {
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * 360f * Mathf.Deg2Rad;
                float px = c.x + Mathf.Sin(a) * r;
                float py = c.y - Mathf.Cos(a) * r;
                GUI.DrawTexture(new Rect(px - 1f, py - 1f, 2f, 2f), _px);
            }
        }

        private static void DrawSeg(Vector2 a, Vector2 b, float w)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1f)
                return;
            int n = Mathf.CeilToInt(len / 3f);
            if (n > 16)
                n = 16;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float px = a.x + d.x * t;
                float py = a.y + d.y * t;
                GUI.DrawTexture(new Rect(px - w * 0.5f, py - w * 0.5f, w, w), _px);
            }
        }

        private static void Ensure()
        {
            if (_styles)
                return;
            _styles = true;
            _px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
            _tiny = new GUIStyle(GUI.skin.label);
            _tiny.fontSize = 10;
            _tiny.normal.textColor = new Color(0.45f, 1f, 0.88f);
            Font en = EnglishFont(12);
            _bsodFace = new GUIStyle(GUI.skin.label);
            _bsodFace.fontSize = 28;
            _bsodFace.fontStyle = FontStyle.Bold;
            _bsodFace.normal.textColor = Color.white;
            _bsodFace.alignment = TextAnchor.UpperLeft;
            _bsodBody = new GUIStyle(GUI.skin.label);
            _bsodBody.fontSize = 11;
            _bsodBody.wordWrap = true;
            _bsodBody.normal.textColor = Color.white;
            _bsodCode = new GUIStyle(GUI.skin.label);
            _bsodCode.fontSize = 10;
            _bsodCode.normal.textColor = Color.white;
            if (en != null)
            {
                _tiny.font = en;
                _bsodFace.font = en;
                _bsodBody.font = en;
                _bsodCode.font = en;
            }
        }

        private static Font EnglishFont(int size)
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(
                    new string[] { "Segoe UI", "Arial", "Tahoma", "Calibri" }, size);
            }
            catch
            {
                return null;
            }
        }
    }
}
