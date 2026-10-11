using BepInEx.Configuration;

namespace GroundArsenal
{
    internal static class GaConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> PauseOritasy;
        internal static ConfigEntry<bool> ShowTracers;
        internal static ConfigEntry<bool> ShowPerf;

        internal static void Bind(ConfigFile config)
        {
            if (config == null)
                return;
            Enabled = config.Bind("Infantry", "Enabled", true,
                "Standalone FPS infantry. Off = no hangar / carrier / cargo deploy.");
            PauseOritasy = config.Bind("Infantry", "PauseOritasyFps", true,
                "Pause Oritasy infantry Tick/Draw so only Ground Arsenal runs FPS.");
            ShowTracers = config.Bind("Combat", "ShowTracers", true,
                "Shot streaks. Slug trails always draw; this keeps extra flash lines.");
            ShowPerf = config.Bind("Debug", "ShowPerf", true,
                "FPS / spark / mesh overlay while infantry is active or hangar is open.");
        }

        internal static bool IsEnabled()
        {
            return Enabled == null || Enabled.Value;
        }

        internal static void SetEnabled(bool on)
        {
            if (Enabled != null)
                Enabled.Value = on;
            if (!on && GaMode.Active)
                GaMode.Shutdown();
        }

        internal static bool WantPauseOritasy()
        {
            return IsEnabled() && (PauseOritasy == null || PauseOritasy.Value);
        }

        internal static bool WantTracers()
        {
            return ShowTracers != null && ShowTracers.Value;
        }

        internal static bool WantPerf()
        {
            return ShowPerf == null || ShowPerf.Value;
        }
    }
}
