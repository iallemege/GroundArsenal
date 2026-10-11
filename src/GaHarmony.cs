using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace GroundArsenal
{
    internal static class GaPilotAc
    {
        private static readonly FieldInfo AircraftField =
            AccessTools.Field(typeof(PilotBaseState), "aircraft");
        private static Aircraft _ac;
        private static PilotPlayerState _state;
        private static int _frame = -1;

        internal static Aircraft Of(PilotPlayerState state)
        {
            if (state == null)
                return null;
            int f = Time.frameCount;
            if (f == _frame && object.ReferenceEquals(state, _state))
                return _ac;
            _state = state;
            _frame = f;
            try
            {
                _ac = AircraftField != null
                    ? AircraftField.GetValue(state) as Aircraft : null;
            }
            catch
            {
                _ac = null;
            }
            return _ac;
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    internal static class GaPatch_VtolAxis
    {
        [HarmonyPostfix]
        private static void Postfix(PilotPlayerState __instance)
        {
            if (!GaVtol.Flying || __instance == null)
                return;
            __instance.pitchInput = GaVtol.Pitch;
            __instance.rollInput = GaVtol.Roll;
            __instance.yawInput = GaVtol.Yaw;
            try
            {
                Aircraft ac = GaPilotAc.Of(__instance);
                if (ac == null)
                    return;
                ControlInputs inputs = ac.GetInputs();
                if (inputs == null)
                    return;
                inputs.pitch = GaVtol.Pitch;
                inputs.roll = GaVtol.Roll;
                inputs.yaw = GaVtol.Yaw;
                inputs.brake = GaVtol.Brake;
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerThrottleAxis1Controls")]
    internal static class GaPatch_VtolThrottle
    {
        [HarmonyPostfix]
        private static void Postfix(PilotPlayerState __instance)
        {
            if (!GaVtol.Flying || __instance == null)
                return;
            try
            {
                Aircraft ac = GaPilotAc.Of(__instance);
                if (ac == null)
                    return;
                ControlInputs inputs = ac.GetInputs();
                if (inputs == null)
                    return;
                inputs.throttle = GaVtol.Throttle;
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    internal static class GaPatch_Axis
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(PilotPlayerState __instance)
        {
            if (GaAirLift.Flying)
                return false;
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
            GaMode.SilenceAircraft(GaPilotAc.Of(state), !GaMode.AirHop);
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerControls")]
    internal static class GaPatch_Controls
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            if (GaAirLift.Flying)
                return false;
            return !GaMode.Active;
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerThrottleAxis1Controls")]
    internal static class GaPatch_Throttle
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(PilotPlayerState __instance)
        {
            if (GaAirLift.Flying)
                return false;
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

    [HarmonyPatch(typeof(Aircraft), "FilterInputs")]
    internal static class GaPatch_LiftFilter
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Aircraft __instance)
        {
            if (!GaAirLift.Flying || !GaAirLift.Steer || __instance == null)
                return;
            if (!object.ReferenceEquals(__instance, GaAirLift.Ac))
                return;
            GaAirLift.PushPower(__instance);
        }
    }

    /// <summary>
    /// TV cam frames the target by distance, so FOV keeps moving after you zoom.
    /// Hold the zoom the player set and ignore that distance term.
    /// </summary>
    [HarmonyPatch(typeof(CameraTVState), "UpdateState")]
    internal static class GaPatch_TvFov
    {
        private static float _hold = -1f;
        private static int _unitId;

        [HarmonyPostfix]
        private static void Postfix(CameraStateManager cam)
        {
            if (GaMode.Active || cam == null || cam.mainCamera == null || cam.followingUnit == null)
                return;
            int id = 0;
            try { id = cam.followingUnit.GetInstanceID(); }
            catch { id = 0; }
            if (id != _unitId || _hold < 1f)
            {
                _unitId = id;
                _hold = cam.mainCamera.fieldOfView;
            }
            float axis = 0f;
            try { axis = Input.GetAxis("Mouse ScrollWheel"); }
            catch { axis = 0f; }
            if (axis > 0.01f || axis < -0.01f)
                _hold = Mathf.Clamp(_hold - axis * 80f, 5f, 80f);
            cam.mainCamera.fieldOfView = _hold;
        }
    }

    [HarmonyPatch(typeof(CameraStateManager), "LateUpdate")]
    internal static class GaPatch_Camera
    {
        [HarmonyPostfix]
        [SkipLocalsInit]
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
        private static bool Prefix(PilotDismounted __instance)
        {
            return !GaMode.IsMutedDummy(__instance);
        }
    }

    [HarmonyPatch(typeof(PilotDismounted), "FixedUpdate")]
    internal static class GaPatch_DummyFixed
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(PilotDismounted __instance)
        {
            return !GaMode.IsMutedDummy(__instance);
        }
    }

    [HarmonyPatch(typeof(PilotDismounted), "LocalFixedUpdate")]
    internal static class GaPatch_DummyLocalFixed
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(PilotDismounted __instance)
        {
            return !GaMode.IsMutedDummy(__instance);
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
            if (!GaAirLift.PassCamSwitch(__instance, state))
                return false;
            return !GaMode.BlockCockpitSwitch(__instance, state);
        }
    }

    [HarmonyPatch(typeof(Aircraft), "StartEjectionSequence")]
    internal static class GaPatch_BlockGroundEject
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Aircraft __instance)
        {
            return !GaAirLift.BlockGroundEject(__instance);
        }
    }

    [HarmonyPatch(typeof(Hangar), "TrySpawnAircraft")]
    internal static class GaPatch_HangarBuy
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Hangar __instance, NuclearOption.Networking.Player player, ref Airbase.TrySpawnResult __result)
        {
            bool local = false;
            try { local = player != null && player.IsLocalPlayer; }
            catch { local = false; }
            if (!local)
                return true;
            if (GaHangar.HangarSelectOpen())
                return true;
            Airbase ab = null;
            try { ab = __instance.GetComponentInParent<Airbase>(); }
            catch { ab = null; }
            if (GaForce.AllowBuyAt(ab))
                return true;
            __result = default(Airbase.TrySpawnResult);
            return false;
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

    [HarmonyPatch(typeof(CameraFreeState), "UpdateState")]
    internal static class GaPatch_CamFree
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return !GaMode.Active;
        }
    }

    [HarmonyPatch(typeof(CameraControlledState), "UpdateState")]
    internal static class GaPatch_CamControlled
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return !GaMode.Active;
        }
    }

    [HarmonyPatch(typeof(GroundVehicle), "UpdateJobFields_Pathfinder")]
    internal static class GaPatch_VehPath
    {
        [HarmonyPostfix]
        private static void Postfix(GroundVehicle __instance)
        {
            GaRide.InjectPath(__instance);
        }
    }

    [HarmonyPatch(typeof(GroundVehicle), "UpdateJobFields")]
    internal static class GaPatch_VehFields
    {
        [HarmonyPostfix]
        private static void Postfix(GroundVehicle __instance)
        {
            GaRide.InjectPath(__instance);
        }
    }

    [HarmonyPatch(typeof(GroundVehicle), "ApplyJobFields")]
    internal static class GaPatch_VehApply
    {
        [HarmonyPostfix]
        private static void Postfix(GroundVehicle __instance)
        {
            GaRide.AfterApply(__instance);
        }
    }

    [HarmonyPatch(typeof(GroundVehicle), "OnStationaryChanged")]
    internal static class GaPatch_VehStationary
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(GroundVehicle __instance, bool nowStationary)
        {
            return !GaRide.BlockStationary(__instance, nowStationary);
        }
    }

    [HarmonyPatch(typeof(Turret), "FixedUpdate")]
    internal static class GaPatch_TurretRide
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Turret __instance)
        {
            return !GaRide.HandleTurret(__instance);
        }
    }

    [HarmonyPatch(typeof(CombatHUD), "LateUpdate")]
    internal static class GaPatch_CombatHudLate
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(CombatHUD __instance)
        {
            if (!GaRide.Busy)
                return true;
            GaIff.HudLate(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(CombatHUD), "SelectUnit")]
    internal static class GaPatch_CombatHudSelect
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Unit unit)
        {
            if (!GaRide.Busy)
                return true;
            GaIff.LockUnit(unit);
            return false;
        }
    }

    [HarmonyPatch(typeof(CombatHUD), "DeselectLast")]
    internal static class GaPatch_CombatHudDeselectLast
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            if (!GaRide.Busy)
                return true;
            GaIff.DropLast();
            return false;
        }
    }

    [HarmonyPatch(typeof(CombatHUD), "DeselectAll")]
    internal static class GaPatch_CombatHudDeselectAll
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            if (!GaRide.Busy)
                return true;
            GaIff.DropAll();
            return false;
        }
    }

    [HarmonyPatch(typeof(CombatHUD), "DeSelectUnit")]
    internal static class GaPatch_CombatHudDeSelectUnit
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Unit unit)
        {
            if (!GaRide.Busy)
                return true;
            GaIff.UnlockUnit(unit);
            return false;
        }
    }

    [HarmonyPatch(typeof(ShipAI), "Update")]
    internal static class GaPatch_ShipAi
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ShipAI __instance)
        {
            return !GaRide.Helming(__instance);
        }
    }

    [HarmonyPatch(typeof(LandingCraftAI), "Update")]
    internal static class GaPatch_LandingAi
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(LandingCraftAI __instance)
        {
            return !GaRide.Helming(__instance);
        }
    }

    [HarmonyPatch(typeof(NuclearOption.Networking.Player), "ShowMap")]
    internal static class GaPatch_ShowMap
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            if (!GaMode.Active || GaMode.Dead)
                return true;
            GaHangar.ToggleField();
            return false;
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "Maximize")]
    internal static class GaPatch_MapMax
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return !GaMode.Active;
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "MapControls")]
    internal static class GaPatch_MapControls
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            return !GaMode.Active;
        }
    }

    [HarmonyPatch(typeof(AutopilotPlane), "AutoAim", new Type[]
    {
        typeof(GlobalPosition), typeof(bool), typeof(bool), typeof(bool),
        typeof(float), typeof(float), typeof(bool), typeof(float), typeof(Vector3)
    })]
    internal static class GaPatch_DropshipPlaneAim
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(
            AutopilotPlane __instance,
            ref GlobalPosition destination,
            ref bool aimVelocity,
            ref bool ignoreCollisions,
            ref float effort,
            ref float bankAllowed,
            ref bool followTerrain,
            ref float altitudeHold)
        {
            Vector3 aim;
            float hold;
            float bank;
            if (!GaAirLift.GuideDropship(__instance != null ? __instance.aircraft : null, out aim, out hold, out bank))
                return;
            destination = aim.ToGlobalPosition();
            aimVelocity = true;
            ignoreCollisions = true;
            followTerrain = false;
            if (effort > 0.42f)
                effort = 0.42f;
            if (bankAllowed > bank)
                bankAllowed = bank;
            altitudeHold = hold;
        }
    }

    [HarmonyPatch(typeof(AutopilotHelo), "AutoAim", new Type[]
    {
        typeof(GlobalPosition), typeof(float), typeof(Vector3), typeof(Vector3), typeof(bool)
    })]
    internal static class GaPatch_DropshipHeloAim
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(
            AutopilotHelo __instance,
            ref GlobalPosition destination,
            ref float altitudeHold,
            ref bool followTerrain)
        {
            Vector3 aim;
            float hold;
            float bank;
            if (!GaAirLift.GuideDropship(__instance != null ? __instance.aircraft : null, out aim, out hold, out bank))
                return;
            destination = aim.ToGlobalPosition();
            altitudeHold = hold;
            followTerrain = true;
        }
    }

    [HarmonyPatch(typeof(AutopilotTiltwing), "AutoAim", new Type[]
    {
        typeof(GlobalPosition), typeof(float), typeof(Vector3), typeof(Vector3), typeof(bool)
    })]
    internal static class GaPatch_DropshipTiltAim
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(
            AutopilotTiltwing __instance,
            ref GlobalPosition destination,
            ref float altitudeHold,
            ref bool followTerrain)
        {
            Vector3 aim;
            float hold;
            float bank;
            if (!GaAirLift.GuideDropship(__instance != null ? __instance.aircraft : null, out aim, out hold, out bank))
                return;
            destination = aim.ToGlobalPosition();
            altitudeHold = hold;
            followTerrain = true;
        }
    }
}
