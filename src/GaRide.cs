using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Jobs;
using UnityEngine;

namespace GroundArsenal
{
    internal static class GaRide
    {
        internal const int SeatCount = 4;
        internal static bool Mounted;
        internal static int Seat;

        private static Unit _host;
        private static readonly Vector3[] _seats = new Vector3[SeatCount];
        private static bool _heldAi;
        private static float _throttle;
        private static float _steer;
        private static float _nextFire;
        private static float _nextDest;
        private static int _wep;
        private static FieldInfo _jobFields;
        private static FieldInfo _mobile;
        private static FieldInfo _resetStationary;
        private static FieldInfo _anchored;
        private static FieldInfo _turretUnit;
        private static FieldInfo _aiShip;
        private static MethodInfo _aimTurret;
        private static readonly List<Unit> _ride = new List<Unit>(32);
        private static float _nextRide;
        private static Unit _skipHost;
        private static float _skipUntil;
        private static bool _orbit;
        private static float _orbitToggleUntil;
        private static readonly List<GaDropBot> _crew = new List<GaDropBot>(4);
        private static float _nextInvite;

        internal static bool Busy
        {
            get { return Mounted && _host != null; }
        }

        internal static bool Driving
        {
            get { return Busy && Seat == 0 && CanHelm(_host); }
        }

        internal static bool Gunner
        {
            get { return Busy && Seat == 1; }
        }

        internal static bool Passenger
        {
            get { return Busy && Seat >= 2; }
        }

        internal static bool CanFire
        {
            get { return Busy && Seat < 2; }
        }

        internal static bool Orbiting
        {
            get { return Busy && _orbit; }
        }

        internal static GroundVehicle Vehicle
        {
            get { return _host as GroundVehicle; }
        }

        internal static Unit Host
        {
            get { return _host; }
        }

        internal static WeaponStation CurrentStation()
        {
            return PickStation();
        }

        internal static bool OpticsMissile()
        {
            return IsMissile(PickStation());
        }

        internal static bool Helming(ShipAI ai)
        {
            if (!Busy || !CanHelm(_host) || ai == null || _host == null)
                return false;
            Ship want = _host as Ship;
            if (want == null)
                return false;
            Ship have = ShipOf(ai);
            return have != null && object.ReferenceEquals(have, want);
        }

        internal static void PushShipInputs(ShipAI ai)
        {
            if (!Helming(ai))
                return;
            DriveShip();
        }

        internal static void Tick()
        {
            if (!Mounted)
                return;
            if (_host == null || _host.disabled)
            {
                Dismount();
                return;
            }
            TickSeatKeys();
            TickWepKeys();
            TickAds();
            ReadDrive();
            if (Busy && Vehicle != null && CanHelm(Vehicle))
            {
                KeepLoose();
                if (Driving)
                {
                    DriveRb();
                    CommandDest();
                }
            }
            if (Driving)
            {
                DriveShip();
                CommandShip();
            }
            StickPawn();
            AimAll();
            TickFire();
            TickCrew();
            TickOrbit();
        }

        internal static bool TryToggle()
        {
            if (Mounted)
            {
                Unit next = Nearest(_host);
                if (next != null && !ReferenceEquals(next, _host) && LookingAt(next))
                {
                    bool keepOrbit = _orbit;
                    Dismount();
                    Mount(next, 0);
                    if (keepOrbit)
                        EnterOrbit();
                    return true;
                }
                Dismount();
                return true;
            }
            Unit u = Nearest(null);
            if (u == null)
                return false;
            Mount(u, 0);
            return true;
        }

        internal static string Hint()
        {
            if (Mounted)
            {
                string wep = WepName(PickStation());
                string view = _orbit ? "ORBIT" : "SEAT";
                if (Passenger)
                    return "F3/F4 PAX  L orbit  T off  " + view;
                if (Driving)
                    return "F1 HELM  WASD  F2 GUN  L orbit  TAB " + wep + "  T off";
                if (Gunner)
                    return "F2 GUN  TAB " + wep + "  RMB  SELECT lock  L orbit  T off";
                return "MAN  TAB " + wep + "  L orbit  T off";
            }
            Unit u = Nearest();
            if (u == null)
                return "";
            string n = UnitName(u);
            if (u is Ship)
                return n + "  T  BOARD  TAB weapons";
            if (CanDrive(u as GroundVehicle))
                return n + "  T  RIDE";
            return n + "  T  MAN  TAB weapons";
        }

        internal static void Clear()
        {
            if (Mounted)
                ReleaseAi();
            KickCrew();
            LeaveOrbit();
            GaIff.Stop();
            Mounted = false;
            _host = null;
            Seat = 0;
            _wep = 0;
            _throttle = 0f;
            _steer = 0f;
            _orbit = false;
            GaCombat.Ads = 0f;
            GaMode.SetWalker(true);
        }

        internal static void ApplyCam(CameraStateManager mgr)
        {
            if (mgr == null || _host == null)
                return;
            Camera cam = mgr.mainCamera;
            if (cam == null)
                cam = GaPerfFrameGate.Cam();
            if (cam == null)
                return;
            try
            {
                Transform parent = cam.transform.parent;
                if (parent != null)
                {
                    bool sticky = false;
                    try
                    {
                        sticky = mgr.cameraPivot != null && parent == mgr.cameraPivot;
                    }
                    catch { }
                    if (!sticky)
                    {
                        try
                        {
                            sticky = parent.GetComponent<Rigidbody>() != null
                                || parent.GetComponent<Unit>() != null;
                        }
                        catch { }
                    }
                    if (sticky)
                        cam.transform.SetParent(mgr.transform, true);
                }
            }
            catch { }
            if (_orbit)
            {
                ApplyFakeOrbit(cam);
                return;
            }
            cam.transform.position = CamPos();
            cam.transform.rotation = Quaternion.Euler(GaMode.LookPitch, GaMode.LookYaw, 0f);
        }

        internal static Vector3 AimFwd()
        {
            return Quaternion.Euler(GaMode.LookPitch, GaMode.LookYaw, 0f) * Vector3.forward;
        }

