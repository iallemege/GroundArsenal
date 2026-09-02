using System;
using System.Reflection;
using HarmonyLib;
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
        internal static bool Zh = false;
        internal static float Hp = MaxHp;
        internal const float MaxHp = 80f;
        internal const int OuterArmor = 10;
        internal const int InnerArmor = 1;
        internal static float ArmorFatigue;
        internal static float MoveSpeed;
        internal static bool Sprinting;
        internal static float Strafe;
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
        private static float _savedFov = 60f;
        private static int _savedMask = -1;
        private static float _baseFov = 60f;
        private static bool _haveSavedCam;
        private static CameraMode _savedMode;
        private static bool _savedLook;
        private static float _deadUntil;
        private static float _lastArmorHitAt;
        private static float _sprintUntil;
        private static float _sprintReadyAt;
        private static float _jumpReadyAt;
        private static bool _chuteArmed;
        private static float _chuteReadyAt;
        private static PilotDismounted _chuteDummy;
        private static int _chuteSeenId;
        internal const float SprintDur = 60f;
        internal const float SprintCd = 3f;
        internal const float JumpCd = 3f;
        internal const float JumpHeight = 8f;
        private static readonly FieldInfo StatusDisplayField =
            AccessTools.Field(typeof(Aircraft), "statusDisplay");

        internal static Aircraft ParkedAircraft
        {
            get { return _parked; }
        }

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

        internal static float LookYaw
        {
            get { return _yaw; }
        }

        internal static void PullLook(Vector3 worldFwd, float t)
        {
            if (_eye == null || t <= 0.0001f)
                return;
            if (worldFwd.sqrMagnitude < 0.0001f)
                return;
            worldFwd.Normalize();
            Vector3 next = Vector3.Slerp(_eye.forward, worldFwd, Mathf.Clamp01(t));
            if (next.sqrMagnitude < 0.0001f)
                return;
            next.Normalize();
            Vector3 flat = new Vector3(next.x, 0f, next.z);
            if (flat.sqrMagnitude > 0.0001f)
                _yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            _pitch = -Mathf.Asin(Mathf.Clamp(next.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (_pitch < -85f) _pitch = -85f;
            if (_pitch > 85f) _pitch = 85f;
            if (_pawn != null)
                _pawn.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _eye.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        internal static bool SprintActive
        {
            get { return Time.time < _sprintUntil; }
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
            if (!GaConfig.IsEnabled())
            {
                if (Active)
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
                TickChuteLanding();
                if (!GaHangar.HangarSelectOpen())
                {
                    if (GaHud.CatalogOpen)
                        GaHud.CloseMenus();
                    if (Input.GetKeyDown(KeyCode.U))
                        TryHopOut();
                    return;
                }
                GaHangar.TickKeys();
                if (Input.GetKeyDown(KeyCode.Quote))
                {
                    GaHud.CatalogOpen = !GaHud.CatalogOpen;
                    if (GaHud.CatalogOpen)
                        ArmorCatalogService.EnsureScanned();
                }
                if (Input.GetKeyDown(KeyCode.Escape) && GaHud.CatalogOpen)
                    GaHud.CloseMenus();
                if (Input.GetKeyDown(KeyCode.U))
                    TryHopOut();
                return;
            }
            HoldFlightControls();
            FreezeAircraft();
            HideAircraftHud();
            KickCockpitCam();
            if (_chuteDummy != null)
                NeutralizeDummy(_chuteDummy);
            TickArmorFatigue();
            if (Dead)
            {
                if (Time.unscaledTime >= _deadUntil)
                    FinishDeath();
                return;
            }
            GaHud.Tick();
            GaRadar.Tick(Time.deltaTime);
            if (!GaHud.BlocksLook)
            {
                LookMove();
                if (_eye != null)
                    GaCombat.Tick(_eye, Time.deltaTime);
            }
            GaLoot.Tick(_pawn != null ? _pawn.transform.position : EyePos);
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
            DenyMsg = en;
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
                _savedFov = cam.fieldOfView;
                _savedMask = cam.cullingMask;
                if (_baseFov < 5f)
                    _baseFov = cam.fieldOfView;
                _haveSavedCam = true;
            }
            if (cam.nearClipPlane > 0.035f)
                cam.nearClipPlane = 0.03f;
            cam.useOcclusionCulling = false;
            cam.cullingMask = cam.cullingMask | 1;
            float fov = _baseFov;
            if (fov < 10f)
                fov = 60f;
            fov *= GaCombat.FovMul();
            float floor = 5.5f;
            if (GaCombat.IsSniper())
                floor = 3.2f;
            else if (GaCombat.IsG11())
                floor = 7.5f;
            if (fov < floor)
                fov = floor;
            if (fov > 95f)
                fov = 95f;
            cam.fieldOfView = fov;
            try { CameraStateManager.cameraMode = CameraMode.free; }
            catch { }
            try { CameraStateManager.enableMouseLook = false; }
            catch { }
            GaView.BindToCamera(cam);
        }

        internal static void HoldFlightControls()
        {
            try { GameManager.flightControlsEnabled = false; }
            catch { }
        }

        internal static void ClaimParked(Aircraft ac)
        {
            if (ac == null)
                return;
            bool fresh = _parked != ac;
            _parked = ac;
            if (fresh)
                Deny("Aircraft marked. Walk up, U to board.",
                    "Aircraft marked. Walk up, U to board.");
        }

        internal static void OnPlayerSetAircraft(Player p, Aircraft ac)
        {
            if (!Active || p == null || ac == null)
                return;
            bool local = false;
            try { local = p.IsLocalPlayer; }
            catch { local = false; }
            if (!local)
                return;
            ClaimParked(ac);
            try { p.RemoveAircraft(ac); }
            catch { }
            HideAircraftHud();
            KickCockpitCam();
        }

        internal static void OnLocalAircraftUi(Aircraft ac)
        {
            if (!Active || ac == null)
                return;
            ClaimParked(ac);
            Player p = null;
            try { GameManager.GetLocalPlayer(out p); }
            catch { }
            if (p != null)
            {
                try { p.RemoveAircraft(ac); }
                catch { }
            }
            HideAircraftHud();
            KickCockpitCam();
        }

        internal static bool BlockCockpitSwitch(CameraStateManager mgr, CameraBaseState state)
        {
            if (!Active || mgr == null || state == null)
                return false;
            return state == mgr.cockpitState || state == mgr.chaseState;
        }

        internal static void HideAircraftHud()
        {
            try { FlightHud.EnableCanvas(false); }
            catch { }
            try
            {
                CombatHUD hud = SceneSingleton<CombatHUD>.i;
                if (hud != null && hud.aircraft != null)
                    hud.RemoveAircraft();
            }
            catch { }
            SetStatusVisible(_parked, false);
        }

        internal static void KickCockpitCam()
        {
            try
            {
                CameraStateManager mgr = SceneSingleton<CameraStateManager>.i;
                if (mgr == null || mgr.currentState == null)
                    return;
                CameraBaseState cur = mgr.currentState;
                if (cur == mgr.cockpitState || cur == mgr.chaseState || cur == mgr.orbitState)
                    mgr.SwitchState(mgr.freeState);
            }
            catch { }
            try { CameraStateManager.cameraMode = CameraMode.free; }
            catch { }
            try { CameraStateManager.enableMouseLook = false; }
            catch { }
        }

        internal static void ShowAircraftHud(Aircraft ac)
        {
            if (ac == null)
                return;
            SetStatusVisible(ac, true);
            try
            {
                FlightHud hud = SceneSingleton<FlightHud>.i;
                if (hud != null)
                    hud.SetAircraft(ac);
            }
            catch { }
            try
            {
                CombatHUD ch = SceneSingleton<CombatHUD>.i;
                if (ch != null && ch.aircraft != ac)
                    ch.SetAircraft(ac);
            }
            catch { }
            try { FlightHud.EnableCanvas(true); }
            catch { }
        }

        private static void SetStatusVisible(Aircraft ac, bool on)
        {
            if (ac == null || StatusDisplayField == null)
                return;
            try
            {
                StatusDisplay sd = StatusDisplayField.GetValue(ac) as StatusDisplay;
                if (sd != null && sd.gameObject != null)
                    sd.gameObject.SetActive(on);
            }
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
                Hp = 0f;
                Die();
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
            float sens = 2.4f * GaCombat.LookSensMul();
            _yaw += Input.GetAxis("Mouse X") * sens;
            _pitch -= Input.GetAxis("Mouse Y") * sens;
            if (_pitch < -85f) _pitch = -85f;
            if (_pitch > 85f) _pitch = 85f;
            if (_pawn != null)
                _pawn.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_eye != null)
                _eye.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            float walk = 4.6f;
            float sprint = 7.2f;
            float now = Time.time;
            if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                && now >= _sprintReadyAt)
            {
                _sprintUntil = now + SprintDur;
                _sprintReadyAt = _sprintUntil + SprintCd;
            }
            bool burst = now < _sprintUntil;
            float speed = burst ? sprint * 2f : walk;
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
            MoveSpeed = world.magnitude;
            Sprinting = burst && wish.sqrMagnitude > 0.01f;
            Strafe = 0f;
            if (Input.GetKey(KeyCode.A))
                Strafe -= 1f;
            if (Input.GetKey(KeyCode.D))
                Strafe += 1f;
            if (_cc == null)
                return;
            bool grounded = _cc.isGrounded;
            if (!grounded && _pawn != null)
            {
                RaycastHit gh;
                if (Physics.Raycast(_pawn.transform.position + Vector3.up * 0.2f, Vector3.down,
                    out gh, 0.45f, ~0, QueryTriggerInteraction.Ignore))
                    grounded = true;
            }
            if (grounded && _vertVel < 0f)
                _vertVel = -2f;
            if (grounded && Input.GetKeyDown(KeyCode.Space) && now >= _jumpReadyAt)
            {
                float g = Mathf.Abs(Physics.gravity.y);
                if (g < 1f)
                    g = 9.81f;
                _vertVel = Mathf.Sqrt(2f * g * JumpHeight);
                _jumpReadyAt = now + JumpCd;
            }
            _vertVel += Physics.gravity.y * Time.deltaTime;
            world.y = _vertVel;
            _cc.Move(world * Time.deltaTime);
        }

        internal static void TryHangarSpawn()
        {
            if (Active)
                return;
            if (!GaConfig.IsEnabled())
            {
                Deny("Infantry disabled.", "Infantry disabled.");
                return;
            }
            float cost = GaCombat.KitCostM();
            if (!GaFunds.CanPay(cost))
            {
                Deny("Not enough funds (" + cost.ToString("0.00") + "M / have "
                    + GaFunds.Read().ToString("0.0") + "M).", "Not enough funds.");
                return;
            }
            Vector3 pos;
            float yaw;
            if (!GaHangar.TrySpawn(out pos, out yaw))
            {
                Deny("No hangar spawn point.", "找不到机库出生点。");
                return;
            }
            if (!GaFunds.TryPay(cost))
            {
                Deny("Not enough funds (" + cost.ToString("0.00") + "M).", "Not enough funds.");
                return;
            }
            GaHangar.CloseUi();
            SpawnPawn(pos, yaw);
            GaCombat.ApplyHangarKit();
            GaCombat.AttachViews(_eye);
            Deny("Deployed  -" + cost.ToString("0.00") + "M  funds "
                + GaFunds.Read().ToString("0.0") + "M.", "Deployed.");
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
            HideAircraftHud();
            KickCockpitCam();
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
            ShowAircraftHud(ac);
            try
            {
                CameraStateManager mgr = SceneSingleton<CameraStateManager>.i;
                if (mgr != null)
                    mgr.SwitchState(mgr.cockpitState);
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
            _pawn.AddComponent<GaPawn>();
            GameObject hitbox = new GameObject("Hitbox");
            hitbox.transform.SetParent(_pawn.transform, false);
            CapsuleCollider cap = hitbox.AddComponent<CapsuleCollider>();
            cap.height = 1.8f;
            cap.radius = 0.36f;
            cap.center = new Vector3(0f, 0.9f, 0f);
            GameObject eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(_pawn.transform, false);
            eyeGo.transform.localPosition = new Vector3(0f, 1.64f, 0f);
            _eye = eyeGo.transform;
            Eye = _eye;
            Hp = MaxHp;
            ArmorFatigue = 0f;
            _lastArmorHitAt = 0f;
            _sprintUntil = 0f;
            _sprintReadyAt = 0f;
            _jumpReadyAt = 0f;
            MoveSpeed = 0f;
            Dead = false;
            _vertVel = 0f;
            Sprinting = false;
            Strafe = 0f;
            Active = true;
            Camera cam0 = Camera.main;
            if (cam0 != null && cam0.fieldOfView > 5f)
                _baseFov = cam0.fieldOfView;
            HoldFlightControls();
            try { _savedMode = CameraStateManager.cameraMode; }
            catch { _savedMode = CameraMode.cockpit; }
            try { _savedLook = CameraStateManager.enableMouseLook; }
            catch { _savedLook = true; }
            try { CameraStateManager.cameraMode = CameraMode.free; }
            catch { }
            try { CameraStateManager.enableMouseLook = false; }
            catch { }
            HideAircraftHud();
            KickCockpitCam();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void Exit(bool toCockpit)
        {
            GaCombat.ClearBolts();
            GaLoot.Clear();
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
            _chuteDummy = null;
            _chuteArmed = false;
            _chuteSeenId = 0;
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
                    if (_savedFov > 5f)
                        _infCam.fieldOfView = _savedFov;
                    if (_savedMask != -1)
                        _infCam.cullingMask = _savedMask;
                }
                catch { }
            }
            _infCam = null;
            _haveSavedCam = false;
            _savedNear = -1f;
            _savedMask = -1;
        }

        private static void Die()
        {
            if (Dead)
                return;
            Dead = true;
            Hp = 0f;
            _deadUntil = Time.unscaledTime + 5f;
            GaHud.CloseMenus();
            if (_cc != null)
                _cc.enabled = false;
            GaCombat.ClearBolts();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void FinishDeath()
        {
            Exit(false);
            OpenSpawnMap();
        }

        private static void OpenSpawnMap()
        {
            Player p = null;
            try { GameManager.GetLocalPlayer(out p); }
            catch { p = null; }
            try
            {
                CombatHUD hud = SceneSingleton<CombatHUD>.i;
                if (hud != null && hud.aircraft != null)
                    hud.RemoveAircraft();
            }
            catch { }
            if (p != null)
            {
                try
                {
                    Aircraft owned = p.Aircraft;
                    if (owned != null)
                        p.RemoveAircraft(owned);
                }
                catch { }
                try
                {
                    PilotDismounted pd = p.PilotDismounted;
                    if (pd != null)
                        p.RemovePilotDismounted(pd);
                }
                catch { }
            }
            try { DynamicMap.AllowedToOpen = true; }
            catch { }
            try
            {
                if (p != null)
                    p.ShowMap(0f);
                else
                {
                    DynamicMap map = SceneSingleton<DynamicMap>.i;
                    if (map != null)
                        map.Maximize();
                }
            }
            catch { }
            try
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            catch { }
        }

        internal static Unit LinkedUnit()
        {
            if (_parked != null)
                return _parked;
            return _chuteDummy;
        }

        internal static int OuterEffective()
        {
            float f = ArmorFatigue;
            if (f < 0f) f = 0f;
            if (f > 1f) f = 1f;
            int drop = Mathf.RoundToInt(f * 8f);
            int g = OuterArmor - drop;
            if (g < 0) g = 0;
            return g;
        }

        internal static float SprintFill()
        {
            float now = Time.time;
            float fill;
            if (now < _sprintUntil)
                fill = (_sprintUntil - now) / SprintDur;
            else if (now < _sprintReadyAt)
                fill = 1f - (_sprintReadyAt - now) / SprintCd;
            else
                fill = 1f;
            if (fill < 0f)
                fill = 0f;
            if (fill > 1f)
                fill = 1f;
            return fill;
        }

        internal static float JumpFill()
        {
            float now = Time.time;
            float fill = 1f;
            if (now < _jumpReadyAt)
                fill = 1f - (_jumpReadyAt - now) / JumpCd;
            if (fill < 0f)
                fill = 0f;
            if (fill > 1f)
                fill = 1f;
            return fill;
        }

        internal static float ServoHeat()
        {
            if (Time.time < _sprintUntil)
                return 1f - SprintFill();
            return 0f;
        }

        internal static void TickArmorFatigue()
        {
            if (ArmorFatigue <= 0.0001f)
                return;
            if (Time.time - _lastArmorHitAt < 5.5f)
                return;
            ArmorFatigue -= Time.deltaTime * 0.018f;
            if (ArmorFatigue < 0f)
                ArmorFatigue = 0f;
        }

        internal static void ReceiveHit(float pierce, float blast, float fire, float impact, PersistentID dealer, Vector3 incoming)
        {
            if (!Active || Dead)
                return;
            int pen = GaVehicleArmor.PenGradeIncoming(dealer, pierce);
            WeaponInfo w = GaVehicleArmor.WeaponFromDealer(dealer);
            int wep = GaArmorMath.ClassifyKinetic(w);
            if (wep == GaArmorMath.WepUnknown)
            {
                if (blast > pierce * 0.8f && blast > 8f)
                    wep = GaArmorMath.WepRocket;
                else if (pen >= 13)
                    wep = GaArmorMath.WepMissile;
                else
                    wep = GaArmorMath.WepCannon;
            }
            bool pureKe = GaArmorMath.IsPureKeWeapon(wep, w);
            if (impact < 1f)
                impact = GaArmorMath.SynthesizeKe(wep, pierce, impact, 0, 0f, 0f);
            _lastArmorHitAt = Time.time;
            pen = GaArmorMath.ApplyPenFloat(pen);
            int outer = OuterEffective();
            if (outer >= 1)
            {
                int hit = GaArmorMath.ResolveHit(pen, outer, wep);
                float fat = 0.05f;
                if (hit == GaArmorMath.HitGlance) fat = 0.09f;
                else if (hit == GaArmorMath.HitPen) fat = 0.22f;
                else if (hit == GaArmorMath.HitOvermatch) fat = 0.42f;
                int gap = pen - outer;
                if (gap < 0) gap = -gap;
                if (gap <= 1) fat *= 1.7f;
                ArmorFatigue += fat;
                if (ArmorFatigue > 1f) ArmorFatigue = 1f;
                if (hit == GaArmorMath.HitBounce || hit == GaArmorMath.HitGlance)
                {
                    string kind = hit == GaArmorMath.HitGlance ? "GLANCE" : "BOUNCE";
                    GaHud.FlashHit(kind + "  plate " + outer + "  0 dmg  fat "
                        + Mathf.RoundToInt(ArmorFatigue * 100f) + "%");
                    return;
                }
                float pass = hit == GaArmorMath.HitOvermatch ? 0.82f : 0.45f;
                ApplyInner(pen, wep, pierce * pass, blast * pass, impact * pass, fire * pass, pureKe, hit, true);
                return;
            }
            ApplyInner(pen, wep, pierce, blast, impact, fire, pureKe, GaArmorMath.HitPen, false);
        }

        private static void ApplyInner(int pen, int wep, float pierce, float blast, float impact, float fire, bool pureKe, int outerHit, bool throughPlate)
        {
            int hit = GaArmorMath.ResolveHit(pen, InnerArmor, wep);
            if (hit == GaArmorMath.HitBounce || hit == GaArmorMath.HitGlance)
            {
                GaHud.FlashHit("BODY GLANCE  0");
                return;
            }
            float pm = GaArmorMath.PierceMul(hit);
            float bm = GaArmorMath.BlastMul(hit, false, pureKe);
            float im = GaArmorMath.ImpactMul(hit);
            float dmg = pierce * pm + blast * bm + impact * im + fire * 0.4f;
            if (dmg < 2f)
                dmg = 2f;
            Hp -= dmg;
            string layer = throughPlate ? "PEN plate→body" : "BODY HIT";
            if (outerHit == GaArmorMath.HitOvermatch && throughPlate)
                layer = "OVERPEN plate→body";
            GaHud.FlashHit(layer + "  KE " + Mathf.RoundToInt(impact * im)
                + "  -" + Mathf.RoundToInt(dmg) + " HP");
            if (Hp <= 0f)
            {
                Hp = 0f;
                Die();
            }
        }

        private static int PenFromPierce(float pierce)
        {
            if (pierce >= 180f) return 18;
            if (pierce >= 110f) return 15;
            if (pierce >= 40f) return 12;
            if (pierce >= 20f) return 8;
            if (pierce >= 12f) return 6;
            if (pierce >= 8f) return 5;
            if (pierce >= 4f) return 4;
            if (pierce >= 2f) return 2;
            return 1;
        }

        internal static void TryAbsorbWorldHit(Vector3 position, Vector3 velocity, float pierce, float blast, PersistentID dealer)
        {
            if (!Active || _pawn == null)
                return;
            float d = Vector3.Distance(_pawn.transform.position + Vector3.up * 0.9f, position);
            if (d < 0.55f || d > 1.7f)
                return;
            Vector3 incoming = velocity.sqrMagnitude > 0.01f ? velocity : Vector3.forward;
            float ke = velocity.magnitude * 0.12f;
            if (ke < pierce * 0.9f)
                ke = pierce * 0.9f;
            ReceiveHit(pierce, blast, 0f, ke, dealer, incoming);
        }

        private static void TickChuteLanding()
        {
            PilotDismounted pd = null;
            try
            {
                if (!GameManager.GetLocalPilotDismounted(out pd) || pd == null)
                {
                    _chuteArmed = false;
                    _chuteSeenId = 0;
                    return;
                }
            }
            catch
            {
                _chuteArmed = false;
                return;
            }
            int id = pd.GetInstanceID();
            if (id != _chuteSeenId)
            {
                _chuteSeenId = id;
                _chuteArmed = false;
                _chuteReadyAt = 0f;
            }
            PilotDismounted.PilotState st = pd.animationState;
            if (st == PilotDismounted.PilotState.dead)
            {
                _chuteArmed = false;
                return;
            }
            bool down = st == PilotDismounted.PilotState.landing || DummyOnGround(pd);
            if (!down)
                return;
            if (!_chuteArmed)
            {
                _chuteArmed = true;
                _chuteReadyAt = Time.unscaledTime + 0.35f;
                return;
            }
            if (Time.unscaledTime < _chuteReadyAt)
                return;
            if (Active)
                return;
            EnterFromChute(pd);
        }

        private static bool DummyOnGround(PilotDismounted pd)
        {
            if (pd == null)
                return false;
            try
            {
                Vector3 pos = pd.transform.position;
                RaycastHit hit;
                if (!Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out hit, 6f,
                    ~0, QueryTriggerInteraction.Ignore))
                    return false;
                float agl = pos.y - hit.point.y;
                if (agl > 2.4f)
                    return false;
                Vector3 v = Vector3.zero;
                if (pd.rb != null)
                    v = pd.rb.velocity;
                if (v.y < -6f)
                    return false;
                return pd.animationState == PilotDismounted.PilotState.parachuting
                    || pd.animationState == PilotDismounted.PilotState.landing;
            }
            catch
            {
                return false;
            }
        }

        private static void EnterFromChute(PilotDismounted pd)
        {
            if (pd == null || Active)
                return;
            Vector3 pos = pd.transform.position;
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 4f, Vector3.down, out hit, 40f,
                ~0, QueryTriggerInteraction.Ignore))
                pos = hit.point + Vector3.up * 0.08f;
            float yaw = pd.transform.rotation.eulerAngles.y;
            NeutralizeDummy(pd);
            _chuteDummy = pd;
            _parked = null;
            AirHop = false;
            SpawnPawn(pos, yaw);
            GaCombat.ApplyCarryOnly();
            GaCombat.AttachViews(_eye);
            HoldFlightControls();
            try { CameraStateManager.cameraMode = CameraMode.free; }
            catch { }
            try { CameraStateManager.enableMouseLook = false; }
            catch { }
            HideAircraftHud();
            KickCockpitCam();
            _chuteArmed = false;
        }

        internal static void NeutralizeDummy(PilotDismounted pd)
        {
            if (pd == null)
                return;
            try { pd.SetCollidable(false); }
            catch { }
            try
            {
                CharacterController cc = pd.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = false;
            }
            catch { }
            try
            {
                if (pd.rb != null)
                {
                    pd.rb.detectCollisions = false;
                    pd.rb.isKinematic = true;
                    pd.rb.velocity = Vector3.zero;
                    pd.rb.angularVelocity = Vector3.zero;
                }
            }
            catch { }
            try
            {
                Collider[] cols = pd.GetComponentsInChildren<Collider>(true);
                if (cols != null)
                {
                    for (int i = 0; i < cols.Length; i++)
                    {
                        if (cols[i] != null)
                            cols[i].enabled = false;
                    }
                }
            }
            catch { }
            try
            {
                Renderer[] rs = pd.GetComponentsInChildren<Renderer>(true);
                if (rs != null)
                {
                    for (int i = 0; i < rs.Length; i++)
                    {
                        if (rs[i] != null)
                            rs[i].enabled = false;
                    }
                }
            }
            catch { }
        }
    }
}
