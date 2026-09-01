using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GroundArsenal
{
    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    internal static class GaPatch_Axis
    {
        private static readonly FieldInfo AircraftField =
            AccessTools.Field(typeof(PilotBaseState), "aircraft");

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(PilotPlayerState __instance)
        {
            if (!GaMode.Active)
                return true;
            Zero(__instance);
            return false;
        }

        private static void Zero(PilotPlayerState state)
        {
            if (state == null)
                return;
            try
            {
                state.pitchInput = 0f;
                state.rollInput = 0f;
                state.yawInput = 0f;
            }
            catch { }
            Aircraft ac = null;
            try
            {
                ac = AircraftField != null
                    ? AircraftField.GetValue(state) as Aircraft : null;
            }
            catch { }
            GaMode.SilenceAircraft(ac, !GaMode.AirHop);
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerControls")]
    internal static class GaPatch_Controls
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return !GaMode.Active;
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerThrottleAxis1Controls")]
    internal static class GaPatch_Throttle
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return !GaMode.Active;
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "FixedUpdateState")]
    internal static class GaPatch_Fixed
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return !GaMode.Active;
        }
    }

    [HarmonyPatch(typeof(CameraStateManager), "LateUpdate")]
    internal static class GaPatch_Camera
    {
        [HarmonyPostfix]
        private static void Postfix(CameraStateManager __instance)
        {
            GaMode.ApplyCamera(__instance);
        }
    }
}
