using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GroundArsenal
{
    internal static class GaMode
    {
        internal static bool Active;
        private static float _nextHudKickAt;
        private static float _nextHoldFlightAt;
        internal static bool AirHop;
        internal static bool Dead;
        internal static bool Downed;
        internal static bool CrashKia;
        internal static string DeathCause = "";
        internal const float BleedSec = 14f;
        internal static bool Zh = false;
        internal static float Hp = 80f;
        internal static float MaxHp
        {
            get { return GaClass.MaxHp(GaClass.Picked) * 1.55f + 48f; }
        }
        internal static int OuterArmor
        {
            get
            {
                if (GaClass.Picked == GaKitId.Tsf)
                    return GaArmorMath.GradeMax;
                return GaClass.Armor(GaClass.Picked) + 16;
            }
        }
        internal const int InnerArmor = 4;
        internal static float ArmorFatigue;
        internal static float MoveSpeed;
        internal static bool Sprinting;
        internal static float Strafe;
        internal static Transform Eye;
        internal static string DenyMsg;
        internal static float DenyUntil;
        internal static bool BranchQueued;
        private static bool _forceHop;

        private static GameObject _pawn;
        private static CharacterController _cc;
        private static Transform _eye;
        private static Aircraft _parked;
        private static float _yaw;
        private static float _pitch;
        private static float _moveYaw;
        private static Transform _deck;
        private static Vector3 _deckLocal;
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
        private static PersistentID _ghostDealer;
        private static bool _wasGrounded = true;
        private static float _airY;
        private static int _chuteSeenId;
        private static int _dummyMuteId;
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

        internal static bool IsMutedDummy(PilotDismounted pd)
        {
            return Active && pd != null && _chuteDummy != null
                && object.ReferenceEquals(pd, _chuteDummy);
        }

        internal static Transform EyeRoot
        {
            get { return _eye; }
        }

        internal static void SetWalker(bool on)
        {
            if (_cc != null)
                _cc.enabled = on;
            if (_eye == null)
                return;
            if (on)
                _eye.localPosition = new Vector3(0f, 1.72f, 0f);
            else
                _eye.localPosition = new Vector3(0f, 0.48f, 0.1f);
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

        internal static Quaternion FormRot
        {
            get { return Quaternion.Euler(0f, _moveYaw, 0f); }
        }

        internal static float LookPitch
        {
            get { return _pitch; }
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

        [SkipLocalsInit]
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
            if (GaShop.PendingFly != null)
            {
                Aircraft fly = GaShop.PendingFly;
                GaShop.PendingFly = null;
                if (Active)
                    Exit(true);
                ShowAircraftHud(fly);
                try { CameraStateManager.cameraMode = CameraMode.cockpit; }
                catch { }
                try { GameManager.flightControlsEnabled = true; }
                catch { }
            }
            if (BranchQueued)
            {
                BranchQueued = false;
                TryBranchSpawn();
            }
            if (!Active)
            {
                if (CrashKia)
                {
                    if (Time.unscaledTime >= _deadUntil)
                    {
                        CrashKia = false;
                        FinishDeath();
                    }
                    return;
                }
                GaAirLift.Tick();
                GaDropSquad.Tick();
                GaSupport.Tick();
                GaRadio.Tick();
                GaDeploy.Tick();
                if (Input.GetKeyDown(KeyCode.T))
                    TryHopOut();
                if (!GaHangar.HangarSelectOpen() && !GaHangar.DeathPick)
                {
                    if (GaHud.CatalogOpen)
                        GaHud.CloseMenus();
                    return;
                }
                GaHangar.TickKeys();
                if (GaHangar.MenuOpen && Input.GetKeyDown(KeyCode.Quote))
                {
                    GaHud.CatalogOpen = !GaHud.CatalogOpen;
                    if (GaHud.CatalogOpen)
                        ArmorCatalogService.EnsureScanned();
                }
                if (Input.GetKeyDown(KeyCode.Escape) && GaHud.CatalogOpen)
                    GaHud.CloseMenus();
                if (Input.GetKeyDown(KeyCode.T))
                    TryHopOut();
                return;
            }
            float nowKick = Time.unscaledTime;
            if (nowKick >= _nextHoldFlightAt)
            {
                _nextHoldFlightAt = nowKick + 0.25f;
                HoldFlightControls();
            }
            FreezeAircraft();
            if (nowKick >= _nextHudKickAt)
            {
                _nextHudKickAt = nowKick + 0.5f;
                HideAircraftHud();
                KickCockpitCam();
            }
            if (_chuteDummy != null)
                NeutralizeDummy(_chuteDummy);
            TickArmorFatigue();
            GaDropSquad.Tick();
            GaSupply.Tick();
            GaSupport.Tick();
            GaAtLaser.Tick();
            GaRadio.Tick();
            GaHotensia.Tick();
            if (Downed && !Dead)
            {
                if (Input.GetKeyDown(KeyCode.Backspace) && !GaHud.BlocksLook && !GaRadio.Open)
                    Die("SUICIDE", true);
                if (!GaHud.BlocksLook)
                {
                    GaVr.Sample();
                    LookOnly();
                }
                GaFps.TickDowned();
                if (Time.unscaledTime >= _deadUntil)
                {
                    Dead = true;
                    if (string.IsNullOrEmpty(DeathCause) || DeathCause == "KIA")
                        DeathCause = "BLED OUT";
                    _deadUntil = Time.unscaledTime + 2.2f;
                    if (_cc != null)
                        _cc.enabled = false;
                }
                return;
            }
            if (Dead)
            {
                if (Time.unscaledTime >= _deadUntil)
                    FinishDeath();
                return;
            }
            GaHud.Tick();
            GaFps.Tick();
            if (!GaHud.BlocksLook)
            {
                GaVr.Sample();
                GaPalmMenu.Tick();
                LookMove();
                if (_eye != null && !GaHotensia.HidesHud)
                    GaCombat.Tick(_eye, Time.deltaTime);
            }
            GaLoot.Tick(_pawn != null ? _pawn.transform.position : EyePos);
            GaRifleDrop.Tick(_eye);
            GaAtLaser.TickCancel();
            GaClass.Tick();
            GaRide.Tick();
            GaShop.Tick();
            GaGarage.Tick();
            GaHangar.TickKeys();
            if (Input.GetKeyDown(KeyCode.Backspace) && !GaHud.BlocksLook && !GaRadio.Open)
                Die("SUICIDE", true);
            if (!GaHud.BlocksLook && Input.GetKeyDown(KeyCode.T))
            {
                if (!GaRide.TryToggle())
                    TryBoard();
            }
        }

        internal static void Draw()
        {
            GaFxUi.Tick();
            if (!Active)
            {
                if (CrashKia)
                {
                    GaHud.DrawKiaScreen();
                    return;
                }
                GaHangar.Draw();
                DrawDropHint();
                GaRadio.Draw();
                return;
            }
            if (GaHotensia.HidesHud && !GaHangar.FieldOpen && !GaTsf.Owns())
            {
                GaHotensia.Draw();
                return;
            }
            if (GaPerfFrameGate.Paint() || GaHud.ArsenalOpen || GaHud.CatalogOpen
                || GaFxUi.Busy || GaHangar.FieldOpen)
                GaHud.Draw();
            GaHangar.Draw();
            if (!GaTsf.Owns())
                GaHotensia.Draw();
            GaShop.Draw();
            GaGarage.Draw();
            if (GaPerfFrameGate.Paint() || GaFxUi.Radio > 0.02f)
            {
                GaRadio.Draw();
                GaFps.Draw();
            }
        }

        private static void DrawDropHint()
        {
            string hint = GaAirLift.Hint();
            if (string.IsNullOrEmpty(hint))
            {
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
                hint = GaChute.CockpitHint(ac);
            }
            if (string.IsNullOrEmpty(hint))
                return;
            float w = 560f;
            float x = Screen.width * 0.5f - w * 0.5f;
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.04f, 0.05f, 0.62f);
            GUI.DrawTexture(new Rect(x, 42f, w, 24f), GaFxUi.Px);
            GUI.color = new Color(0.92f, 0.95f, 0.88f, 0.95f);
            GUI.Label(new Rect(x, 42f, w, 24f), hint, GUI.skin.label);
            GUI.color = prev;
        }

        internal static void Shutdown()
        {
            if (Active)
                Exit(false);
            Dead = false;
            Downed = false;
            GaHud.CloseMenus();
            GaDropSquad.ClearAll();
            GaNet.Shutdown();
            GaPhys.ClearWalkers();
        }

        internal static void Deny(string en, string zh)
        {
            DenyMsg = en;
            DenyUntil = Time.unscaledTime + 3f;
        }

        private static bool _camConfigured;
        private static float _lastAppliedFov = -1f;

        internal static void ApplyCamera(CameraStateManager mgr)
        {
            if (GaAirLift.Flying)
            {
                GaAirLift.ApplyCam(mgr);
                return;
            }
            if (!Active || _eye == null || mgr == null)
                return;
            Camera cam = mgr.mainCamera;
            if (cam == null)
                cam = GaPerfFrameGate.Cam();
            if (cam == null)
                return;

            if (GaRide.Busy)
                GaRide.ApplyCam(mgr);
            else if (GaVr.Active)
                GaVr.Follow(cam, _eye);
            else if (GaTsf.Owns())
                GaTsf.ApplyCam(cam);
            else
            {
                cam.transform.position = _eye.position;
                cam.transform.rotation = _eye.rotation;
            }

            // One-shot / dirty: clip, occlusion, culling, mode — NOT every LateUpdate.
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
                _camConfigured = false;
            }
            if (!_camConfigured)
            {
                if (cam.nearClipPlane > 0.035f)
                    cam.nearClipPlane = 0.03f;
                cam.useOcclusionCulling = false;
                cam.cullingMask = cam.cullingMask | 1;
                try { CameraStateManager.cameraMode = CameraMode.free; } catch { }
                try { CameraStateManager.enableMouseLook = false; } catch { }
                _camConfigured = true;
            }

            if (GaVr.Active)
                return;
            float fov = _baseFov;
            if (fov < 10f) fov = 60f;
            if (GaTsf.Owns())
                fov = GaTsf.Fov();
            fov *= GaCombat.FovMul();
            float floor = 5.5f;
            if (GaTsf.Owns()) floor = 48f;
            else if (GaCombat.IsSniper()) floor = 3.2f;
            else if (GaCombat.IsG11()) floor = 7.5f;
            if (fov < floor) fov = floor;
            if (GaTsf.Owns())
            {
                if (fov > 82f) fov = 82f;
            }
            else if (fov > 95f) fov = 95f;
            // Free-cam UpdateState lerps FOV back toward desiredFOV every frame.
            // Write ours after that, every frame, or a zoom eases itself back out.
            cam.fieldOfView = fov;
            _lastAppliedFov = fov;
            try
            {
                if (mgr != null && mgr.cockpitCamRender != null)
                    mgr.cockpitCamRender.fieldOfView = fov;
            }
            catch { }
            if (!GaTsf.Owns())
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
                Deny("Aircraft marked. Walk up, T to board.",
                    "已标记飞机。走近按 T 登机。");
        }

        internal static void OnPlayerSetAircraft(Player p, Aircraft ac)
        {
            if (p == null || ac == null)
                return;
            bool local = false;
            try { local = p.IsLocalPlayer; }
            catch { local = false; }
            if (!local)
                return;
            if (GaShop.ArmFly)
            {
                GaShop.ArmFly = false;
                GaShop.PendingFly = ac;
                return;
            }
            if (GaAirLift.Waiting)
            {
                GaAirLift.Attach(ac);
                HideAircraftHud();
                KickCockpitCam();
                try { CameraStateManager.cameraMode = CameraMode.free; }
                catch { }
                try { CameraStateManager.enableMouseLook = false; }
                catch { }
                return;
            }
            if (!Active)
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
            if (GaAirLift.Flying || GaAirLift.Waiting)
            {
                HideAircraftHud();
                KickCockpitCam();
                return;
            }
            if (GaShop.ArmFly || GaShop.PendingFly != null)
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
            if (!Active || Dead || Downed)
                return;
            amount *= 0.42f;
            if (amount > 0f && amount < 1f)
                amount = 1f;
            Hp -= amount;
            if (Hp < 0f)
                Hp = 0f;
            if (Hp <= 0f)
            {
                Hp = 0f;
                Die("WOUNDS", false);
            }
        }

        internal static void Revive(float hp)
        {
            Downed = false;
            Dead = false;
            Hp = hp;
            if (Hp > MaxHp)
                Hp = MaxHp;
            if (Hp < 12f)
                Hp = 12f;
            if (_cc != null)
                _cc.enabled = true;
            GaFps.RescueAmt = 0f;
        }

        private static void FreezeAircraft()
        {
            if (_parked == null)
                return;
            SilenceAircraft(_parked, !AirHop);
        }

        private static void LookOnly()
        {
            Camera vrCam = GaPerfFrameGate.Cam();
            if (GaVr.ApplyLook(_pawn != null ? _pawn.transform : null, _eye, vrCam))
            {
                if (_pawn != null)
                    _yaw = _pawn.transform.eulerAngles.y;
                return;
            }
            float sens = 2.4f * GaCombat.LookSensMul();
            _yaw += Input.GetAxis("Mouse X") * sens;
            _pitch -= Input.GetAxis("Mouse Y") * sens;
            if (_pitch < -85f) _pitch = -85f;
            if (_pitch > 85f) _pitch = 85f;
            if (_pawn != null)
                _pawn.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_eye != null)
                _eye.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private static void LookMove()
        {
            Camera vrCam = GaPerfFrameGate.Cam();
            if (GaVr.ApplyLook(_pawn != null ? _pawn.transform : null, _eye, vrCam))
            {
                if (_pawn != null)
                    _yaw = _pawn.transform.eulerAngles.y;
            }
            else
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
            }
            if (GaRide.Busy)
            {
                MoveSpeed = 0f;
                Sprinting = false;
                return;
            }

            float walk = GaClass.Walk(GaClass.Picked);
            float sprint = GaClass.Sprint(GaClass.Picked);
            float now = Time.time;
            bool holding = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                || (GaVr.Active && GaVr.SprintHeld);
            if (GaJet.On || GaTsf.Jumping)
                holding = false;
            if ((holding || GaVr.SprintDown) && now >= _sprintReadyAt)
            {
                if (now >= _sprintUntil)
                    _sprintUntil = now + SprintDur;
                _sprintReadyAt = _sprintUntil + SprintCd;
            }
            bool burst = holding && now < _sprintUntil;
            float speed = burst ? sprint : walk;
            GaFps.ApplyMove(ref speed, ref burst, _cc, _eye);
            Vector3 wish = GaVr.Active ? GaVr.MoveWish : Vector3.zero;
            if (!GaVr.Active)
            {
                if (Input.GetKey(KeyCode.W)) wish += Vector3.forward;
                if (Input.GetKey(KeyCode.S)) wish += Vector3.back;
                if (Input.GetKey(KeyCode.A)) wish += Vector3.left;
                if (Input.GetKey(KeyCode.D)) wish += Vector3.right;
            }
            if (wish.sqrMagnitude > 1f)
                wish.Normalize();
            Vector3 world = _pawn != null
                ? _pawn.transform.TransformDirection(wish) * speed
                : wish * speed;
            MoveSpeed = world.magnitude;
            Sprinting = burst && wish.sqrMagnitude > 0.01f;
            if (wish.sqrMagnitude > 0.01f)
            {
                Vector3 flat = new Vector3(world.x, 0f, world.z);
                if (flat.sqrMagnitude > 0.0001f)
                    _moveYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            }
            Strafe = 0f;
            if (Input.GetKey(KeyCode.A))
                Strafe -= 1f;
            if (Input.GetKey(KeyCode.D))
                Strafe += 1f;
            if (GaRide.Busy)
                return;
            if (_cc == null)
                return;
            if (_pawn != null)
                GaDeck.Carry(_pawn.transform, _cc, ref _deck, ref _deckLocal);
            if (GaChute.Falling)
            {
                Vector3 air = Vector3.zero;
                if (_pawn != null && wish.sqrMagnitude > 0.01f)
                    air = _pawn.transform.TransformDirection(wish) * (GaChute.Open ? 9f : 1.6f);
                GaChute.Tick(_pawn != null ? _pawn.transform : null, _cc, Time.deltaTime, ref air, ref _vertVel);
                MoveSpeed = new Vector3(air.x, 0f, air.z).magnitude;
                _cc.Move(air * Time.deltaTime);
                bool chuteDown = _cc.isGrounded;
                if (!chuteDown && _pawn != null)
                {
                    RaycastHit gh;
                if (GaPhys.RayWalk(_pawn.transform.position, GaTsf.Owns() ? 3.4f : 0.5f, out gh)
                    && gh.distance < (GaTsf.Owns() ? 3.2f : 0.55f))
                    chuteDown = true;
                }
                if (GaChute.Open && _pawn != null)
                    _airY = _pawn.transform.position.y;
                ApplyFall(chuteDown);
                if (_pawn != null)
                    GaDeck.Sample(_pawn.transform, ref _deck, ref _deckLocal);
                return;
            }
            bool grounded = _cc.isGrounded;
            if (!grounded && _pawn != null)
            {
                RaycastHit gh;
                float probe = GaTsf.Owns() ? 3.4f : 0.5f;
                if (GaPhys.RayWalk(_pawn.transform.position, probe, out gh)
                    && gh.distance < (GaTsf.Owns() ? 3.2f : 0.55f))
                    grounded = true;
            }
            if (grounded && _vertVel < 0f)
                _vertVel = -2f;
            bool jetTap = GaTsf.TryToggle() || GaJet.TryToggle();
            if (!jetTap && grounded && (Input.GetKeyDown(KeyCode.Space) || GaVr.JumpDown) && now >= _jumpReadyAt)
            {
                if (!GaFps.BlocksJump())
                {
                    float g = Mathf.Abs(Physics.gravity.y);
                    if (g < 1f)
                        g = 9.81f;
                    _vertVel = Mathf.Sqrt(2f * g * JumpHeight * GaClass.JumpMul(GaClass.Picked));
                    _jumpReadyAt = now + JumpCd;
                }
            }
            float dt = Time.deltaTime;
            Vector3 jetPos = _pawn != null ? _pawn.transform.position : Vector3.zero;
            bool tsfFly = GaTsf.Fly(ref _vertVel, ref world, jetPos, grounded, dt);
            bool tsfGlide = !tsfFly && GaTsf.Glide(ref _vertVel, ref world, grounded, dt);
            if (!tsfFly && !tsfGlide && !GaJet.Fly(ref _vertVel, ref world, jetPos, grounded, dt))
                _vertVel += Physics.gravity.y * dt;
            else if (_pawn != null)
                _airY = _pawn.transform.position.y;
            world.y = _vertVel;
            Vector3 delta = world * Time.deltaTime;
            if (!GaTsf.Jumping && !tsfGlide)
            {
                float hy = delta.y;
                delta.y = 0f;
                const float maxH = 0.26f;
                if (delta.sqrMagnitude > maxH * maxH)
                    delta = delta.normalized * maxH;
                delta.y = hy;
                float capY = GaJet.On ? 0.72f : 0.42f;
                if (delta.y > capY)
                    delta.y = capY;
                else if (delta.y < -capY)
                    delta.y = -capY;
            }
            _cc.Move(delta);
            bool nowDown = _cc.isGrounded;
            if (!nowDown && _pawn != null)
            {
                RaycastHit gh;
                if (GaPhys.RayWalk(_pawn.transform.position, GaTsf.Owns() ? 3.4f : 0.5f, out gh)
                    && gh.distance < (GaTsf.Owns() ? 3.2f : 0.55f))
                    nowDown = true;
            }
            if (_pawn != null)
                GaDeck.Sample(_pawn.transform, ref _deck, ref _deckLocal);
            ApplyFall(nowDown);
        }

        private static void ApplyFall(bool nowDown)
        {
            if (_pawn == null || Dead)
            {
                _wasGrounded = nowDown;
                return;
            }
            float y = _pawn.transform.position.y;
            if (!nowDown)
            {
                if (_wasGrounded)
                    _airY = y;
                else if (y > _airY)
                    _airY = y;
                _wasGrounded = false;
                return;
            }
            if (!_wasGrounded)
            {
                float drop = _airY - y;
                bool saved = GaChute.SoftLand || GaChute.Falling || GaTsf.Jumping
                    || (GaTsf.Owns() && drop < 90f);
                GaChute.SoftLand = false;
                if (!saved && drop > GaPhys.FallKillM)
                {
                    Deny("Fell to death.", "摔死了。");
                    Die("FALL", true);
                }
            }
            _wasGrounded = true;
        }

        internal static void QueueBranchSpawn()
        {
            BranchQueued = true;
        }

        internal static void TryBranchSpawn()
        {
            if (Active)
                return;
            if (!GaConfig.IsEnabled())
            {
                Deny("Infantry disabled.", "Infantry disabled.");
                return;
            }
            float cost = GaCombat.ClassCostM();
            float hull = 0f;
            if (GaForce.Kind == GaForceKind.Air && GaAirLift.CanStart())
                hull = GaAirLift.HalfCost(GaAirLift.Picked);
            cost += hull;
            if (!GaFunds.CanPay(cost))
            {
                Deny("Not enough funds (" + cost.ToString("0.00") + "M).", "Not enough funds.");
                return;
            }
            if (GaForce.Kind == GaForceKind.Air && GaAirLift.CanStart())
            {
                if (!GaFunds.TryPay(cost))
                {
                    Deny("Not enough funds.", "Not enough funds.");
                    return;
                }
                GaDeploy.Open = false;
                GaHangar.CloseUi();
                if (!GaAirLift.Start())
                {
                    GaFunds.Give(cost);
                    Deny("Lift spawn refused.", "Lift spawn refused.");
                    return;
                }
                Deny(GaAirLift.PickName() + "  half hull. T jump when you want.",
                    GaAirLift.PickName() + "  半价机体  到高度后 T 跳伞。");
                return;
            }
            if (GaForce.Kind == GaForceKind.Air)
            {
                Vector3 airPos;
                float airYaw;
                if (!GaHangar.TryForceSpawn(GaForceKind.Land, out airPos, out airYaw))
                {
                    Deny("No air spawn.", "没有空中出生点。");
                    return;
                }
                if (!GaFunds.TryPay(cost))
                {
                    Deny("Not enough funds.", "Not enough funds.");
                    return;
                }
                GaDeploy.Open = false;
                GaHangar.CloseUi();
                FinishAirDrop(airPos, airYaw);
                return;
            }
            Vector3 pos;
            float yaw;
            if (!GaHangar.TryForceSpawn(GaForce.Kind, out pos, out yaw))
            {
                Deny("No spawn for " + GaForce.Label() + ".", "没有该军种出生点。");
                return;
            }
            if (!GaFunds.TryPay(cost))
            {
                Deny("Not enough funds.", "Not enough funds.");
                return;
            }
            GaDeploy.Open = false;
            GaHangar.CloseUi();
            SpawnPawn(pos, yaw);
            GaCombat.ApplyClassKit();
            GaCombat.AttachViews(_eye);
            GaDropSquad.PlayerDeployed(pos, yaw);
            Deny(GaForce.Label() + "  " + GaClass.Name() + "  deployed.",
                GaForce.Label() + "  " + GaClass.Name() + "  已部署。");
        }

        private static void FinishAirDrop(Vector3 pos, float yaw)
        {
            pos.y += 850f;
            SpawnPawn(pos, yaw);
            GaCombat.ApplyClassKit();
            GaCombat.AttachViews(_eye);
            AirHop = true;
            GaChute.Begin(Vector3.zero, true);
            _vertVel = 0f;
            Deny("AIR DROP  SPACE open chute", "空投  空格开伞");
        }

        internal static void ForceHop()
        {
            _forceHop = true;
            try { TryHopOut(); }
            finally { _forceHop = false; }
        }

        private static void TryHopOut()
        {
            if (Active)
                return;
            Aircraft ac = null;
            if (GaAirLift.Flying)
                ac = GaAirLift.Ac;
            if (ac == null)
            {
                try
                {
                    if (!GameManager.GetLocalAircraft(out ac) || ac == null)
                    {
                        Deny("No local aircraft.", "没有本机。");
                        return;
                    }
                }
                catch
                {
                    Deny("No local aircraft.", "没有本机。");
                    return;
                }
            }
            Vector3 pos = ac.transform.position;
            float yaw = ac.transform.eulerAngles.y;
            bool lift = GaAirLift.Flying;
            bool cargo = lift || GaChute.IsDropship(ac);
            float agl = GaPhys.Agl(pos, 2500f);
            bool high = agl >= 40f;
            bool deck = GaHangar.OnCarrier(ac) && agl < 28f;
            if (_forceHop && lift && high)
                AirHop = true;
            else if (cargo && high)
                AirHop = true;
            else if (deck)
                AirHop = false;
            else if (lift && !high)
                AirHop = false;
            else if (GaForce.Kind == GaForceKind.Air && agl < 28f)
                AirHop = false;
            else
            {
                Deny("T hops cargo, carrier deck, or AIR at an airfield.",
                    "T：货舱跳伞、航母甲板，或空军在机场下飞机。");
                return;
            }
            if (AirHop)
                pos = GaChute.ExitPoint(ac, 0);
            else
            {
                RaycastHit hit;
                if (GaPhys.RayDown(pos, 40f, out hit))
                    pos = hit.point + Vector3.up * 0.08f;
                else
                    pos = pos + Vector3.up * 0.2f;
            }
            Vector3 inherit = Vector3.zero;
            try
            {
                if (ac.rb != null)
                    inherit = ac.rb.velocity;
            }
            catch { }
            _parked = ac;
            Player p = null;
            try { GameManager.GetLocalPlayer(out p); }
            catch { }
            if (p != null)
            {
                try { p.RemoveAircraft(ac); }
                catch { }
            }
            if (lift)
            {
                GaAirLift.SendHome();
                GaAirLift.Clear();
            }
            SpawnPawn(pos, yaw);
            GaCombat.ApplyClassKit();
            GaCombat.AttachViews(_eye);
            HoldFlightControls();
            if (!(lift && AirHop))
                SilenceAircraft(ac, !AirHop);
            HideAircraftHud();
            KickCockpitCam();
            if (AirHop)
            {
                GaChute.Begin(inherit, cargo);
                _vertVel = inherit.y;
                if (_vertVel > 10f)
                    _vertVel = 10f;
                if (cargo)
                    GaDropSquad.PlayerJumped(ac, inherit);
                Deny("SPACE  —  OPEN CHUTE", "空格开伞");
            }
            else
            {
                GaChute.Clear();
                Deny("On the deck.", "航母甲板。");
            }
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
            _moveYaw = yaw;
            _deck = null;
            _deckLocal = Vector3.zero;
            _pawn.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _cc = _pawn.AddComponent<CharacterController>();
            GaPhys.TuneWalker(_cc);
            _pawn.AddComponent<GaPawn>();
            GameObject hitbox = new GameObject("Hitbox");
            hitbox.transform.SetParent(_pawn.transform, false);
            GaPhys.Hitbox(hitbox);
            GameObject eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(_pawn.transform, false);
            eyeGo.transform.localPosition = new Vector3(0f, 1.72f, 0f);
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
            Downed = false;
            DeathCause = "";
            GaHangar.ClearDeath();
            _vertVel = 0f;
            _wasGrounded = true;
            _airY = spawn.y;
            Sprinting = false;
            Strafe = 0f;
            Active = true;
            Camera cam0 = GaPerfFrameGate.Cam();
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
            GaFps.OnSpawn();
            GaRadio.OnSpawn();
            GaHud.OnSpawn();
            if (GaClass.Picked != GaKitId.Tsf)
                GaHotensia.OnSpawn();
            GaAtLaser.Clear();
            GaJet.Attach(_pawn.transform);
            GaTsf.OnSpawn(_pawn.transform, _cc, _eye);
            EnsureDealer();
        }

        private static void Exit(bool toCockpit)
        {
            GaCombat.ClearBolts();
            GaLoot.Clear();
            GaRifleDrop.Clear();
            GaSupply.Clear();
            GaSupport.Clear();
            GaAtLaser.Clear();
            GaPalmMenu.Clear();
            GaChute.Clear();
            GaJet.Clear();
            GaTsf.Clear();
            GaRide.Clear();
            GaShop.Clear();
            GaGarage.Clear();
            GaRadio.Clear();
            GaFps.Clear();
            GaHotensia.Clear();
            GaDropSquad.DespawnFriendlies();
            GaNet.LeaveInfantry();
            GaPhys.Unregister(_cc);
            GaCombat.DestroyViews();
            RestoreCameraClip();
            if (_pawn != null)
                UnityEngine.Object.Destroy(_pawn);
            _pawn = null;
            _cc = null;
            _eye = null;
            Eye = null;
            _deck = null;
            Active = false;
            Dead = false;
            Downed = false;
            CrashKia = false;
            AirHop = false;
            _chuteDummy = null;
            _chuteArmed = false;
            _chuteSeenId = 0;
            _dummyMuteId = 0;
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

        internal static void Die(string cause, bool instant)
        {
            if (Dead)
                return;
            GaJet.Cut();
            if (!string.IsNullOrEmpty(cause))
                DeathCause = cause;
            if (string.IsNullOrEmpty(DeathCause))
                DeathCause = "KIA";
            if (instant)
            {
                Downed = true;
                Dead = true;
                Hp = 0f;
                _deadUntil = Time.unscaledTime + 2.4f;
                GaHud.CloseMenus();
                GaHud.OnDeath();
                if (_cc != null)
                    _cc.enabled = false;
                GaCombat.ClearBolts();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }
            if (!Downed)
            {
                Downed = true;
                Hp = 0f;
                _deadUntil = Time.unscaledTime + BleedSec;
                GaHud.CloseMenus();
                if (_cc != null)
                    _cc.enabled = false;
                GaRadio.PlayerDown();
                return;
            }
            Dead = true;
            Hp = 0f;
            _deadUntil = Time.unscaledTime + 2.2f;
            if (string.IsNullOrEmpty(DeathCause) || DeathCause == "KIA")
                DeathCause = "BLED OUT";
            GaHud.CloseMenus();
            GaHud.OnDeath();
            if (_cc != null)
                _cc.enabled = false;
            GaCombat.ClearBolts();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void FinishDeath()
        {
            Exit(false);
            GaDeploy.AfterDeath();
            GaHangar.OpenDeath();
            try
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            catch { }
        }

        internal static float BleedLeft()
        {
            if (!Downed || Dead)
                return 0f;
            return Mathf.Clamp01((_deadUntil - Time.unscaledTime) / BleedSec);
        }

        internal static void OnLiftCrash()
        {
            if (CrashKia)
            {
                GaAirLift.Clear();
                return;
            }
            Aircraft ac = GaAirLift.Ac;
            GaAirLift.Clear();
            DeathCause = "CRASH";
            CrashKia = true;
            Dead = true;
            Downed = true;
            Hp = 0f;
            _deadUntil = Time.unscaledTime + 2.4f;
            GaHud.OnDeath();
            Player p = null;
            try { GameManager.GetLocalPlayer(out p); }
            catch { p = null; }
            if (p != null && ac != null)
            {
                try { p.RemoveAircraft(ac); }
                catch { }
            }
            Deny("CRASH  —  you died in the wreck.", "坠机身亡。");
            try
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            catch { }
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

        internal static PersistentID DealerId()
        {
            EnsureDealer();
            if (_ghostDealer.IsValid)
            {
                PersistentUnit ghost;
                if (UnitRegistry.TryGetPersistentUnit(_ghostDealer, out ghost) && ghost != null)
                    return _ghostDealer;
            }
            return ResolveDealerId();
        }

        internal static void EnsureDealer()
        {
            Player p = null;
            try { GameManager.GetLocalPlayer(out p); }
            catch { }
            BindGhostDealer(p);
        }

        private static void BindGhostDealer(Player p)
        {
            if (p == null)
                return;
            string name = "Infantry";
            try
            {
                string n = p.GetDisplayName(PlayerNameContext.ChatOrLeaderboard);
                if (!string.IsNullOrEmpty(n))
                    name = n;
            }
            catch { }
            PersistentUnit pu = null;
            if (_ghostDealer.IsValid)
            {
                try { UnitRegistry.TryGetPersistentUnit(_ghostDealer, out pu); }
                catch { }
            }
            if (pu == null)
            {
                PersistentID id = UnitRegistry.GetNextIndex();
                pu = (PersistentUnit)FormatterServices.GetUninitializedObject(typeof(PersistentUnit));
                pu.id = id;
                pu.unit = null;
                pu.definition = null;
                UnitRegistry.persistentUnitLookup[id] = pu;
                _ghostDealer = id;
            }
            pu.player = p;
            pu.unitName = name;
            try { pu.SetHQ(p.HQ); }
            catch { }
        }

        private static PersistentID ResolveDealerId()
        {
            Player p = null;
            try { GameManager.GetLocalPlayer(out p); }
            catch { }
            PersistentID id;
            if (TryPlayerUnitId(p, out id))
                return id;
            if (TryUnitId(_parked, out id))
                return id;
            Aircraft ac = null;
            try
            {
                if (GameManager.GetLocalAircraft(out ac) && TryUnitId(ac, out id))
                    return id;
            }
            catch { }
            if (p != null)
            {
                try
                {
                    if (TryUnitId(p.Aircraft, out id))
                        return id;
                }
                catch { }
                try
                {
                    if (TryUnitId(p.PilotDismounted, out id))
                        return id;
                }
                catch { }
            }
            if (TryUnitId(_chuteDummy, out id))
                return id;
            if (p != null)
            {
                try
                {
                    foreach (KeyValuePair<PersistentID, PersistentUnit> kv in UnitRegistry.persistentUnitLookup)
                    {
                        if (!kv.Key.IsValid || kv.Value == null)
                            continue;
                        if (!object.ReferenceEquals(kv.Value.player, p))
                            continue;
                        return kv.Key;
                    }
                }
                catch { }
            }
            return PersistentID.None;
        }

        private static bool TryPlayerUnitId(Player p, out PersistentID id)
        {
            id = PersistentID.None;
            if (p == null)
                return false;
            try
            {
                PersistentID? uid = p.UnitID;
                if (!uid.HasValue || !uid.Value.IsValid)
                    return false;
                PersistentUnit pu;
                if (!UnitRegistry.TryGetPersistentUnit(uid.Value, out pu) || pu == null)
                    return false;
                id = uid.Value;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryUnitId(Unit u, out PersistentID id)
        {
            id = PersistentID.None;
            if (u == null)
                return false;
            try
            {
                if (!u.persistentID.IsValid)
                    return false;
                id = u.persistentID;
                return true;
            }
            catch
            {
                return false;
            }
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
            if (!Active || Dead || Downed)
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
            if (GaClass.Picked == GaKitId.Tsf
                && wep != GaArmorMath.WepRocket
                && wep != GaArmorMath.WepMissile
                && wep != GaArmorMath.WepBomb)
            {
                _lastArmorHitAt = Time.time;
                GaHud.FlashHit("BOUNCE  TSF plate  0 dmg");
                return;
            }
            if (GaClass.Picked == GaKitId.Tsf)
            {
                pierce *= 0.28f;
                blast *= 0.22f;
                impact *= 0.28f;
                fire *= 0.22f;
            }
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
                fat *= 0.48f;
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
            float dmg = (pierce * pm + blast * bm + impact * im + fire * 0.4f) * 0.52f;
            if (dmg < 1f)
                dmg = 1f;
            Hp -= dmg;
            string layer = throughPlate ? "PEN plate→body" : "BODY HIT";
            if (outerHit == GaArmorMath.HitOvermatch && throughPlate)
                layer = "OVERPEN plate→body";
            GaHud.FlashHit(layer + "  KE " + Mathf.RoundToInt(impact * im)
                + "  -" + Mathf.RoundToInt(dmg) + " HP");
            if (Hp < 22f)
                GaRadio.PlayerHurt();
            if (Hp <= 0f)
            {
                Hp = 0f;
                Die("GUNFIRE", false);
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
            int id = pd.GetInstanceID();
            bool first = id != _dummyMuteId;
            try { pd.SetCollidable(false); }
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
            if (!first)
                return;
            _dummyMuteId = id;
            try
            {
                CharacterController cc = pd.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = false;
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
