namespace GroundArsenal
{
    internal static class VrHandoff
    {
        private static bool _checked;
        private static bool _loaded;

        internal static bool OritasyVrLoaded()
        {
            if (_checked)
                return _loaded;
            _checked = true;
            try
            {
                _loaded = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.iallemege.oritasyvr");
            }
            catch
            {
                _loaded = false;
            }
            return _loaded;
        }
    }
}
