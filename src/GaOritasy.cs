using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

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
        internal static bool HasModularArmor;

        internal static void Detect()
        {
            HasModularArmor = FindType("Oritasy.ModularArmorService") != null;
        }

        internal static void Claim()
        {
            _mode = FindType("Oritasy.InfantryModeService");
            _hangar = FindType("Oritasy.InfantryHangarMenu");
            Detect();
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

        internal static bool NukeOverlayActive()
        {
            return ReadBool("Oritasy.NukeFlashService", "Active");
        }

        internal static bool EmpStunned()
        {
            if (NukeOverlayActive())
                return true;
            return CallBool("Oritasy.EmpPulseService", "LocalPlayerStunned");
        }

        private static bool ReadBool(string typeName, string prop)
        {
            Type t = FindType(typeName);
            if (t == null)
                return false;
            try
            {
                PropertyInfo p = t.GetProperty(prop,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (p == null)
                    return false;
                object v = p.GetValue(null, null);
                return v is bool && (bool)v;
            }
            catch
            {
                return false;
            }
        }

        private static bool CallBool(string typeName, string method)
        {
            Type t = FindType(typeName);
            if (t == null)
                return false;
            try
            {
                MethodInfo m = t.GetMethod(method,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (m == null)
                    return false;
                object v = m.Invoke(null, null);
                return v is bool && (bool)v;
            }
            catch
            {
                return false;
            }
        }

        internal static void RememberShot(int penGrade, Vector3 incoming, bool pureKe)
        {
            Type t = FindType("Oritasy.ModularArmorService");
            if (t == null)
                return;
            try
            {
                MethodInfo m = t.GetMethod("RememberInfantryShot",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(int), typeof(Vector3), typeof(bool) },
                    null);
                if (m != null)
                    m.Invoke(null, new object[] { penGrade, incoming, pureKe });
            }
            catch { }
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
