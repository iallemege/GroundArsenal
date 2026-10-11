using System.Collections.Generic;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>10 km LOS boxes on hostiles plus a live lead pip.</summary>
    internal static class GaSpot
    {
        private const float Range = 12000f;
        private const float RangeSq = Range * Range;
        private const int Cap = 24;

        private static bool _styles;
        private static float _nextLos;
        private static readonly List<Mark> Marks = new List<Mark>(Cap);

        private struct Mark
        {
            public Vector3 P;
            public Vector3 V;
            public float Ext;
            public bool Los;
        }

        internal static void Draw()
        {
            if (!GaMode.Active || GaMode.Dead)
                return;
            Camera cam = GaPerfFrameGate.Cam();
            if (cam == null)
                return;
            Ensure();
            if (Time.unscaledTime >= _nextLos)
            {
                _nextLos = Time.unscaledTime + 0.12f;
                Scan(cam);
            }
            GaGun g = GaCombat.Current;
            float spd = GaCombat.BulletSpeed(g);
            if (spd < 40f)
                spd = 40f;
            Vector3 eye = cam.transform.position;
            Color box = new Color(1f, 0.16f, 0.1f, 0.92f);
            Color pip = new Color(1f, 0.86f, 0.12f, 0.95f);
            for (int i = 0; i < Marks.Count; i++)
            {
                Mark m = Marks[i];
                if (!m.Los)
                    continue;
                Vector3 p = m.P;
                Vector3 sp = cam.WorldToScreenPoint(p);
                if (sp.z < 1.2f)
                    continue;
                float y = Screen.height - sp.y;
                if (HudBlocked(sp.x, y))
                    continue;
                float dist = Vector3.Distance(eye, p);
                if (dist < 4f)
                    dist = 4f;
                float s = (m.Ext * 280f) / dist;
                if (s < 10f) s = 10f;
                if (s > 140f) s = 140f;
                GaFxUi.Corners(sp.x, y, s, box);
                float t = dist / spd;
                Vector3 lead = p + m.V * t;
                lead.y -= 4.9f * t * t;
                t = Vector3.Distance(eye, lead) / spd;
                lead = p + m.V * t;
                lead.y -= 4.9f * t * t;
                Vector3 lp = cam.WorldToScreenPoint(lead);
                if (lp.z < 1.2f)
                    continue;
                float ly = Screen.height - lp.y;
                if (HudBlocked(lp.x, ly))
                    continue;
                GaFxUi.LeadPip(lp.x, ly, pip);
            }
        }

        private static void Scan(Camera cam)
        {
            Marks.Clear();
            Vector3 eye = cam.transform.position;
            FactionHQ hq = GaPerfFrameGate.Hq();
            Aircraft lift = GaAirLift.Ac;
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all != null)
            {
                for (int i = 0; i < all.Count && Marks.Count < Cap; i++)
                {
                    Unit u = all[i];
                    if (u == null || u.disabled)
                        continue;
                    if (lift != null && object.ReferenceEquals(u, lift))
                        continue;
                    if (!Hostile(u, hq))
                        continue;
                    Vector3 p;
                    try { p = u.GetCenterOfMass(); }
                    catch { p = u.transform.position; }
                    Vector3 d = p - eye;
                    if (d.sqrMagnitude > RangeSq || d.sqrMagnitude < 16f)
                        continue;
                    if (!InView(cam, p))
                        continue;
                    float ext = 6f;
                    if (u is Ship)
                        ext = 36f;
                    else if (u is Aircraft)
                        ext = 10f;
                    else if (u is GroundVehicle)
                        ext = 4f;
                    Mark m;
                    m.P = p;
                    m.V = Vel(u);
                    m.Ext = ext;
                    m.Los = Line(eye, p, u, null);
                    Marks.Add(m);
                }
            }
            List<GaDropBot> bots = GaDropSquad.Hostiles;
            if (bots == null)
                return;
            for (int i = 0; i < bots.Count && Marks.Count < Cap; i++)
            {
                GaDropBot b = bots[i];
                if (b == null || b.Downed || b.Hp <= 0f)
                    continue;
                Vector3 p = b.transform.position + Vector3.up * 0.9f;
                if ((p - eye).sqrMagnitude > RangeSq)
                    continue;
                if (!InView(cam, p))
                    continue;
                Mark m;
                m.P = p;
                m.V = Vector3.zero;
                m.Ext = 1.1f;
                m.Los = Line(eye, p, null, b);
                Marks.Add(m);
            }
        }

        private static bool InView(Camera cam, Vector3 world)
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            return v.z > 1.2f && v.x > 0.02f && v.x < 0.98f && v.y > 0.02f && v.y < 0.98f;
        }

        private static bool Line(Vector3 from, Vector3 to, Unit unit, GaDropBot bot)
        {
            Vector3 dir = to - from;
            float mag = dir.magnitude;
            if (mag < 2f)
                return true;
            RaycastHit hit;
            if (!Physics.Raycast(from, dir / mag, out hit, mag - 0.8f, GaPhys.WorldMask, QueryTriggerInteraction.Ignore))
                return true;
            if (unit != null)
            {
                Unit hitU = hit.collider.GetComponentInParent<Unit>();
                if (hitU != null && object.ReferenceEquals(hitU, unit))
                    return true;
            }
            if (bot != null)
            {
                GaDropBot hitB = GaPhys.Bot(hit.collider);
                if (hitB == bot)
                    return true;
            }
            return false;
        }

        private static bool Hostile(Unit u, FactionHQ local)
        {
            if (u == null)
                return false;
            if (local == null)
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

        private static Vector3 Vel(Unit u)
        {
            try
            {
                if (u.rb != null)
                    return u.rb.velocity;
            }
            catch { }
            return Vector3.zero;
        }

        private static bool HudBlocked(float x, float y)
        {
            float h = Screen.height;
            float w = Screen.width;
            if (y < 44f)
                return true;
            if (y > h - 40f)
                return true;
            if (x < 280f && y > h - 120f)
                return true;
            if (x > w - 330f && y > h - 160f)
                return true;
            return false;
        }

        private static void Ensure()
        {
            if (_styles)
                return;
            _styles = true;
        }
    }
}