        internal static void InjectPath(GroundVehicle v)
        {
            if (!Busy || v == null || !object.ReferenceEquals(v, _host) || !CanHelm(v))
                return;
            FieldInfo fi = JobFields();
            if (fi == null)
                return;
            object boxed;
            try { boxed = fi.GetValue(v); }
            catch { return; }
            if (boxed == null)
                return;
            try
            {
                PtrAllocation<GroundVehicleFields> alloc = (PtrAllocation<GroundVehicleFields>)boxed;
                ref GroundVehicleFields f = ref alloc.Ref();
                f.mobile = true;
                f.stationary = false;
                f.inputs.throttle = _throttle;
                f.inputs.steering = _steer;
                f.inputs.brake = Mathf.Abs(_throttle) < 0.08f ? 1f : 0f;
                if (Mathf.Abs(_throttle) < 0.08f && Mathf.Abs(_steer) < 0.08f)
                {
                    f.steeringInfoNullable = null;
                    f.reverseTimer = 0f;
                    return;
                }
                Vector3 fwd = v.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.forward;
                else
                    fwd.Normalize();
                Vector3 dir = Quaternion.AngleAxis(_steer * 55f, Vector3.up) * fwd;
                if (_throttle < -0.12f)
                    dir = -dir;
                f.steeringInfoNullable = new SteeringInfo(dir, 12f);
                if (_throttle < -0.12f)
                    f.reverseTimer = 0.18f;
                else
                    f.reverseTimer = 0f;
            }
            catch { }
        }

        internal static bool HandleTurret(Turret turret)
        {
            if (!Busy || turret == null || _host == null)
                return false;
            Unit att = TurretUnit(turret);
            if (att == null || !object.ReferenceEquals(att, _host))
                return false;
            int stIx = StationOfTurret(turret);
            if (Passenger || !CanFire || BotOwnsGun(stIx))
                return true;
            Vector3 aim = AimFwd();
            try { turret.SetStowed(false); }
            catch { }
            try { turret.SetManual(true); }
            catch { }
            try { turret.SetVector(aim); }
            catch { }
            MethodInfo aimFn = AimTurretFn();
            if (aimFn != null)
            {
                try { aimFn.Invoke(turret, new object[] { aim }); }
                catch { }
            }
            return true;
        }

        internal static void AfterApply(GroundVehicle v)
        {
            InjectPath(v);
            if (!Driving || v == null || !object.ReferenceEquals(v, _host))
                return;
            DriveRb();
        }

        internal static bool BlockStationary(GroundVehicle v, bool stationary)
        {
            return stationary && Busy && v != null && CanHelm(v) && object.ReferenceEquals(v, _host);
        }

        private static void Mount(Unit u, int seat)
        {
            _host = u;
            Seat = Mathf.Clamp(seat, 0, SeatCount - 1);
            if (!CanHelm(u))
                Seat = 0;
            Mounted = true;
            _wep = 0;
            FillSeats(u);
            HoldAi(u);
            GaMode.SetWalker(false);
            ArmTurrets(u);
            GaIff.Start(u);
            _orbit = false;
            string n = UnitName(u);
            if (CanHelm(u))
                GaMode.Deny(n + "  F1 helm  F2 gun  F3/F4 pax  L orbit  T off",
                    n + "  F1驾驶  F2炮手  F3/F4乘客  L环视  T离开");
            else
                GaMode.Deny(n + "  TAB weapons  L orbit  T off",
                    n + "  TAB切武器  L环视  T离开");
        }

        private static void Dismount()
        {
            Transform pawn = GaMode.PawnRoot;
            Unit u = _host;
            int seat = Seat;
            KickCrew();
            LeaveOrbit();
            GaIff.Stop();
            ReleaseAi();
            GaCombat.Ads = 0f;
            if (pawn != null && u != null)
            {
                Vector3 side = (seat % 2) == 0 ? -u.transform.right : u.transform.right;
                float off = u is Ship ? 8.5f : 2.3f;
                float up = u is Ship ? 2.4f : 0.4f;
                Vector3 p = u.transform.position + side * off + u.transform.up * up;
                RaycastHit hit;
                if (GaPhys.RayDown(p + Vector3.up * 4f, 14f, out hit))
                    p = hit.point + Vector3.up * 0.08f;
                pawn.position = p;
            }
            Mounted = false;
            _host = null;
            Seat = 0;
            _wep = 0;
            _throttle = 0f;
            _steer = 0f;
            _orbit = false;
            GaMode.SetWalker(true);
            if (u != null)
            {
                _skipHost = u;
                _skipUntil = Time.unscaledTime + 6f;
            }
            GaMode.Deny("Dismounted.", "已离开。");
        }

        private static void TickSeatKeys()
        {
            if (GaHud.BlocksLook || GaRadio.Open)
                return;
            if (!CanHelm(_host))
                return;
            if (Input.GetKeyDown(KeyCode.F1))
                Seat = 0;
            else if (Input.GetKeyDown(KeyCode.F2))
                Seat = 1;
            else if (Input.GetKeyDown(KeyCode.F3))
                Seat = 2;
            else if (Input.GetKeyDown(KeyCode.F4))
                Seat = 3;
            else
                return;
            FillSeats(_host);
            ReassignCrewGuns();
            string lab = SeatLabel();
            GaMode.Deny(lab, lab);
        }

