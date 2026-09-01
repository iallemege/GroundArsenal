using System;
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
        internal const string Version = "1.0.0";
    }

    [BepInPlugin(GaInfo.GUID, GaInfo.Name, GaInfo.Version)]
    [BepInDependency("com.iallemege.oritasy", BepInDependency.DependencyFlags.SoftDependency)]
    public class GaPlugin : BaseUnityPlugin
    {
        internal static GaPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            _harmony = new Harmony(GaInfo.GUID);
            _harmony.PatchAll();
            GaOritasy.Claim();
            GaOritasy.PatchTicks(_harmony);
            try { SceneManager.sceneLoaded += OnScene; }
            catch { }
            Logger.LogInfo(GaInfo.Name + " " + GaInfo.Version
                + " — 300 guns, no Oritasy compile reference");
        }

        private void Start()
        {
            GaOritasy.Claim();
            GaOritasy.PatchTicks(_harmony);
        }

        private void Update()
        {
            GaMode.Tick();
        }

        private void OnGUI()
        {
            if (GaMode.IsMenuScene())
                return;
            GaMode.Draw();
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
