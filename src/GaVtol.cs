using UnityEngine;
using UnityEngine.XR;

namespace GroundArsenal
{
    /// <summary>
    /// VTOL-style Quest Touch. Right grip grabs the stick (hand offset is pitch and roll,
    /// wrist twist is rudder). Left grip grabs the throttle. Index triggers fire and drop flares.
    /// </summary>
    internal static class GaVtol
    {
        internal static bool Flying;
        internal static bool StickHeld;
        internal static float Pitch;
        internal static float Roll;
        internal static float Yaw;
        internal static float Throttle = 0.7f;
        internal static float Brake;

        private const float StickTravel = 0.11f;
        private const float ThrottleTravel = 0.22f;
        private const float YawDegrees = 28f;

        private static bool _prevRightGrip;
        private static bool _prevLeftGrip;
        private static bool _prevTrigger;
        private static bool _prevCm;
        private static bool _prevNext;
        private static bool _haveStick;
        private static bool _haveThrottle;
        private static Vector3 _stickGrab;
        private static float _stickGrabYaw;
        private static float _throttleGrabY;
        private static float _throttleGrabValue;
        private static bool _logged;

        internal static void Tick()
        {
            if (VrHandoff.OritasyVrLoaded())
            {
                Flying = false;
                StickHeld = false;
                return;
            }
            Flying = false;
            StickHeld = false;
            Pitch = 0f;
            Roll = 0f;
            Yaw = 0f;
            Brake = 0f;
            if (GaMode.Active)
                return;
            if (!XRSettings.enabled || !XRSettings.isDeviceActive)
                return;
            Camera cam = GaPerfFrameGate.Cam();
            if (cam == null || cam.transform.parent == null)
                return;
            Aircraft ac = null;
            try
            {
                if (!GameManager.GetLocalAircraft(out ac) || ac == null)
                    return;
            }
            catch
            {
                return;
            }
            Flying = true;
            Transform rig = cam.transform.parent;
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool rightGrip = false;
            bool leftGrip = false;
            bool trigger = false;
            bool cm = false;
            bool next = false;
            bool brake = false;
            Vector3 rightPos = Vector3.zero;
            Quaternion rightRot = Quaternion.identity;
            float leftY = 0f;
            bool haveRight = TryHand(rig, right, out rightPos, out rightRot);
            bool haveLeft = false;
            Vector3 leftPos;
            Quaternion leftRot;
            if (TryHand(rig, left, out leftPos, out leftRot))
            {
                haveLeft = true;
                leftY = cam.transform.InverseTransformPoint(leftPos).y;
            }
            if (right.isValid)
            {
                right.TryGetFeatureValue(CommonUsages.gripButton, out rightGrip);
                right.TryGetFeatureValue(CommonUsages.triggerButton, out trigger);
                right.TryGetFeatureValue(CommonUsages.primaryButton, out next);
            }
            if (left.isValid)
            {
                left.TryGetFeatureValue(CommonUsages.gripButton, out leftGrip);
                left.TryGetFeatureValue(CommonUsages.triggerButton, out cm);
                left.TryGetFeatureValue(CommonUsages.secondaryButton, out brake);
            }

            if (haveRight && rightGrip)
            {
                Vector3 local = cam.transform.InverseTransformPoint(rightPos);
                float yawNow = SignedYaw(cam.transform.rotation, rightRot);
                if (!_haveStick || !_prevRightGrip)
                {
                    _stickGrab = local;
                    _stickGrabYaw = yawNow;
                    _haveStick = true;
                }
                Vector3 delta = local - _stickGrab;
                Roll = Mathf.Clamp(delta.x / StickTravel, -1f, 1f);
                Pitch = Mathf.Clamp(-delta.z / StickTravel, -1f, 1f);
                Yaw = Mathf.Clamp(Mathf.DeltaAngle(_stickGrabYaw, yawNow) / YawDegrees, -1f, 1f);
                StickHeld = true;
            }
            else if (!rightGrip)
            {
                _haveStick = false;
            }

            if (haveLeft && leftGrip)
            {
                if (!_haveThrottle || !_prevLeftGrip)
                {
                    _throttleGrabY = leftY;
                    _throttleGrabValue = Throttle;
                    _haveThrottle = true;
                }
                Throttle = Mathf.Clamp01(_throttleGrabValue + (leftY - _throttleGrabY) / ThrottleTravel);
            }
            else if (!leftGrip)
            {
                _haveThrottle = false;
            }

            if (brake)
                Brake = 1f;

            if (trigger && StickHeld)
                Fire(ac);
            if (cm && !_prevCm)
                Flares(ac, true);
            else if (!cm && _prevCm)
                Flares(ac, false);
            if (next && !_prevNext)
                Cycle(ac);

            _prevRightGrip = rightGrip;
            _prevLeftGrip = leftGrip;
            _prevTrigger = trigger;
            _prevCm = cm;
            _prevNext = next;

            if (!_logged && (right.isValid || left.isValid) && GaPlugin.Log != null)
            {
                _logged = true;
                GaPlugin.Log.LogInfo("VTOL controls: right grip stick, twist rudder, trigger fire. Left grip throttle, left trigger flares, Y brake, A next weapon.");
            }
        }

        private static bool TryHand(Transform rig, InputDevice device, out Vector3 worldPos, out Quaternion worldRot)
        {
            worldPos = Vector3.zero;
            worldRot = Quaternion.identity;
            if (!device.isValid)
                return false;
            Vector3 localPos;
            Quaternion localRot;
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out localPos))
                return false;
            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out localRot))
                return false;
            worldPos = rig.TransformPoint(localPos);
            worldRot = rig.rotation * localRot;
            return true;
        }

        private static float SignedYaw(Quaternion head, Quaternion hand)
        {
            Vector3 flat = Vector3.ProjectOnPlane(hand * Vector3.forward, head * Vector3.up);
            if (flat.sqrMagnitude < 0.0001f)
                return 0f;
            return Vector3.SignedAngle(head * Vector3.forward, flat.normalized, head * Vector3.up);
        }

        private static Pilot ControlledPilot(Aircraft ac)
        {
            if (ac == null)
                return null;
            Pilot[] pilots = ac.GetComponentsInChildren<Pilot>(true);
            for (int i = 0; i < pilots.Length; i++)
            {
                if (pilots[i] != null && pilots[i].playerControlled && !pilots[i].dead)
                    return pilots[i];
            }
            return pilots.Length > 0 ? pilots[0] : null;
        }

        private static void Fire(Aircraft ac)
        {
            Pilot pilot = ControlledPilot(ac);
            if (pilot == null)
                return;
            try { pilot.Fire(); }
            catch { }
        }

        private static void Cycle(Aircraft ac)
        {
            Pilot pilot = ControlledPilot(ac);
            if (pilot == null)
                return;
            try { pilot.NextWeapon(); }
            catch { }
        }

        private static void Flares(Aircraft ac, bool on)
        {
            if (ac == null)
                return;
            try { ac.Countermeasures(on, 0); }
            catch { }
        }
    }
}
