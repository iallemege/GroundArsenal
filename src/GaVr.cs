using UnityEngine;
using UnityEngine.XR;

namespace GroundArsenal
{
    /// <summary>
    /// Quest 3S / Touch via NOVR OpenXR. Head aims, left stick walks, right stick turns.
    /// Right trigger fires, left trigger aims, grip reloads, A jumps, left-stick click sprints.
    /// </summary>
    internal static class GaVr
    {
        internal static bool Active;
        internal static Vector3 MoveWish;
        internal static float Turn;
        internal static bool FireHeld;
        internal static bool FireDown;
        internal static bool AdsHeld;
        internal static bool ReloadDown;
        internal static bool JumpDown;
        internal static bool SprintDown;
        internal static bool SprintHeld;
        internal static bool DropDown;
        internal static bool GrabDown;

        private static bool _prevFire;
        private static bool _prevGrip;
        private static bool _prevJump;
        private static bool _prevSprint;
        private static bool _prevDrop;
        private static bool _logged;

        internal static void Sample()
        {
            FireDown = false;
            ReloadDown = false;
            JumpDown = false;
            SprintDown = false;
            DropDown = false;
            GrabDown = false;
            MoveWish = Vector3.zero;
            Turn = 0f;
            bool on = false;
            try
            {
                on = XRSettings.enabled && XRSettings.isDeviceActive;
            }
            catch
            {
                on = false;
            }
            Active = on;
            if (!on)
            {
                FireHeld = false;
                AdsHeld = false;
                return;
            }
            if (!_logged)
            {
                _logged = true;
                GaPlugin.Log.LogInfo("Ground Arsenal VR on (" + XRSettings.loadedDeviceName + "). Quest Touch: RT fire, LT ADS, grip reload, A jump.");
            }

            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool fire = false;
            bool ads = false;
            bool grip = false;
            bool jump = false;
            bool sprintClick = false;
            bool drop = false;
            Vector2 leftStick = Vector2.zero;
            Vector2 rightStick = Vector2.zero;
            if (right.isValid)
            {
                right.TryGetFeatureValue(CommonUsages.triggerButton, out fire);
                right.TryGetFeatureValue(CommonUsages.gripButton, out grip);
                right.TryGetFeatureValue(CommonUsages.primaryButton, out jump);
                right.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightStick);
            }
            if (left.isValid)
            {
                left.TryGetFeatureValue(CommonUsages.triggerButton, out ads);
                left.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out sprintClick);
                left.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftStick);
                left.TryGetFeatureValue(CommonUsages.secondaryButton, out drop);
            }
            FireDown = fire && !_prevFire;
            _prevFire = fire;
            FireHeld = fire;
            AdsHeld = ads;
            ReloadDown = grip && !_prevGrip;
            GrabDown = ReloadDown;
            _prevGrip = grip;
            DropDown = drop && !_prevDrop;
            _prevDrop = drop;
            JumpDown = jump && !_prevJump;
            _prevJump = jump;
            SprintDown = sprintClick && !_prevSprint;
            SprintHeld = sprintClick;
            _prevSprint = sprintClick;
            if (leftStick.sqrMagnitude > 0.04f)
                MoveWish = new Vector3(leftStick.x, 0f, leftStick.y);
            Turn = Mathf.Abs(rightStick.x) > 0.2f ? rightStick.x : 0f;
        }

        internal static bool Fire()
        {
            if (GaPalmMenu.Open)
                return false;
            return Active ? FireHeld : Input.GetMouseButton(0);
        }

        internal static bool FirePressed()
        {
            if (GaPalmMenu.Open)
                return false;
            return Active ? FireDown : Input.GetMouseButtonDown(0);
        }

        internal static bool Ads()
        {
            return Active ? AdsHeld : Input.GetMouseButton(1);
        }

        internal static void Follow(Camera cam, Transform eye)
        {
            if (!Active || cam == null || eye == null)
                return;
            Transform rig = cam.transform.parent;
            if (rig == null)
            {
                cam.transform.position = eye.position;
                return;
            }
            if (Mathf.Abs(Turn) > 0.01f)
                rig.RotateAround(cam.transform.position, Vector3.up, Turn * 140f * Time.deltaTime);
            rig.position += eye.position - cam.transform.position;
        }

        internal static bool ApplyLook(Transform pawn, Transform eye, Camera cam)
        {
            if (!Active || cam == null)
                return false;
            if (pawn != null)
            {
                Vector3 flat = cam.transform.forward;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f)
                    pawn.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
            if (eye != null)
                eye.rotation = cam.transform.rotation;
            return true;
        }

        internal static Vector3 HandWorld(Transform eye)
        {
            Camera cam = GaPerfFrameGate.Cam();
            if (Active && cam != null && cam.transform.parent != null)
            {
                InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                Vector3 local;
                if (right.isValid && right.TryGetFeatureValue(CommonUsages.devicePosition, out local))
                    return cam.transform.parent.TransformPoint(local);
            }
            if (eye == null)
                return Vector3.zero;
            return eye.position + eye.forward * 0.42f + Vector3.down * 0.12f;
        }
    }
}