        private static void TickWepKeys()
        {
            if (Passenger)
                return;
            if (GaHud.BlocksLook || GaRadio.Open)
                return;
            int n = StationCount();
            if (n <= 0)
                return;
            int was = _wep;
            if (Input.GetKeyDown(KeyCode.Tab))
                _wep++;
            bool ads = GaVr.Ads();
            float w = 0f;
            if (!ads)
            {
                try { w = Input.GetAxis("Mouse ScrollWheel"); }
                catch { w = 0f; }
            }
            if (w > 0.01f)
                _wep++;
            else if (w < -0.01f)
                _wep--;
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < n)
                    _wep = i;
            }
            if (_wep < 0)
                _wep = n - 1;
            if (_wep >= n)
                _wep = 0;
            if (_wep != was)
            {
                ReassignCrewGuns();
                GaMode.Deny(WepName(PickStation()), WepName(PickStation()));
                GaIff.RefreshStation();
            }
        }

        private static void TickAds()
        {
            if (Passenger)
            {
                GaCombat.Ads = Mathf.MoveTowards(GaCombat.Ads, 0f, Time.deltaTime * 7f);
                return;
            }
            float dt = Time.deltaTime;
            bool want = !GaHud.BlocksLook && GaVr.Ads();
            GaCombat.Ads = Mathf.MoveTowards(GaCombat.Ads, want ? 1f : 0f, dt * 7f);
            if (want)
            {
                float wh = 0f;
                try { wh = Input.GetAxis("Mouse ScrollWheel"); }
                catch { wh = 0f; }
                if (wh > 0.01f)
                    GaCombat.Zoom = Mathf.Clamp01(GaCombat.Zoom + 0.11f);
                else if (wh < -0.01f)
                    GaCombat.Zoom = Mathf.Clamp01(GaCombat.Zoom - 0.11f);
            }
            else if (GaCombat.Ads < 0.08f)
                GaCombat.Zoom = Mathf.MoveTowards(GaCombat.Zoom, 0f, dt * 2.4f);
        }

        private static void ReadDrive()
        {
            _throttle = 0f;
            _steer = 0f;
            if (!Driving || GaHud.BlocksLook)
                return;
            if (Input.GetKey(KeyCode.W))
                _throttle += 1f;
            if (Input.GetKey(KeyCode.S))
                _throttle -= 1f;
            if (Input.GetKey(KeyCode.D))
                _steer += 1f;
            if (Input.GetKey(KeyCode.A))
                _steer -= 1f;
            if (GaVr.Active)
            {
                Vector3 wish = GaVr.MoveWish;
                _throttle += wish.z;
                _steer += wish.x;
            }
            _throttle = Mathf.Clamp(_throttle, -1f, 1f);
            _steer = Mathf.Clamp(_steer, -1f, 1f);
            if (Input.GetKey(KeyCode.Space))
                _throttle = 0f;
        }

        private static void StickPawn()
        {
            Transform pawn = GaMode.PawnRoot;
            if (pawn == null || _host == null)
                return;
            pawn.position = _host.transform.TransformPoint(_seats[Seat]);
        }

        private static void AimAll()
        {
            if (_host == null || Passenger || !CanFire)
                return;
            Vector3 aim = AimFwd();
            List<WeaponStation> stations = _host.weaponStations;
            if (stations == null)
                return;
            for (int i = 0; i < stations.Count; i++)
            {
                if (BotOwnsGun(i) && i != _wep)
                    continue;
                WeaponStation st = stations[i];
                if (st == null)
                    continue;
                AimStation(st, aim);
            }
        }

        private static void TickFire()
        {
            if (_host == null || GaHud.BlocksLook || !CanFire)
                return;
            if (!GaVr.Fire())
                return;
            if (Time.time < _nextFire)
                return;
            WeaponStation st = PickStation();
            if (st == null)
                return;
            Unit tgt = GaIff.Primary();
            if (tgt == null)
                tgt = RayTarget();
            if (tgt == null)
                tgt = AimHostile();
            bool missile = IsMissile(st);
            if (missile && tgt == null)
                return;
            bool ok = false;
            try
            {
                if (tgt != null)
                {
                    try { st.SetStationTargets(new PersistentID[] { tgt.persistentID }); }
                    catch { }
                }
                st.SetTurretVector(AimFwd());
                st.Fire(_host, tgt);
                ok = true;
            }
            catch { }
            if (!ok)
            {
                try
                {
                    st.RemoteFireSingle(_host);
                    ok = true;
                }
                catch { }
            }
            if (ok && tgt != null)
            {
                List<Turret> guns = st.Turrets;
                if (guns != null)
                {
                    for (int i = 0; i < guns.Count; i++)
                    {
                        if (guns[i] == null)
                            continue;
                        try { guns[i].SetTargetFromController(tgt); }
                        catch { }
                    }
                }
            }
            float wait = 0.08f;
            if (missile)
                wait = 0.55f;
            if (!ok)
                wait = 0.18f;
            _nextFire = Time.time + wait;
        }

        private static WeaponStation PickStation()
        {
            List<WeaponStation> stations = _host != null ? _host.weaponStations : null;
            if (stations == null || stations.Count == 0)
                return null;
            int idx = _wep;
            if (idx < 0)
                idx = 0;
            if (idx >= stations.Count)
                idx = idx % stations.Count;
            WeaponStation st = stations[idx];
            if (st != null)
                return st;
            return stations[0];
        }

        private static int StationCount()
        {
            List<WeaponStation> stations = _host != null ? _host.weaponStations : null;
            return stations != null ? stations.Count : 0;
        }

        private static Unit RayTarget()
        {
            Vector3 origin = CamPos();
            Vector3 dir = AimFwd();
            RaycastHit hit;
            if (!Physics.Raycast(origin, dir, out hit, 12000f, GaPhys.WorldMask, QueryTriggerInteraction.Ignore))
                return null;
            if (hit.collider == null)
                return null;
            return hit.collider.GetComponentInParent<Unit>();
        }

        private static Unit AimHostile()
        {
            List<Unit> all = null;
            try { all = UnitRegistry.allUnits; }
            catch { all = GaPerfFrameGate.PlotUnits(); }
            if (all == null)
                return null;
            FactionHQ hq = GaPerfFrameGate.Hq();
            Vector3 origin = CamPos();
            Vector3 dir = AimFwd();
            Unit best = null;
            float bestAng = 14f;
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i];
                if (u == null || u.disabled)
                    continue;
                if (object.ReferenceEquals(u, _host))
                    continue;
                if (u is Missile || u is PilotDismounted)
                    continue;
                try
                {
                    if (hq != null && u.NetworkHQ != null && object.ReferenceEquals(u.NetworkHQ, hq))
                        continue;
                }
                catch { }
                Vector3 p;
                try { p = u.GetCenterOfMass(); }
                catch { p = u.transform.position; }
                Vector3 to = p - origin;
                if (to.sqrMagnitude < 16f)
                    continue;
                float ang = Vector3.Angle(dir, to);
                if (ang < bestAng)
                {
                    bestAng = ang;
                    best = u;
                }
            }
            return best;
        }

        private static Vector3 CamPos()
        {
            if (_host == null)
                return GaMode.EyePos;
            if (Passenger)
                return _host.transform.TransformPoint(_seats[Seat]) + _host.transform.up * 0.45f;
            Turret gun = CurrentTurret();
            if (gun != null && (!Driving || Seat > 0))
                return gun.transform.position + gun.transform.up * 0.42f + Vector3.up * 0.12f;
            Transform cv = null;
            try { cv = _host.cockpitViewPoint; }
            catch { cv = null; }
            if (cv != null)
                return cv.position;
            if (gun != null)
                return gun.transform.position + gun.transform.up * 0.45f;
            return _host.transform.position + _host.transform.up * 1.55f + _host.transform.forward * 0.18f;
        }

        private static Turret CurrentTurret()
        {
            WeaponStation st = PickStation();
            if (st != null && st.Turrets != null && st.Turrets.Count > 0)
            {
                Turret t = st.Turrets[0];
                if (t != null)
                    return t;
            }
            return SeatTurret();
        }

        private static Turret SeatTurret()
        {
            List<WeaponStation> stations = _host != null ? _host.weaponStations : null;
            if (stations == null)
                return null;
            int want = _wep;
            int n = 0;
            for (int i = 0; i < stations.Count; i++)
            {
                WeaponStation st = stations[i];
                if (st == null || st.Turrets == null)
                    continue;
                for (int t = 0; t < st.Turrets.Count; t++)
                {
                    Turret turret = st.Turrets[t];
                    if (turret == null)
                        continue;
                    if (n == want)
                        return turret;
                    n++;
                }
            }
            return null;
        }

        private static void ArmTurrets(Unit u)
        {
            if (u == null)
                return;
            GroundVehicle gv = u as GroundVehicle;
            if (gv != null)
            {
                try { gv.StowTurrets(false); }
                catch { }
                try { gv.RpcDeployFireControl(true); }
                catch { }
            }
            List<WeaponStation> stations = u.weaponStations;
            if (stations == null)
                return;
            for (int i = 0; i < stations.Count; i++)
            {
                WeaponStation st = stations[i];
                if (st == null || st.Turrets == null)
                    continue;
                for (int t = 0; t < st.Turrets.Count; t++)
                {
                    Turret turret = st.Turrets[t];
                    if (turret == null)
                        continue;
                    try { turret.SetStowed(false); }
                    catch { }
                    try { turret.SetManual(true); }
                    catch { }
                }
            }
        }

        private static void HoldAi(Unit u)
        {
            if (u == null || _heldAi)
                return;
            Ship ship = u as Ship;
            if (ship != null)
            {
                if (!Driving)
                    return;
                _heldAi = true;
                UnstickShip(ship);
                return;
            }
            GroundVehicle v = u as GroundVehicle;
            if (v == null)
                return;
            _heldAi = true;
            if (!CanDrive(v))
            {
                try { v.SetHoldPosition(true); }
                catch { }
                return;
            }
            try { v.StopImmediately(); }
            catch { }
            Unstick(v);
        }

        private static void DriveShip()
        {
            Ship ship = _host as Ship;
            if (!Driving || ship == null)
                return;
            UnstickShip(ship);
            try
            {
                ShipInputs inp = ship.GetInputs();
                if (inp != null)
                {
                    inp.throttle = _throttle;
                    inp.steering = _steer;
                }
            }
            catch { }
            DriveShipRb(ship);
        }

        private static void UnstickShip(Ship ship)
        {
            if (ship == null)
                return;
            try { ship.SetHoldPosition(false); }
            catch { }
            try { ship.SetLocalSim(true); }
            catch { }
            Rigidbody rb = null;
            try { rb = ship.rb; }
            catch { rb = null; }
            if (rb == null)
                return;
            try
            {
                if (rb.isKinematic)
                    rb.isKinematic = false;
            }
            catch { }
        }

        private static void DriveShipRb(Ship ship)
        {
            if (ship == null)
                return;
            Rigidbody rb = null;
            try { rb = ship.rb; }
            catch { rb = null; }
            if (rb == null)
                return;
            try
            {
                if (rb.isKinematic)
                    rb.isKinematic = false;
            }
            catch { return; }
            Vector3 fwd = ship.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.forward;
            else
                fwd.Normalize();
            rb.AddForce(fwd * (_throttle * 4.2f), ForceMode.Acceleration);
            if (Mathf.Abs(_steer) > 0.04f)
                rb.AddTorque(Vector3.up * (_steer * 0.22f), ForceMode.Acceleration);
        }

        private static void CommandShip()
        {
            Ship ship = _host as Ship;
            if (!Driving || ship == null)
                return;
            if (Mathf.Abs(_throttle) < 0.12f && Mathf.Abs(_steer) < 0.12f)
                return;
            if (Time.unscaledTime < _nextDest)
                return;
            _nextDest = Time.unscaledTime + 0.4f;
            Vector3 fwd = ship.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.forward;
            else
                fwd.Normalize();
            Vector3 dir = Quaternion.AngleAxis(_steer * 40f, Vector3.up) * fwd;
            if (_throttle < -0.12f)
                dir = -dir;
            Vector3 dest = ship.transform.position + dir * (90f + Mathf.Abs(_throttle) * 240f);
            dest.y = ship.transform.position.y;
            try
            {
                UnitCommand cmd = ship.UnitCommand;
                if (cmd != null)
                    cmd.SetDestination(dest.ToGlobalPosition(), true);
            }
            catch { }
        }

        private static Ship ShipOf(ShipAI ai)
        {
            if (ai == null)
                return null;
            if (_aiShip == null)
                _aiShip = AccessTools.Field(typeof(ShipAI), "ship");
            if (_aiShip != null)
            {
                try
                {
                    Ship s = _aiShip.GetValue(ai) as Ship;
                    if (s != null)
                        return s;
                }
                catch { }
            }
            try { return ai.GetComponentInParent<Ship>(); }
            catch { return null; }
        }

        internal static void StickBot(GaDropBot bot)
        {
            if (bot == null || _host == null)
                return;
            int seat = bot.RideSeat;
            if (seat < 0 || seat >= SeatCount)
                seat = 2;
            bot.transform.position = _host.transform.TransformPoint(_seats[seat]);
            bot.transform.rotation = _host.transform.rotation;
        }

        internal static bool TryBoardBot(GaDropBot bot)
        {
            if (!Busy || bot == null || _host == null)
                return false;
            if (bot.Downed || bot.Hp <= 0f || bot.Falling)
                return false;
            if (bot.Kit == GaKitId.Tsf)
                return false;
            if (bot.RideHost != null)
                return object.ReferenceEquals(bot.RideHost, _host);
            float lim = _host is Ship ? 625f : 220f;
            float d = (bot.transform.position - _host.transform.position).sqrMagnitude;
            if (d > lim)
                return false;
            int seat = FreeSeat();
            if (seat < 0)
                return false;
            bot.Board(_host, seat, FreeGun());
            _crew.Add(bot);
            return true;
        }

        private static void TickCrew()
        {
            if (!Busy || _host == null)
                return;
            if (Time.unscaledTime >= _nextInvite)
            {
                _nextInvite = Time.unscaledTime + 0.45f;
                GaDropSquad.InviteTo(_host);
            }
            for (int i = _crew.Count - 1; i >= 0; i--)
            {
                GaDropBot b = _crew[i];
                if (b == null || b.Downed || b.Hp <= 0f
                    || !object.ReferenceEquals(b.RideHost, _host))
                {
                    if (b != null)
                        b.ClearRide();
                    _crew.RemoveAt(i);
                    continue;
                }
                StickBot(b);
                if (b.RideGun < 0 || GaHud.BlocksLook)
                    continue;
                if (Time.time < b.RideNextFire)
                    continue;
                WeaponStation st = StationAt(b.RideGun);
                if (st == null)
                    continue;
                Unit tgt = CrewTarget();
                Vector3 aim = tgt != null ? TargetPoint(tgt) : AimFwd();
                AimStation(st, aim);
                bool missile = IsMissile(st);
                if (missile && tgt == null)
                    continue;
                FireStation(st, tgt, aim);
                b.RideNextFire = Time.time + (missile ? 0.7f : 0.22f);
            }
        }

        private static void KickCrew()
        {
            Unit u = _host;
            for (int i = 0; i < _crew.Count; i++)
            {
                GaDropBot b = _crew[i];
                if (b == null)
                    continue;
                if (u != null)
                {
                    Vector3 side = ((i & 1) == 0 ? -u.transform.right : u.transform.right);
                    float off = u is Ship ? 8.5f : 2.4f;
                    Vector3 p = u.transform.position + side * off + u.transform.up * 0.4f;
                    RaycastHit hit;
                    if (GaPhys.RayDown(p + Vector3.up * 4f, 14f, out hit))
                        p = hit.point + Vector3.up * 0.08f;
                    b.transform.position = p;
                }
                b.ClearRide();
            }
            _crew.Clear();
        }

        private static void ReassignCrewGuns()
        {
            for (int i = 0; i < _crew.Count; i++)
            {
                GaDropBot b = _crew[i];
                if (b == null)
                    continue;
                if (b.RideGun >= 0 && CanFire && b.RideGun == _wep)
                    b.RideGun = -1;
            }
            for (int i = 0; i < _crew.Count; i++)
            {
                GaDropBot b = _crew[i];
                if (b == null)
                    continue;
                if (b.RideGun < 0)
                    b.RideGun = FreeGun();
            }
        }

        private static int FreeSeat()
        {
            bool[] taken = new bool[SeatCount];
            taken[Seat] = true;
            taken[0] = true;
            for (int i = 0; i < _crew.Count; i++)
            {
                GaDropBot b = _crew[i];
                if (b == null)
                    continue;
                if (b.RideSeat >= 0 && b.RideSeat < SeatCount)
                    taken[b.RideSeat] = true;
            }
            for (int s = 1; s < SeatCount; s++)
            {
                if (!taken[s])
                    return s;
            }
            return -1;
        }

        private static int FreeGun()
        {
            int n = StationCount();
            if (n <= 0)
                return -1;
            for (int i = 0; i < n; i++)
            {
                if (CanFire && i == _wep)
                    continue;
                if (BotOwnsGun(i))
                    continue;
                return i;
            }
            return -1;
        }

        private static bool BotOwnsGun(int gun)
        {
            if (gun < 0)
                return false;
            for (int i = 0; i < _crew.Count; i++)
            {
                GaDropBot b = _crew[i];
                if (b != null && b.RideGun == gun)
                    return true;
            }
            return false;
        }

        private static WeaponStation StationAt(int ix)
        {
            List<WeaponStation> stations = _host != null ? _host.weaponStations : null;
            if (stations == null || ix < 0 || ix >= stations.Count)
                return null;
            return stations[ix];
        }

        private static int StationOfTurret(Turret turret)
        {
            List<WeaponStation> stations = _host != null ? _host.weaponStations : null;
            if (stations == null || turret == null)
                return -1;
            for (int i = 0; i < stations.Count; i++)
            {
                WeaponStation st = stations[i];
                if (st == null || st.Turrets == null)
                    continue;
                for (int t = 0; t < st.Turrets.Count; t++)
                {
                    if (object.ReferenceEquals(st.Turrets[t], turret))
                        return i;
                }
            }
            return -1;
        }

        private static void AimStation(WeaponStation st, Vector3 aim)
        {
            if (st == null)
                return;
            try { st.SetTurretVector(aim); }
            catch { }
            List<Turret> turrets = st.Turrets;
            if (turrets == null)
                return;
            for (int t = 0; t < turrets.Count; t++)
            {
                Turret turret = turrets[t];
                if (turret == null)
                    continue;
                try { turret.SetStowed(false); }
                catch { }
                try { turret.SetManual(true); }
                catch { }
                try { turret.SetVector(aim); }
                catch { }
            }
        }

        private static void FireStation(WeaponStation st, Unit tgt, Vector3 aim)
        {
            if (st == null || _host == null)
                return;
            try
            {
                if (tgt != null)
                {
                    try { st.SetStationTargets(new PersistentID[] { tgt.persistentID }); }
                    catch { }
                }
                st.SetTurretVector(aim);
                st.Fire(_host, tgt);
            }
            catch
            {
                try { st.RemoteFireSingle(_host); }
                catch { }
            }
        }

        private static Unit CrewTarget()
        {
            Unit tgt = GaIff.Primary();
            if (tgt != null && !tgt.disabled)
                return tgt;
            return AimHostile();
        }

        private static Vector3 TargetPoint(Unit u)
        {
            if (u == null)
                return AimFwd();
            try { return u.GetCenterOfMass(); }
            catch { return u.transform.position; }
        }

        private static string SeatLabel()
        {
            if (Seat <= 0)
                return "F1 DRIVER";
            if (Seat == 1)
                return "F2 GUN";
            return "F" + (Seat + 1).ToString() + " PASSENGER";
        }

        private static void TickOrbit()
        {
            if (!Busy || GaHud.BlocksLook || GaRadio.Open)
                return;
            if (Time.unscaledTime < _orbitToggleUntil)
                return;
            bool press = Input.GetKeyDown(KeyCode.L);
            try
            {
                Rewired.Player inp = GameManager.playerInput;
                if (inp != null && inp.GetButtonDown("Switch View"))
                    press = true;
            }
            catch { }
            if (!press)
                return;
            _orbitToggleUntil = Time.unscaledTime + 0.18f;
            if (_orbit)
                LeaveOrbit();
            else
                EnterOrbit();
        }

        private static void EnterOrbit()
        {
            _orbit = true;
            DetachCam();
            try { FlightHud.EnableCanvas(true); }
            catch { }
            GaMode.Deny("ORBIT", "环视");
        }

        private static void LeaveOrbit()
        {
            bool was = _orbit;
            _orbit = false;
            DetachCam();
            if (!was)
                return;
            GaMode.Deny("SEAT", "座位");
        }

        private static void DetachCam()
        {
            CameraStateManager mgr = null;
            try { mgr = SceneSingleton<CameraStateManager>.i; }
            catch { mgr = null; }
            if (mgr == null)
                return;
            try
            {
                if (mgr.cameraPivot != null)
                    mgr.cameraPivot.SetParent(null, true);
            }
            catch { }
            try
            {
                if (mgr.mainCamera != null)
                    mgr.mainCamera.transform.SetParent(mgr.transform, true);
            }
            catch { }
            try
            {
                CameraBaseState cur = mgr.currentState;
                if (cur == mgr.orbitState || cur == mgr.cockpitState || cur == mgr.chaseState
                    || cur == mgr.TVState)
                    mgr.SwitchState(mgr.freeState);
            }
            catch { }
            try { CameraStateManager.cameraMode = CameraMode.free; }
            catch { }
            try { CameraStateManager.enableMouseLook = false; }
            catch { }
            try
            {
                if (mgr.mainCamera != null && mgr.mainCamera.nearClipPlane > 0.08f)
                    mgr.mainCamera.nearClipPlane = 0.03f;
            }
            catch { }
        }

        private static void ApplyFakeOrbit(Camera cam)
        {
            if (cam == null || _host == null)
                return;
            float dist = _host is Ship ? 42f : 11f;
            float lift = _host is Ship ? 14f : 3.2f;
            try
            {
                float r = _host.maxRadius;
                if (r > 4f)
                    dist = Mathf.Clamp(r * 2.4f, 8f, 80f);
            }
            catch { }
            Vector3 back = Quaternion.Euler(GaMode.LookPitch, GaMode.LookYaw, 0f)
                * new Vector3(0f, 0f, -dist);
            Vector3 at = _host.transform.position + Vector3.up * lift;
            cam.transform.position = at + back;
            Vector3 look = at - cam.transform.position;
            if (look.sqrMagnitude < 0.01f)
                look = -_host.transform.forward;
            cam.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        private static void KeepLoose()
        {
            GroundVehicle v = Vehicle;
            if (v == null)
                return;
            try { v.SetHoldPosition(false); }
            catch { }
            try { v.SetWheelsLocked(false); }
            catch { }
            Rigidbody rb = Rb(v);
            if (rb == null)
                return;
            try
            {
                if (rb.isKinematic)
                    rb.isKinematic = false;
            }
            catch { }
        }

        private static void Unstick(GroundVehicle v)
        {
            if (v == null)
                return;
            try { v.SetHoldPosition(false); }
            catch { }
            try { v.SetWheelsLocked(false); }
            catch { }
            try { v.SetLocalSim(true); }
            catch { }
            if (_resetStationary == null)
                _resetStationary = AccessTools.Field(typeof(GroundVehicle), "resetStationary");
            if (_resetStationary != null)
            {
                try { _resetStationary.SetValue(v, true); }
                catch { }
            }
            if (_anchored == null)
                _anchored = AccessTools.Field(typeof(GroundVehicle), "anchored");
            if (_anchored != null)
            {
                try { _anchored.SetValue(v, false); }
                catch { }
            }
            try { v.NetworknetworkStationary = false; }
            catch { }
            try { v.OnStationaryChanged(false); }
            catch { }
            Rigidbody rb = Rb(v);
            if (rb == null)
                return;
            try { rb.isKinematic = false; }
            catch { }
            try { rb.useGravity = true; }
            catch { }
        }

        private static void DriveRb()
        {
            GroundVehicle v = Vehicle;
            if (!Driving || v == null)
                return;
            Rigidbody rb = Rb(v);
            if (rb == null)
                return;
            try
            {
                if (rb.isKinematic)
                    rb.isKinematic = false;
            }
            catch { return; }
            Vector3 fwd = v.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.forward;
            else
                fwd.Normalize();
            float top = 16f;
            try
            {
                float t = v.GetTopSpeed();
                if (t > 3f)
                    top = Mathf.Clamp(t, 8f, 32f);
            }
            catch { }
            float dt = Time.deltaTime;
            if (dt < 0.008f)
                dt = 0.008f;
            if (dt > 0.05f)
                dt = 0.05f;
            Vector3 vel = rb.velocity;
            float along = Vector3.Dot(new Vector3(vel.x, 0f, vel.z), fwd);
            float want = _throttle * top;
            float next = Mathf.MoveTowards(along, want, dt * (Mathf.Abs(_throttle) > 0.08f ? 14f : 22f));
            Vector3 flat = fwd * next;
            rb.velocity = new Vector3(flat.x, vel.y, flat.z);
            if (Mathf.Abs(_steer) > 0.04f && Mathf.Abs(next) > 0.35f)
            {
                float yaw = _steer * (1.35f + Mathf.Clamp(Mathf.Abs(next) * 0.06f, 0f, 2.2f));
                Vector3 av = rb.angularVelocity;
                rb.angularVelocity = new Vector3(av.x, yaw, av.z);
            }
        }

        private static void CommandDest()
        {
            GroundVehicle v = Vehicle;
            if (!Driving || v == null)
                return;
            if (Mathf.Abs(_throttle) < 0.12f && Mathf.Abs(_steer) < 0.12f)
                return;
            if (Time.unscaledTime < _nextDest)
                return;
            _nextDest = Time.unscaledTime + 0.22f;
            Vector3 fwd = v.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.forward;
            else
                fwd.Normalize();
            Vector3 dir = Quaternion.AngleAxis(_steer * 50f, Vector3.up) * fwd;
            if (_throttle < -0.12f)
                dir = -dir;
            Vector3 dest = v.transform.position + dir * (18f + Mathf.Abs(_throttle) * 28f);
            try
            {
                UnitCommand cmd = v.UnitCommand;
                if (cmd != null)
                    cmd.SetDestination(dest.ToGlobalPosition(), true);
            }
            catch { }
        }

        private static Rigidbody Rb(GroundVehicle v)
        {
            if (v == null)
                return null;
            Rigidbody rb = null;
            try { rb = v.rb; }
            catch { rb = null; }
            if (rb == null)
            {
                try { rb = v.hitRB; }
                catch { rb = null; }
            }
            return rb;
        }

        private static void ReleaseAi()
        {
            if (!_heldAi)
                return;
            _heldAi = false;
            Ship ship = _host as Ship;
            if (ship != null)
            {
                try { ship.SetHoldPosition(true); }
                catch { }
                try
                {
                    ShipInputs inp = ship.GetInputs();
                    if (inp != null)
                    {
                        inp.throttle = 0f;
                        inp.steering = 0f;
                    }
                }
                catch { }
                return;
            }
            GroundVehicle v = Vehicle;
            if (v == null)
                return;
            try { v.StopImmediately(); }
            catch { }
            try { v.SetHoldPosition(true); }
            catch { }
        }

        private static int SeatFromApproach(Unit u)
        {
            FillSeats(u);
            Vector3 local = u.transform.InverseTransformPoint(GaMode.EyePos);
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < SeatCount; i++)
            {
                Vector3 d = _seats[i] - local;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < bestD)
                {
                    bestD = sq;
                    best = i;
                }
            }
            return best;
        }

        private static void FillSeats(Unit u)
        {
            if (u == null)
                return;
            float hx = 0.75f;
            float hz = 0.7f;
            float hy = 1.15f;
            Collider[] cols = u.GetComponentsInChildren<Collider>();
            if (cols != null && cols.Length > 0)
            {
                bool have = false;
                Bounds wb = new Bounds(u.transform.position, Vector3.zero);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] == null || !cols[i].enabled)
                        continue;
                    if (!have)
                    {
                        wb = cols[i].bounds;
                        have = true;
                    }
                    else
                        wb.Encapsulate(cols[i].bounds);
                }
                if (have)
                {
                    Vector3 min = u.transform.InverseTransformPoint(wb.min);
                    Vector3 max = u.transform.InverseTransformPoint(wb.max);
                    float ax = Mathf.Abs(max.x - min.x) * 0.5f;
                    float ay = Mathf.Abs(max.y - min.y) * 0.5f;
                    float az = Mathf.Abs(max.z - min.z) * 0.5f;
                    hx = Mathf.Clamp(ax * 0.42f, 0.45f, 1.35f);
                    hz = Mathf.Clamp(az * 0.28f, 0.4f, 1.8f);
                    hy = Mathf.Clamp(ay * 0.55f, 0.85f, 1.85f);
                }
            }
            _seats[0] = new Vector3(-hx, hy, hz);
            _seats[1] = new Vector3(hx, hy, hz);
            _seats[2] = new Vector3(-hx, hy, -hz);
            _seats[3] = new Vector3(hx, hy, -hz);
        }

        private static Unit Nearest()
        {
            return Nearest(null);
        }

        private static Unit Nearest(Unit skip)
        {
            List<Unit> all = Rideables(false);
            if (all == null || all.Count == 0)
                return null;
            FactionHQ hq = GaPerfFrameGate.Hq();
            Vector3 at = GaMode.EyePos;
            Vector3 look = LookFlat();
            Unit best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i];
                if (u == null || u.disabled)
                    continue;
                if (skip != null && object.ReferenceEquals(u, skip))
                    continue;
                try
                {
                    if (hq != null && u.NetworkHQ != null && !object.ReferenceEquals(u.NetworkHQ, hq))
                        continue;
                }
                catch { }
                float lim = 256f;
                if (u is Ship)
                    lim = 900f;
                else if (u is Building)
                    lim = 36f;
                float d = DistSq(u, at);
                if (d > lim)
                    continue;
                Vector3 c = u.transform.position;
                float cx = c.x - at.x;
                float cz = c.z - at.z;
                float origin = cx * cx + cz * cz;
                if (u is Building && origin > 64f)
                    continue;
                float originLim = 625f;
                if (u is Ship)
                    originLim = 3600f;
                if (origin > originLim)
                    continue;
                Vector3 to = new Vector3(cx, 0f, cz);
                float facing = 0f;
                if (to.sqrMagnitude > 0.04f)
                    facing = Vector3.Dot(look, to.normalized);
                if (_skipHost != null && object.ReferenceEquals(u, _skipHost)
                    && Time.unscaledTime < _skipUntil && facing < 0.55f)
                    continue;
                float score = Mathf.Sqrt(d) - facing * 18f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = u;
                }
            }
            return best;
        }

        private static Vector3 LookFlat()
        {
            Vector3 look = GaMode.EyeFwd;
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f)
                return Vector3.forward;
            return look.normalized;
        }

        private static bool LookingAt(Unit u)
        {
            if (u == null)
                return false;
            Vector3 to = u.transform.position - GaMode.EyePos;
            to.y = 0f;
            if (to.sqrMagnitude < 0.04f)
                return true;
            return Vector3.Dot(LookFlat(), to.normalized) > 0.42f;
        }

        private static List<Unit> Rideables(bool scanAll)
        {
            if (!scanAll && Time.unscaledTime < _nextRide)
                return _ride;
            _nextRide = Time.unscaledTime + 0.25f;
            _ride.Clear();
            List<Unit> scan = null;
            try { scan = UnitRegistry.allUnits; }
            catch { scan = null; }
            if (scan == null)
                return _ride;
            for (int i = 0; i < scan.Count; i++)
            {
                Unit u = scan[i];
                if (!IsRideable(u))
                    continue;
                _ride.Add(u);
            }
            return _ride;
        }

        private static bool IsRideable(Unit u)
        {
            if (u == null || u.disabled)
                return false;
            if (u is VehicleDepot)
                return false;
            if (u is Aircraft || u is Missile || u is PilotDismounted)
                return false;
            if (u is Ship)
            {
                if (GaForce.Kind != GaForceKind.Sea)
                    return false;
                return HasGuns(u);
            }
            if (!(u is GroundVehicle) && !(u is Building))
                return false;
            return HasGuns(u);
        }

        private static bool HasGuns(Unit u)
        {
            List<WeaponStation> st = u.weaponStations;
            if (st == null || st.Count == 0)
                return false;
            for (int i = 0; i < st.Count; i++)
            {
                WeaponStation s = st[i];
                if (s == null)
                    continue;
                if (s.Turrets != null && s.Turrets.Count > 0)
                    return true;
                if (s.Weapons != null && s.Weapons.Count > 0)
                    return true;
            }
            return false;
        }

        private static bool CanHelm(Unit u)
        {
            if (u is Ship)
                return true;
            return CanDrive(u as GroundVehicle);
        }

        private static bool CanDrive(GroundVehicle v)
        {
            if (v == null)
                return false;
            if (_mobile == null)
                _mobile = AccessTools.Field(typeof(GroundVehicle), "mobile");
            if (_mobile != null)
            {
                try
                {
                    object o = _mobile.GetValue(v);
                    if (o is bool && !(bool)o)
                        return false;
                }
                catch { }
            }
            return true;
        }

        private static float DistSq(Unit u, Vector3 at)
        {
            float best = (u.transform.position - at).sqrMagnitude;
            try
            {
                Collider col = u.GetComponent<Collider>();
                if (col != null)
                {
                    Vector3 c = col.ClosestPoint(at);
                    float d = (c - at).sqrMagnitude;
                    if (d < best)
                        best = d;
                }
            }
            catch { }
            try
            {
                Transform cv = u.cockpitViewPoint;
                if (cv != null)
                {
                    float d = (cv.position - at).sqrMagnitude;
                    if (d < best)
                        best = d;
                }
            }
            catch { }
            List<WeaponStation> st = u.weaponStations;
            if (st != null)
            {
                int n = st.Count;
                if (n > 4)
                    n = 4;
                for (int i = 0; i < n; i++)
                {
                    WeaponStation s = st[i];
                    if (s == null || s.Turrets == null || s.Turrets.Count == 0)
                        continue;
                    Turret t = s.Turrets[0];
                    if (t == null)
                        continue;
                    float d = (t.transform.position - at).sqrMagnitude;
                    if (d < best)
                        best = d;
                }
            }
            return best;
        }

        private static string UnitName(Unit u)
        {
            if (u == null)
                return "UNIT";
            try
            {
                if (u.definition != null && !string.IsNullOrEmpty(u.definition.unitName))
                    return u.definition.unitName;
            }
            catch { }
            return u.name;
        }

        private static string WepName(WeaponStation st)
        {
            if (st == null)
                return "GUN";
            try
            {
                if (st.WeaponInfo != null)
                {
                    if (!string.IsNullOrEmpty(st.WeaponInfo.shortName))
                        return st.WeaponInfo.shortName;
                    if (!string.IsNullOrEmpty(st.WeaponInfo.weaponName))
                        return st.WeaponInfo.weaponName;
                }
            }
            catch { }
            return "GUN " + (_wep + 1).ToString();
        }

        private static bool IsMissile(WeaponStation st)
        {
            if (st == null)
                return false;
            try
            {
                if (st.WeaponInfo != null && st.WeaponInfo.missile)
                    return true;
            }
            catch { }
            return false;
        }

        private static FieldInfo JobFields()
        {
            if (_jobFields == null)
                _jobFields = AccessTools.Field(typeof(GroundVehicle), "JobFields");
            return _jobFields;
        }

        private static Unit TurretUnit(Turret turret)
        {
            if (turret == null)
                return null;
            if (_turretUnit == null)
                _turretUnit = AccessTools.Field(typeof(Turret), "attachedUnit");
            if (_turretUnit != null)
            {
                try
                {
                    Unit u = _turretUnit.GetValue(turret) as Unit;
                    if (u != null)
                        return u;
                }
                catch { }
            }
            return turret.GetComponentInParent<Unit>();
        }

        private static MethodInfo AimTurretFn()
        {
            if (_aimTurret == null)
                _aimTurret = AccessTools.Method(typeof(Turret), "AimTurret", new[] { typeof(Vector3) });
            return _aimTurret;
        }
    }
}
