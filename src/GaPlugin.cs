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
        internal const string Version = "1.0.4";
    }

    [DefaultExecutionOrder(-2000)]
    [BepInPlugin(GaInfo.GUID, GaInfo.Name, GaInfo.Version)]
    [BepInDependency("bia.runtime")]
    public class GaPlugin : BaseUnityPlugin
    {
        internal static GaPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            GaConfig.Bind(Config);
            GaDeploy.Load();
            GaForeignGuard.DisableOthers(Logger);
            _harmony = new Harmony(GaInfo.GUID);
            _harmony.PatchAll();
            GaOritasy.Detect();
            if (GaConfig.WantPauseOritasy())
            {
                GaOritasy.Claim();
                GaOritasy.PatchTicks(_harmony);
            }
            try { SceneManager.sceneLoaded += OnScene; }
            catch { }
            Logger.LogInfo(GaInfo.Name + " " + GaInfo.Version
                + " — infantry (requires BIA Runtime) "
                + (GaConfig.IsEnabled() ? "ON" : "OFF")
                + ", " + GaCatalog.Count + " modeled guns");
        }

        private void Start()
        {
            if (GaConfig.WantPauseOritasy())
            {
                GaOritasy.Claim();
                GaOritasy.PatchTicks(_harmony);
            }
        }

        [SkipLocalsInit]
        private void Update()
        {
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
                if (Log != null)
                    Log.LogWarning("GroundArsenal OnGUI: " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            GaMode.Shutdown();
            if (_harmony != null)
                _harmony.UnpatchSelf();
        }

        private static void OnScene(Scene scene, LoadSceneMode mode)
        {
            if (GaMode.IsMenuSceneName(scene.name))
                GaMode.Shutdown();
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
