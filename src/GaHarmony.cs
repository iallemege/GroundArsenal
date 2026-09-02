using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
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

    [HarmonyPatch(typeof(PilotDismounted), "Update")]
    internal static class GaPatch_DummyUpdate
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return !GaMode.Active;
        }
    }

    [HarmonyPatch(typeof(PilotDismounted), "FixedUpdate")]
    internal static class GaPatch_DummyFixed
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return !GaMode.Active;
        }
    }

    [HarmonyPatch(typeof(PilotDismounted), "LocalFixedUpdate")]
    internal static class GaPatch_DummyLocalFixed
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return !GaMode.Active;
        }
    }

    [HarmonyPatch(typeof(DamageEffects), "ArmorPenetrate")]
    internal static class GaPatch_ArmorPenetrate
    {
        [HarmonyPostfix]
        private static void Postfix(Vector3 position, Vector3 velocity, float muzzleVelocity,
            float pierceDamage, float blastDamage, PersistentID dealerID)
        {
            GaMode.TryAbsorbWorldHit(position, velocity, pierceDamage, blastDamage, dealerID);
        }
    }

    [HarmonyPatch(typeof(FlightHud), "EnableCanvas")]
    internal static class GaPatch_FlightHudCanvas
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(bool enable)
        {
            if (GaMode.Active && enable)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(CameraStateManager), "SwitchState")]
    internal static class GaPatch_CamSwitch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(CameraStateManager __instance, CameraBaseState state)
        {
            return !GaMode.BlockCockpitSwitch(__instance, state);
        }
    }

    [HarmonyPatch(typeof(NuclearOption.Networking.Player), "SetAircraft")]
    internal static class GaPatch_PlayerSetAircraft
    {
        [HarmonyPostfix]
        private static void Postfix(NuclearOption.Networking.Player __instance, Aircraft aircraft)
        {
            GaMode.OnPlayerSetAircraft(__instance, aircraft);
        }
    }

    [HarmonyPatch(typeof(Aircraft), "SetupLocalPlayerAndUI")]
    internal static class GaPatch_AircraftLocalUi
    {
        [HarmonyPostfix]
        private static void Postfix(Aircraft __instance)
        {
            GaMode.OnLocalAircraftUi(__instance);
        }
    }

    [HarmonyPatch(typeof(CombatHUD), "SetAircraft")]
    internal static class GaPatch_CombatHudSet
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return !GaMode.Active;
        }
    }
}
