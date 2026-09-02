using System.IO;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>512x256 hangar-style side profiles. One PNG per modeled gun.</summary>
    internal static class GaIcons
    {
        private static Texture2D[] _tex;
        private static bool _tried;

        internal static Texture2D Get(GaGun g)
        {
            Ensure();
            if (g == null || _tex == null)
                return null;
            int i = g.Id;
            if (i < 0 || i >= _tex.Length)
                return null;
            return _tex[i];
        }

        internal static void Ensure()
        {
            if (_tried)
                return;
            _tried = true;
            _tex = new Texture2D[GaCatalog.Count];
            string dir = GaObj.ResolveDir();
            if (string.IsNullOrEmpty(dir))
            {
                EnsureMags();
                return;
            }
            int n = 0;
            for (int i = 0; i < GaCatalog.Count; i++)
            {
                GaGun g = GaCatalog.All[i];
                string file = g != null ? g.Icon : null;
                if (string.IsNullOrEmpty(file))
                    continue;
                Texture2D t = GaObj.LoadPng(Path.Combine(dir, file));
                if (t != null)
                {
                    t.wrapMode = TextureWrapMode.Clamp;
                    n++;
                }
                _tex[i] = t;
            }
            if (GaPlugin.Log != null)
                GaPlugin.Log.LogInfo("GroundArsenal icons " + n + "/" + GaCatalog.Count + " from " + dir);
            EnsureMags();
        }

        private static Texture2D[,] _mag;
        private static bool _mags;

        internal static Texture2D MagIcon(GaGun g, int state)
        {
            EnsureMags();
            if (_mag == null)
                return null;
            if (state < 0) state = 0;
            if (state > 2) state = 2;
            int kind = 0;
            if (g != null && g.Fire == 4)
                kind = 1;
            else if (g != null && (g.Family == 4 || g.Family == 5))
                kind = 2;
            return _mag[kind, state];
        }

        internal static void EnsureMags()
        {
            if (_mags)
                return;
            _mags = true;
            _mag = new Texture2D[3, 3];
            for (int k = 0; k < 3; k++)
            {
                for (int s = 0; s < 3; s++)
                    _mag[k, s] = PaintMag(k, s);
            }
        }

        private static Texture2D PaintMag(int kind, int state)
        {
            int w = 40;
            int h = 64;
            Texture2D t = new Texture2D(w, h, TextureFormat.ARGB32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[w * h];
            Color edge = new Color(0.25f, 0.95f, 0.82f, 0.95f);
            Color fill = state == 2
                ? new Color(0.35f, 1f, 0.55f, 0.92f)
                : new Color(0.95f, 0.72f, 0.18f, 0.9f);
            Color empty = new Color(0.04f, 0.08f, 0.08f, 0.72f);
            float fillU = state == 0 ? 0f : (state == 1 ? 0.48f : 0.92f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f) / w;
                    float ny = (y + 0.5f) / h;
                    bool inside = false;
                    if (kind == 1)
                    {
                        float dx = Mathf.Abs(nx - 0.5f);
                        inside = dx < 0.18f && ny > 0.06f && ny < 0.92f;
                        if (ny > 0.78f && dx < 0.28f)
                            inside = true;
                        if (ny < 0.18f && dx < 0.12f)
                            inside = true;
                    }
                    else if (kind == 2)
                    {
                        float dx = Mathf.Abs(nx - 0.5f);
                        inside = dx < 0.16f && ny > 0.08f && ny < 0.9f;
                    }
                    else
                    {
                        float dx = Mathf.Abs(nx - 0.5f);
                        float taper = 0.28f - ny * 0.08f;
                        inside = dx < taper && ny > 0.08f && ny < 0.9f;
                        if (ny < 0.2f && dx < 0.32f)
                            inside = true;
                    }
                    Color c = new Color(0f, 0f, 0f, 0f);
                    if (inside)
                    {
                        bool rim = false;
                        if (kind == 0)
                        {
                            float dx = Mathf.Abs(nx - 0.5f);
                            float taper = 0.28f - ny * 0.08f;
                            rim = dx > taper - 0.045f || ny < 0.12f || ny > 0.86f;
                        }
                        else
                        {
                            float dx = Mathf.Abs(nx - 0.5f);
                            rim = dx > 0.13f || ny < 0.12f || ny > 0.86f;
                        }
                        if (rim)
                            c = edge;
                        else if (ny < fillU)
                            c = fill;
                        else
                            c = empty;
                    }
                    px[y * w + x] = c;
                }
            }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }
    }
}
