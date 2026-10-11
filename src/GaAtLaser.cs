using UnityEngine;

namespace GroundArsenal
{
    /// <summary>Anti-tank HOTENSIA laser: X visor, Q a target inside 10 km, player rounds home for 60s.</summary>
    internal static class GaAtLaser
    {
        private const float Range = 10000f;
        private const float HomeSec = 60f;
        private const float CoolSec = 120f;

        private static float _homeUntil;
        private static float _coolUntil;
        private static Vector3 _aim;
        private static Unit _tgt;
        private static bool _have;
        internal static bool TookE;

        internal static bool Homing
        {
            get { return _have && Time.unscaledTime < _homeUntil; }
        }

        internal static bool BlocksQ
        {
            get
            {
                if (GaClass.Picked != GaKitId.AntiTank)
                    return false;
                return Time.unscaledTime < _coolUntil;
            }
        }

        internal static Vector3 Aim
        {
            get { return _aim; }
        }

        internal static Unit Tracked
        {
            get { return _tgt; }
        }

        internal static void Clear()
        {
            _homeUntil = 0f;
            _coolUntil = 0f;
            _tgt = null;
            _have = false;
        }

        internal static void TickCancel()
        {
            TookE = false;
            if (!GaMode.Active || GaMode.Dead || GaMode.Downed)
                return;
            if (GaHud.BlocksLook || GaRide.Busy || GaRadio.Open)
                return;
            if (GaClass.Picked != GaKitId.AntiTank)
                return;
            if (!_have && Time.unscaledTime >= _coolUntil)
                return;
            if (GaRifleDrop.Grabbed)
                return;
            if (!Input.GetKeyDown(KeyCode.E) && !GaVr.GrabDown)
                return;
            CancelMark();
            TookE = true;
            GaVr.GrabDown = false;
        }

        internal static void CancelMark()
        {
            _have = false;
            _homeUntil = 0f;
            _coolUntil = 0f;
            _tgt = null;
            GaFps.ClearMark();
            GaMode.Deny("LASER OFF", "激光标记已取消");
            GaRadio.Say(GaRadio.PlayerCall(), "Laser off. Mark cancelled.", false, 8);
        }

        internal static void Tick()
        {
            if (!_have)
                return;
            if (Time.unscaledTime >= _coolUntil)
            {
                _have = false;
                _tgt = null;
                return;
            }
            if (_tgt != null)
            {
                try
                {
                    if (_tgt.disabled)
                        _tgt = null;
                    else
                        _aim = _tgt.GetCenterOfMass();
                }
                catch { _tgt = null; }
            }
            if (Homing)
                GaFps.RefreshMark(_aim);
        }

        internal static string Hint()
        {
            if (GaClass.Picked != GaKitId.AntiTank)
                return null;
            float now = Time.unscaledTime;
            if (now < _homeUntil)
            {
                int s = Mathf.CeilToInt(_homeUntil - now);
                return "LASER  " + s.ToString() + "s  E cancel";
            }
            if (now < _coolUntil)
            {
                int s = Mathf.CeilToInt(_coolUntil - now);
                return "LASER CD  " + s.ToString() + "s  E cancel";
            }
            if (GaHotensia.Live)
                return "Q  designate 10km  (laser)";
            return null;
        }

        internal static bool TryDesignate()
        {
            if (GaClass.Picked != GaKitId.AntiTank)
                return false;
            if (!GaHotensia.Live)
                return false;
            if (BlocksQ)
            {
                GaMode.Deny("DESIGNATOR LOCKED", "指示器锁定中");
                return true;
            }
            Transform eye = GaMode.Eye;
            if (eye == null)
                return true;
            RaycastHit hit;
            if (!Physics.Raycast(eye.position, eye.forward, out hit, Range,
                GaPhys.WorldMask, QueryTriggerInteraction.Ignore))
            {
                GaMode.Deny("NO TARGET", "10公里内没有目标");
                return true;
            }
            if (hit.distance > Range)
            {
                GaMode.Deny("OUT OF RANGE", "超出10公里");
                return true;
            }
            _aim = hit.point;
            _tgt = null;
            try { _tgt = hit.collider.GetComponentInParent<Unit>(); }
            catch { _tgt = null; }
            if (_tgt != null)
            {
                try
                {
                    if (_tgt.disabled)
                        _tgt = null;
                    else
                        _aim = _tgt.GetCenterOfMass();
                }
                catch { _tgt = null; }
            }
            _have = true;
            _homeUntil = Time.unscaledTime + HomeSec;
            _coolUntil = Time.unscaledTime + CoolSec;
            GaDropBot bot = GaPhys.Bot(hit.collider);
            string label = bot != null ? bot.Callsign : "LASER";
            bool enemy = bot == null || !bot.Friendly;
            if (_tgt != null)
                enemy = true;
            GaFps.PlaceMark(_aim, enemy, label);
            GaMode.Deny("LASER ON  60s", "激光指示 60秒");
            GaRadio.Say(GaRadio.PlayerCall(), "Laser on. All rounds to the mark.", false, 8);
            return true;
        }
    }
}
