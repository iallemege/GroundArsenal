using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using XrDevice = UnityEngine.XR.InputDevice;
using XrUsages = UnityEngine.XR.CommonUsages;

namespace GroundArsenal
{
    /// <summary>
    /// NOVR's menu cursor copies the desktop mouse. Quest Touch never reaches it.
    /// Point the right controller and pull the trigger to move and click that cursor.
    /// </summary>
    internal static class GaControllerPointer
    {
        private static bool _logged;
        private static Vector2 _stickCursor;
        private static bool _haveStick;

        internal static void Tick()
        {
            try
            {
                Apply();
            }
            catch (System.Exception ex)
            {
                if (!_logged && GaPlugin.Log != null)
                {
                    _logged = true;
                    GaPlugin.Log.LogWarning("Controller pointer: " + ex.Message);
                }
            }
        }

        private static void Apply()
        {
            if (VrHandoff.OritasyVrLoaded() || GaVtol.StickHeld)
                return;
            if (!UnityEngine.XR.XRSettings.enabled || !UnityEngine.XR.XRSettings.isDeviceActive)
                return;
            Camera cam = GaPerfFrameGate.Cam();
            if (cam == null || cam.transform.parent == null)
                return;
            XrDevice right = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            if (!right.isValid)
                return;
            Vector3 localPos;
            Quaternion localRot;
            if (!right.TryGetFeatureValue(XrUsages.devicePosition, out localPos))
                return;
            if (!right.TryGetFeatureValue(XrUsages.deviceRotation, out localRot))
                return;

            Transform rig = cam.transform.parent;
            Vector3 origin = rig.TransformPoint(localPos);
            Vector3 dir = (rig.rotation * localRot) * Vector3.forward;
            if (Vector3.Dot(dir, cam.transform.forward) < 0.05f)
                dir = -dir;

            Vector2 screen;
            Ray ray = new Ray(origin, dir);
            Plane plane = new Plane(-cam.transform.forward, cam.transform.position + cam.transform.forward * 2.2f);
            float enter;
            if (plane.Raycast(ray, out enter) && enter > 0.05f && enter < 12f)
            {
                Vector3 world = ray.GetPoint(enter);
                Vector3 sp = cam.WorldToScreenPoint(world);
                screen = new Vector2(sp.x, sp.y);
            }
            else
            {
                if (!_haveStick)
                {
                    _stickCursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                    _haveStick = true;
                }
                Vector2 stick;
                if (right.TryGetFeatureValue(XrUsages.primary2DAxis, out stick) && stick.sqrMagnitude > 0.04f)
                    _stickCursor += stick * 900f * Time.unscaledDeltaTime;
                _stickCursor.x = Mathf.Clamp(_stickCursor.x, 0f, Screen.width);
                _stickCursor.y = Mathf.Clamp(_stickCursor.y, 0f, Screen.height);
                screen = _stickCursor;
            }

            bool trigger = false;
            right.TryGetFeatureValue(XrUsages.triggerButton, out trigger);
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;
            InputState.Change(mouse, new MouseState
            {
                position = screen,
                buttons = trigger ? (ushort)1 : (ushort)0
            });
            if (!_logged && GaPlugin.Log != null)
            {
                _logged = true;
                GaPlugin.Log.LogInfo("Quest right controller is driving the NOVR cursor. Point and pull the trigger.");
            }
        }
    }
}
