using System;
using System.Reflection;
using HarmonyLib;

namespace GroundArsenal
{
    /// <summary>
    /// Optional coexistence with Oritasy. No compile reference — reflection only.
    /// Stops Oritasy infantry Tick/Draw so this plugin owns FPS.
    /// </summary>
    internal static class GaOritasy
    {
        private static Type _mode;
        private static Type _hangar;
        private static bool _claimed;

        private static bool _patched;

        internal static void Claim()
        {
            _mode = FindType("Oritasy.InfantryModeService");
            _hangar = FindType("Oritasy.InfantryHangarMenu");
            if (_mode == null)
                return;
            try
            {
                PropertyInfo p = _mode.GetProperty("ExternalHost",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite)
                    p.SetValue(null, true, null);
            }
            catch (Exception ex)
            {
                if (GaPlugin.Log != null)
                    GaPlugin.Log.LogWarning("Oritasy ExternalHost: " + ex.Message);
            }
            if (GaPlugin.Log != null && !_claimed)
                GaPlugin.Log.LogInfo("Ground Arsenal is hosting infantry (Oritasy FPS paused)");
            _claimed = true;
        }

        internal static void PatchTicks(Harmony harmony)
        {
            if (harmony == null || _patched)
                return;
            if (_mode == null)
                Claim();
            if (_mode == null)
                return;
            _patched = true;
            SkipMethod(_mode, "Tick", harmony);
            SkipMethod(_mode, "Draw", harmony);
            SkipMethod(_hangar, "Draw", harmony);
        }

        private static void SkipMethod(Type t, string name, Harmony harmony)
        {
            if (t == null)
                return;
            MethodInfo m = t.GetMethod(name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null)
                return;
            try
            {
                harmony.Patch(m, new HarmonyMethod(typeof(GaOritasy).GetMethod("Skip",
                    BindingFlags.Static | BindingFlags.NonPublic)));
            }
            catch (Exception ex)
            {
                if (GaPlugin.Log != null)
                    GaPlugin.Log.LogWarning("Patch " + t.Name + "." + name + ": " + ex.Message);
            }
        }

        private static bool Skip()
        {
            return false;
        }

        private static Type FindType(string fullName)
        {
            Type t = Type.GetType(fullName + ", Oritasy", false);
            if (t != null)
                return t;
            Assembly[] all = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < all.Length; i++)
            {
                try { t = all[i].GetType(fullName, false); }
                catch { t = null; }
                if (t != null)
                    return t;
            }
            return null;
        }
    }
}
