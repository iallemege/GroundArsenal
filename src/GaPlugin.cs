using System;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GroundArsenal
{
    internal static class GaInfo
    {
        internal const string GUID = "com.iallemege.groundarsenal";
        internal const string Name = "Ground Arsenal";
        internal const string Version = "1.0.5";
    }

    [BepInPlugin(GaInfo.GUID, GaInfo.Name, GaInfo.Version)]
    [BepInDependency("bia.runtime")]
    public class GaPlugin : BaseUnityPlugin
    {
        internal static GaPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;

        // Everything after config runs on GaHost. With BepInEx's default HideManagerGameObject = false
        // the game destroys the BepInEx manager object on its first scene load, taking this component
        // (and its Start/Update/OnGUI) with it.
        private void Awake()
        {
            Instance = this;
            Log = Logger;
            GaConfig.Bind(Config);
            GaDeploy.Load();
            GaForeignGuard.DisableOthers(Logger);
            try { SceneManager.sceneLoaded += OnScene; }
            catch { }
            GameObject host = new GameObject("GroundArsenal.Host") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<GaHost>();
            Logger.LogInfo(GaInfo.Name + " " + GaInfo.Version
                + " — infantry (requires BIA Runtime) "
                + (GaConfig.IsEnabled() ? "ON" : "OFF")
                + ", " + GaCatalog.Count + " modeled guns");
        }

        private static void OnScene(Scene scene, LoadSceneMode mode)
        {
            if (GaMode.IsMenuSceneName(scene.name))
                GaMode.Shutdown();
        }
    }

    /// <summary>Ground Arsenal's frame loop, on a hidden object that survives scene loads.</summary>
    [DefaultExecutionOrder(-2000)]
    internal sealed class GaHost : MonoBehaviour
    {
        private Harmony _harmony;
        private bool _patched;

        /// <summary>
        /// Patching waits for the game world. On a clean install nothing has touched the vehicle job
        /// struct (GroundVehicleFields) before the first world loads, and Mono crashes natively when
        /// that type is first set up by Harmony copying a method body (or by any early JIT of code
        /// that names it). Once the world has run its own vehicle jobs for a few seconds the type is
        /// set up the game's way and patching is safe. Every patch target is in-world code.
        /// </summary>
        private void PatchWhenWorldRuns()
        {
            Scene s = SceneManager.GetActiveScene();
            if (!s.IsValid() || GaMode.IsMenuSceneName(s.name)) return;
            float wait = s.name == "GameWorld" ? 8f : 20f;
            if (Time.timeSinceLevelLoad < wait) return;
            _patched = true;
            _harmony = new Harmony(GaInfo.GUID);
            int bad = 0;
            foreach (Type t in AccessTools.GetTypesFromAssembly(typeof(GaPlugin).Assembly))
            {
                if (!t.IsDefined(typeof(HarmonyPatch), false)) continue;
                try { _harmony.CreateClassProcessor(t).Patch(); }
                catch (Exception ex)
                {
                    bad++;
                    GaPlugin.Log.LogWarning("skip patch " + t.Name + ": " + ex.Message);
                }
            }
            GaOritasy.Detect();
            if (GaConfig.WantPauseOritasy())
            {
                GaOritasy.Claim();
                GaOritasy.PatchTicks(_harmony);
            }
            GaPlugin.Log.LogInfo(GaInfo.Name + " patches applied in " + s.name + (bad > 0 ? ", " + bad + " skipped" : ""));
        }

        [SkipLocalsInit]
        private void Update()
        {
            if (!_patched) PatchWhenWorldRuns();
            GaPerfFrameGate.BeginFrame();
            GaVtol.Tick();
            GaControllerPointer.Tick();
            GaNet.Tick();
            GaMode.Tick();
        }

        [SkipLocalsInit]
        private void OnGUI()
        {
            GaPerfFrameGate.BeginFrame();
            try
            {
                if (GaMode.IsMenuScene())
                    return;
                Event e = Event.current;
                EventType t = e != null ? e.type : EventType.Repaint;
                bool menu = GaHud.BlocksLook || GaDeploy.Open
                    || GaHangar.HangarSelectOpen() || GaHangar.MenuOpen
                    || GaHangar.DeathPick || GaHangar.FieldOpen;
                if (!menu && t != EventType.Repaint)
                    return;
                GaMode.Draw();
            }
            catch (Exception ex)
            {
                GaPlugin.Log?.LogWarning("GroundArsenal OnGUI: " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            GaMode.Shutdown();
            if (_harmony != null)
                _harmony.UnpatchSelf();
        }
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
    public sealed class SkipLocalsInitAttribute : Attribute
    {
    }
}
