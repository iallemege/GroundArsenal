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
        internal static bool Zh = false;
        internal static float Hp = MaxHp;
        internal const float MaxHp = 80f;
        internal const int OuterArmor = 10;
        internal const int InnerArmor = 1;
        internal static float ArmorFatigue;
        internal static float MoveSpeed;
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
        private static float _lastArmorHitAt;
        private static float _sprintUntil;
        private static float _sprintReadyAt;
        private static float _jumpReadyAt;
        private static bool _chuteArmed;
        private static float _chuteReadyAt;
        private static PilotDismounted _chuteDummy;
        internal const float SprintDur = 60f;
        internal const float SprintCd = 3f;
        internal const float JumpCd = 3f;
        internal const float JumpHeight = 8f;
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
            TickArmorFatigue();
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
                Deny("KIA — Space / Esc to leave.", "KIA — Space / Esc to leave.");
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

            float walk = 4.6f;
            float sprint = 7.2f;
            bool wantSprint = Input.GetKey(KeyCode.LeftShift);
            float now = Time.time;
            if (wantSprint && now >= _sprintReadyAt)
            {
                if (_sprintUntil < now)
                    _sprintUntil = now + SprintDur;
            }
            bool sprinting = wantSprint && now < _sprintUntil;
            if (!sprinting && _sprintUntil > 0f && now >= _sprintUntil)
            {
                _sprintReadyAt = now + SprintCd;
                _sprintUntil = 0f;
            }
            float speed = sprinting ? sprint : walk;
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
            MoveSpeed = wish.sqrMagnitude > 0.01f ? speed : 0f;
            if (_cc == null)
                return;
            if (_cc.isGrounded)
            {
                _vertVel = -2f;
                if (Input.GetKeyDown(KeyCode.Space) && now >= _jumpReadyAt)
                {
                    _vertVel = JumpHeight;
                    _jumpReadyAt = now + JumpCd;
                }
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
            if (now < _sprintUntil)
                return (_sprintUntil - now) / SprintDur;
            if (now < _sprintReadyAt)
                return 1f - (_sprintReadyAt - now) / SprintCd;
            return 1f;
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
                Dead = true;
                _deadUntil = Time.unscaledTime + 8f;
                GaHud.FlashHit("KIA");
                GaHud.CloseMenus();
                if (_cc != null)
                    _cc.enabled = false;
                Deny("KIA — Space / Esc to leave.", "KIA — Space / Esc to leave.");
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
                    return;
                }
            }
            catch
            {
                _chuteArmed = false;
                return;
            }
            PilotDismounted.PilotState st = pd.animationState;
            if (st == PilotDismounted.PilotState.dead)
            {
                _chuteArmed = false;
                return;
            }
            if (st != PilotDismounted.PilotState.landing)
                return;
            if (!_chuteArmed)
            {
                _chuteArmed = true;
                _chuteReadyAt = Time.unscaledTime + 0.65f;
                return;
            }
            if (Time.unscaledTime < _chuteReadyAt)
                return;
            if (Active)
                return;
            Vector3 pos = pd.transform.position;
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 4f, Vector3.down, out hit, 40f,
                ~0, QueryTriggerInteraction.Ignore))
                pos = hit.point + Vector3.up * 0.08f;
            float yaw = pd.transform.rotation.eulerAngles.y;
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
            _chuteDummy = pd;
            SpawnPawn(pos, yaw);
            GaCombat.ApplyCarryOnly();
            GaCombat.AttachViews(_eye);
            _chuteArmed = false;
        }
    }
}
