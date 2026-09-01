using System;
using System.IO;
using UnityEngine;

namespace GroundArsenal
{
    internal sealed class GaKit
    {
        internal string Body;
        internal string Mag;
        internal string Slide;
        internal string Acc;
        internal string Tex;
        internal int Rig;
    }

    internal static class GaKits
    {
        private static GaKit[] _all;
        private static bool _tried;

        internal static GaKit Get(int id)
        {
            Ensure();
            if (_all == null || id < 0 || id >= _all.Length || _all[id] == null)
                return Fallback();
            return _all[id];
        }

        internal static void Ensure()
        {
            if (_tried)
                return;
            _tried = true;
            _all = new GaKit[GaCatalog.Count];
            string dir = GaObj.ResolveDir();
            if (string.IsNullOrEmpty(dir))
                return;
            string path = Path.Combine(dir, "kits.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line) || line[0] == '#')
                    continue;
                string[] p = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 6)
                    continue;
                int id;
                if (!int.TryParse(p[0], out id) || id < 0 || id >= GaCatalog.Count)
                    continue;
                GaKit k = new GaKit();
                k.Body = Dash(p[1]);
                k.Mag = Dash(p[2]);
                k.Slide = Dash(p[3]);
                k.Acc = Dash(p[4]);
                int.TryParse(p[5], out k.Rig);
                k.Tex = p.Length > 6 ? Dash(p[6]) : null;
                _all[id] = k;
            }
        }

        private static string Dash(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "-")
                return null;
            return s;
        }

        private static GaKit Fallback()
        {
            GaKit k = new GaKit();
            k.Body = "q_Pistol_1.obj";
            k.Mag = "pistol_mag.obj";
            k.Slide = "pistol_slide.obj";
            k.Rig = 1;
            return k;
        }
    }
}
