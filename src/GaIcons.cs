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
                return;
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
        }
    }
}
