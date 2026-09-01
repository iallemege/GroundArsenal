using System;
using NuclearOption.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GroundArsenal
{
    internal static class GaMode
    {
        internal static bool Active;
        internal static bool AirHop;
        internal static bool Dead;
        internal static bool Zh = true;
        internal static float Hp = MaxHp;
        internal const float MaxHp = 80f;
        internal static Transform Eye;
        internal static string DenyMsg;
        internal static float DenyUntil;
        internal static bool HangarQueued;

        private static GameObject _pawn;
        private static CharacterController _cc;
        private static Transform _eye;
        private static Aircraft _parked;
        private static float _yaw;
        private static float _pitch;
        private static float _vertVel;
        private static Camera _infCam;
        private static float _savedNear = -1f;
        private static bool _savedOcc;
        private static bool _haveSavedCam;
        private static CameraMode _savedMode;
        private static bool _savedLook;
        private static float _deadUntil;
        internal static Transform PawnRoot
        {
            get { return _pawn != null ? _pawn.transform : null; }
        }

        internal static Vector3 EyePos
        {
            get { return _eye != null ? _eye.position : Vector3.zero; }
        }

        internal static Vector3 EyeFwd
        {
            get { return _eye != null ? _eye.forward : Vector3.forward; }
        }

        internal static bool IsMenuScene()
        {
            try { return IsMenuSceneName(SceneManager.GetActiveScene().name); }
            catch { return false; }
        }

        internal static bool IsMenuSceneName(string n)
        {
            if (string.IsNullOrEmpty(n))
                return false;
            return n.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static void Tick()
        {
            if (IsMenuScene())
            {
                Shutdown();
                return;
            }
            if (HangarQueued)
            {
                HangarQueued = false;
                TryHangarSpawn();
            }
            if (!Active)
            {
                if (Input.GetKeyDown(KeyCode.U))
                    TryHopOut();
                return;
            }
            HoldFlightControls();
            FreezeAircraft();
            if (Dead)
            {
                if (Time.unscaledTime >= _deadUntil
                    || Input.GetKeyDown(KeyCode.Space)
                    || Input.GetKeyDown(KeyCode.Escape))
                    Exit(false);
                return;
            }
            GaHud.Tick();
            if (!GaHud.BlocksLook)
            {
                LookMove();
                if (_eye != null)
                    GaCombat.Tick(_eye, Time.deltaTime);
            }
            if (Input.GetKeyDown(KeyCode.U))
                TryBoard();
        }

        internal static void Draw()
        {
            if (!Active)
            {
                GaHangar.Draw();
                return;
            }
            GaHud.Draw();
        }

        internal static void Shutdown()
        {
            if (Active)
                Exit(false);
            Dead = false;
            GaHud.CloseMenus();
        }

        internal static void Deny(string en, string zh)
        {
            DenyMsg = Zh ? zh : en;
            DenyUntil = Time.unscaledTime + 3f;
        }

        internal static void ApplyCamera(CameraStateManager mgr)
        {
            if (!Active || _eye == null || mgr == null)
                return;
            Camera cam = mgr.mainCamera;
            if (cam == null)
                cam = Camera.main;
            if (cam == null)
                return;
            cam.transform.position = _eye.position;
            cam.transform.rotation = _eye.rotation;
            if (!_haveSavedCam || _infCam != cam)
            {
                if (_infCam != null && _infCam != cam)
                    RestoreCameraClip();
                _infCam = cam;
                _savedNear = cam.nearClipPlane;
                _savedOcc = cam.useOcclusionCulling;
                _haveSavedCam = true;
            }
            if (cam.nearClipPlane > 0.035f)
                cam.nearClipPlane = 0.03f;
        }

        internal static void HoldFlightControls()
        {
            try { GameManager.flightControlsEnabled = false; }
            catch { }
        }

        internal static void SilenceAircraft(Aircraft ac, bool park)
        {
            if (ac == null)
                return;
            try
            {
                ControlInputs inputs = ac.GetInputs();
                if (inputs != null)
                {
                    inputs.pitch = 0f;
                    inputs.roll = 0f;
                    inputs.yaw = 0f;
                    inputs.customAxis1 = 0f;
                    if (park)
                    {
                        inputs.throttle = 0f;
                        inputs.brake = 1f;
                    }
                }
            }
            catch { }
            if (!park)
                return;
            try
            {
                if (ac.rb != null)
                {
                    ac.rb.velocity = Vector3.zero;
                    ac.rb.angularVelocity = Vector3.zero;
                }
            }
            catch { }
        }

        internal static void Hurt(float amount)
        {
            if (!Active || Dead)
                return;
            Hp -= amount;
            if (Hp < 0f)
                Hp = 0f;
            if (Hp <= 0f)
            {
                Dead = true;
                _deadUntil = Time.unscaledTime + 6f;
                Deny("KIA — Space / Esc to leave.", "阵亡 — 空格或 Esc 离开。");
            }
        }

        private static void FreezeAircraft()
        {
            if (_parked == null)
                return;
            SilenceAircraft(_parked, !AirHop);
        }

        private static void LookMove()
        {
            float sens = 2.4f;
            _yaw += Input.GetAxis("Mouse X") * sens;
            _pitch -= Input.GetAxis("Mouse Y") * sens;
            if (_pitch < -85f) _pitch = -85f;
            if (_pitch > 85f) _pitch = 85f;
            if (_pawn != null)
                _pawn.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_eye != null)
                _eye.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            float walk = 4.8f;
            float sprint = 7.4f;
            float speed = Input.GetKey(KeyCode.LeftShift) ? sprint : walk;
            Vector3 wish = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) wish += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) wish += Vector3.back;
            if (Input.GetKey(KeyCode.A)) wish += Vector3.left;
            if (Input.GetKey(KeyCode.D)) wish += Vector3.right;
            if (wish.sqrMagnitude > 1f)
                wish.Normalize();
            Vector3 world = _pawn != null
                ? _pawn.transform.TransformDirection(wish) * speed
                : wish * speed;
            if (_cc == null)
                return;
            if (_cc.isGrounded)
            {
                _vertVel = -2f;
                if (Input.GetKeyDown(KeyCode.Space))
                    _vertVel = 6.2f;
            }
            _vertVel += Physics.gravity.y * Time.deltaTime;
            world.y = _vertVel;
            _cc.Move(world * Time.deltaTime);
        }

        internal static void TryHangarSpawn()
        {
            if (Active)
                return;
            Vector3 pos;
            float yaw;
            if (!GaHangar.TrySpawn(out pos, out yaw))
            {
                Deny("No hangar spawn point.", "找不到机库出生点。");
                return;
            }
            GaHangar.CloseUi();
            SpawnPawn(pos, yaw);
            GaCombat.ApplyHangarKit();
            GaCombat.AttachViews(_eye);
        }

        private static void TryHopOut()
        {
            if (Active)
                return;
            Aircraft ac;
            if (!GameManager.GetLocalAircraft(out ac) || ac == null)
            {
                Deny("No local aircraft.", "没有本机。");
                return;
            }
            Vector3 pos = ac.transform.position;
            float yaw = ac.transform.eulerAngles.y;
            float ralt = 8f;
            try { ralt = ac.transform.position.y; }
            catch { }
            AirHop = false;
            try
            {
                RaycastHit hit;
                if (Physics.Raycast(pos + Vector3.up, Vector3.down, out hit, 80f,
                    ~0, QueryTriggerInteraction.Ignore))
                {
                    float agl = pos.y - hit.point.y;
                    AirHop = agl > 8f;
                    if (!AirHop)
                        pos = hit.point + Vector3.up * 0.08f;
                    else
                        pos = pos + ac.transform.right * 2.2f;
                }
            }
            catch { }
            _parked = ac;
            Player p = null;
            try { GameManager.GetLocalPlayer(out p); }
            catch { }
            if (!AirHop && p != null)
            {
                try { p.RemoveAircraft(ac); }
                catch { }
            }
            SpawnPawn(pos, yaw);
            GaCombat.ApplyCarryOnly();
            GaCombat.AttachViews(_eye);
            HoldFlightControls();
            SilenceAircraft(ac, !AirHop);
        }

        private static void TryBoard()
        {
            if (_parked == null)
            {
                Deny("No marked aircraft.", "没有标记的飞机。");
                return;
            }
            Vector3 from = _pawn != null ? _pawn.transform.position : EyePos;
            float d = Vector3.Distance(from, _parked.transform.position);
            if (d > (AirHop ? 48f : 12f))
            {
                Deny("Too far from aircraft.", "离飞机太远。");
                return;
            }
            Aircraft ac = _parked;
            Exit(true);
            Player p = null;
            try { GameManager.GetLocalPlayer(out p); }
            catch { }
            try
            {
                if (p != null)
                    p.SetAircraft(ac);
            }
            catch { }
            try { CameraStateManager.cameraMode = CameraMode.cockpit; }
            catch { }
            try { CameraStateManager.enableMouseLook = true; }
            catch { }
            try { GameManager.flightControlsEnabled = true; }
            catch { }
        }

        private static void SpawnPawn(Vector3 spawn, float yaw)
        {
            if (_pawn != null)
                UnityEngine.Object.Destroy(_pawn);
            _pawn = new GameObject("GroundArsenalPawn");
            _pawn.transform.position = spawn;
            _yaw = yaw;
            _pitch = 0f;
            _pawn.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _cc = _pawn.AddComponent<CharacterController>();
            _cc.height = 1.8f;
            _cc.radius = 0.32f;
            _cc.center = new Vector3(0f, 0.9f, 0f);
            _cc.stepOffset = 0.35f;
            _cc.slopeLimit = 50f;
            _cc.minMoveDistance = 0f;
            GameObject eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(_pawn.transform, false);
            eyeGo.transform.localPosition = new Vector3(0f, 1.64f, 0f);
            _eye = eyeGo.transform;
            Eye = _eye;
            Hp = MaxHp;
            Dead = false;
            _vertVel = 0f;
            Active = true;
            HoldFlightControls();
            try { _savedMode = CameraStateManager.cameraMode; }
            catch { _savedMode = CameraMode.cockpit; }
            try { _savedLook = CameraStateManager.enableMouseLook; }
            catch { _savedLook = true; }
            try { CameraStateManager.cameraMode = CameraMode.free; }
            catch { }
            try { CameraStateManager.enableMouseLook = false; }
            catch { }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void Exit(bool toCockpit)
        {
            GaCombat.ClearBolts();
            GaCombat.DestroyViews();
            RestoreCameraClip();
            if (_pawn != null)
                UnityEngine.Object.Destroy(_pawn);
            _pawn = null;
            _cc = null;
            _eye = null;
            Eye = null;
            Active = false;
            Dead = false;
            AirHop = false;
            GaHud.CloseMenus();
            if (toCockpit)
            {
                try { CameraStateManager.cameraMode = _savedMode; }
                catch { CameraStateManager.cameraMode = CameraMode.cockpit; }
                try { CameraStateManager.enableMouseLook = _savedLook; }
                catch { }
            }
            if (!toCockpit)
                _parked = null;
            try { GameManager.flightControlsEnabled = true; }
            catch { }
        }

        private static void RestoreCameraClip()
        {
            if (_infCam != null && _haveSavedCam && _savedNear > 0.001f)
            {
                try
                {
                    _infCam.nearClipPlane = _savedNear;
                    _infCam.useOcclusionCulling = _savedOcc;
                }
                catch { }
            }
            _infCam = null;
            _haveSavedCam = false;
            _savedNear = -1f;
        }
    }
}
