using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Rewired;
using UnityEngine;
using UnityEngine.UI;

namespace GroundArsenal
{
    /// <summary>
    /// Vanilla CombatHUD IFF boxes and Select-button lock, hosted on a borrowed
    /// friendly aircraft so HUDUnitMarker can construct without NRE.
    /// </summary>
    internal static class GaIff
    {
        private static CombatHUD _hud;
        private static Aircraft _borrow;
        private static List<Unit> _locks;
        private static float _nextFill;
        private static MethodInfo _updateMarkers;
        private static FieldInfo _targetList;
        private static FieldInfo _currentStation;
        private static FieldInfo _markers;
        private static bool _on;

        internal static Unit Primary()
        {
            if (_locks != null && _locks.Count > 0 && _locks[0] != null && !_locks[0].disabled)
                return _locks[0];
            return null;
        }

        internal static void Start(Unit host)
        {
            Stop();
            _hud = Hud();
            if (_hud == null)
                return;
            _borrow = PickBorrow();
            if (_borrow == null)
                return;
            try { _hud.aircraft = _borrow; }
            catch { return; }
            _locks = new List<Unit>(8);
            Bind();
            try { TargetList().SetValue(_hud, _locks); }
            catch { }
            EnableLayer(true);
            FillMarkers(host);
            _on = true;
            _nextFill = Time.unscaledTime + 0.8f;
        }

        internal static void Stop()
        {
            _on = false;
            if (_hud != null)
            {
                try { _hud.ClearIcons(); }
                catch { }
                try
                {
                    if (object.ReferenceEquals(_hud.aircraft, _borrow))
                        _hud.aircraft = null;
                }
                catch { }
            }
            _hud = null;
            _borrow = null;
            _locks = null;
        }

        internal static void HudLate(CombatHUD hud)
        {
            if (!_on || hud == null || !GaRide.Busy)
                return;
            if (_borrow == null || _borrow.disabled)
            {
                _borrow = PickBorrow();
                if (_borrow == null)
                    return;
                try { hud.aircraft = _borrow; }
                catch { return; }
            }
            if (Time.unscaledTime >= _nextFill)
            {
                _nextFill = Time.unscaledTime + 0.8f;
                FillMarkers(GaRide.Host);
            }
            PushStation(hud);
            MethodInfo um = UpdateMarkersFn();
            if (um != null)
            {
                try { um.Invoke(hud, null); }
                catch { }
            }
            PollSelect(hud);
        }

        private static void PollSelect(CombatHUD hud)
        {
            if (GaHud.BlocksLook)
                return;
            Rewired.Player inp = null;
            try { inp = GameManager.playerInput; }
            catch { inp = null; }
            if (inp == null)
                return;
            try
            {
                if (inp.GetButtonTimedPressUp("Deselect", 0f, PlayerSettings.clickDelay)
                    || inp.GetButtonDown("Deselect"))
                {
                    DropLast();
                    return;
                }
            }
            catch { }
            try
            {
                if (inp.GetButtonTimedPressUp("Select", 0f, PlayerSettings.clickDelay))
                    TrySelect(hud, false);
                else if (inp.GetButtonTimedPressDown("Select", PlayerSettings.pressDelay)
                    && !DynamicMap.mapMaximized)
                    TrySelect(hud, true);
            }
            catch { }
        }

        private static void TrySelect(CombatHUD hud, bool paint)
        {
            if (hud == null || hud.targetDesignator == null)
                return;
            List<HUDUnitMarker> marks = Markers(hud);
            if (marks == null || marks.Count == 0)
                return;
            Vector3 des = hud.targetDesignator.transform.position;
            List<HUDUnitMarker> hit = new List<HUDUnitMarker>(4);
            for (int i = 0; i < marks.Count; i++)
            {
                HUDUnitMarker m = marks[i];
                if (m == null || m.image == null || !m.image.enabled || m.unit == null)
                    continue;
                if (m.unit.disabled)
                    continue;
                if (!paint && m.selected)
                    continue;
                try
                {
                    TargetListSelector sel = SceneSingleton<TargetListSelector>.i;
                    if (sel != null && sel.CheckExclusions(m.unit))
                        continue;
                }
                catch { }
                if (!FastMath.InRange(des, m.image.transform.position, 100f))
                    continue;
                hit.Add(m);
            }
            if (hit.Count == 0)
                return;
            if (paint)
            {
                for (int i = 0; i < hit.Count; i++)
                    LockMark(hud, hit[i]);
                return;
            }
            HUDUnitMarker best = hit[0];
            float bestD = float.MaxValue;
            for (int i = 0; i < hit.Count; i++)
            {
                HUDUnitMarker m = hit[i];
                float d = (m.image.transform.position - des).sqrMagnitude;
                bool enemy = IsEnemy(m.unit);
                if (enemy)
                    d *= 0.15f;
                if (d < bestD)
                {
                    bestD = d;
                    best = m;
                }
            }
            LockMark(hud, best);
        }

        internal static void DropLast()
        {
            if (_locks == null || _locks.Count == 0 || _hud == null)
                return;
            Unit u = _locks[0];
            _locks.RemoveAt(0);
            HUDUnitMarker mark;
            try
            {
                if (u != null && _hud.TryGetMarker(u, out mark) && mark != null)
                    mark.DeselectMarker();
            }
            catch { }
        }

        internal static void DropAll()
        {
            if (_locks == null || _hud == null)
                return;
            for (int i = 0; i < _locks.Count; i++)
            {
                Unit u = _locks[i];
                HUDUnitMarker mark;
                try
                {
                    if (u != null && _hud.TryGetMarker(u, out mark) && mark != null)
                        mark.DeselectMarker();
                }
                catch { }
            }
            _locks.Clear();
        }

        internal static void UnlockUnit(Unit unit)
        {
            if (unit == null || _locks == null || _hud == null)
                return;
            _locks.Remove(unit);
            HUDUnitMarker mark;
            try
            {
                if (_hud.TryGetMarker(unit, out mark) && mark != null)
                    mark.DeselectMarker();
            }
            catch { }
        }

        internal static bool LockUnit(Unit unit)
        {
            if (unit == null || _hud == null || _locks == null)
                return false;
            HUDUnitMarker mark;
            try
            {
                if (!_hud.TryGetMarker(unit, out mark) || mark == null)
                    return false;
            }
            catch { return false; }
            LockMark(_hud, mark);
            return true;
        }

        private static void LockMark(CombatHUD hud, HUDUnitMarker mark)
        {
            if (mark == null || mark.unit == null)
                return;
            if (_locks == null)
                return;
            if (_locks.Contains(mark.unit))
                return;
            _locks.Insert(0, mark.unit);
            try { mark.SelectMarker(); }
            catch { }
        }

        private static void FillMarkers(Unit host)
        {
            CombatHUD hud = _hud;
            if (hud == null || _borrow == null)
                return;
            FactionHQ hq = null;
            try { hq = _borrow.NetworkHQ; }
            catch { hq = null; }
            if (hq == null)
                hq = GaPerfFrameGate.Hq();
            if (hq == null)
                return;
            try
            {
                var faction = hq.factionUnits;
                if (faction != null)
                {
                    for (int i = 0; i < faction.Count; i++)
                        SafeCreate(hud, faction[i], host);
                }
            }
            catch { }
            try
            {
                Dictionary<PersistentID, TrackingInfo> db = hq.trackingDatabase;
                if (db != null)
                {
                    foreach (KeyValuePair<PersistentID, TrackingInfo> kv in db)
                        SafeCreate(hud, kv.Key, host);
                }
            }
            catch { }
        }

        private static void SafeCreate(CombatHUD hud, PersistentID id, Unit host)
        {
            Unit u = null;
            try
            {
                if (!UnitRegistry.TryGetUnit(id, out u) || u == null)
                    return;
            }
            catch { return; }
            if (u.disabled || u is Scenery || u is Missile)
                return;
            if (host != null && object.ReferenceEquals(u, host))
                return;
            try { hud.CreateMarker(id); }
            catch { }
        }

        private static void PushStation(CombatHUD hud)
        {
            FieldInfo fi = CurrentStationFn();
            if (fi == null || hud == null)
                return;
            try { fi.SetValue(hud, GaRide.CurrentStation()); }
            catch { }
        }

        private static void EnableLayer(bool on)
        {
            if (_hud == null)
                return;
            try
            {
                if (_hud.iconLayer != null)
                    _hud.iconLayer.gameObject.SetActive(on);
            }
            catch { }
            try
            {
                if (_hud.targetDesignator != null)
                {
                    _hud.targetDesignator.gameObject.SetActive(on);
                    _hud.targetDesignator.enabled = on;
                }
            }
            catch { }
        }

        private static Aircraft PickBorrow()
        {
            Aircraft parked = GaMode.ParkedAircraft;
            if (parked != null && !parked.disabled)
                return parked;
            FactionHQ hq = GaPerfFrameGate.Hq();
            List<Unit> all = GaPerfFrameGate.PlotUnits();
            if (all == null)
                return null;
            Aircraft best = null;
            float bestD = float.MaxValue;
            Vector3 at = GaMode.EyePos;
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i] as Aircraft;
                if (ac == null || ac.disabled)
                    continue;
                try
                {
                    if (hq != null && ac.NetworkHQ != null
                        && !object.ReferenceEquals(ac.NetworkHQ, hq))
                        continue;
                }
                catch { }
                float d = (ac.transform.position - at).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = ac;
                }
            }
            return best;
        }

        private static bool IsEnemy(Unit u)
        {
            if (u == null)
                return false;
            FactionHQ hq = GaPerfFrameGate.Hq();
            if (hq == null)
                return true;
            try
            {
                FactionHQ other = u.NetworkHQ;
                if (other == null)
                    return true;
                return !object.ReferenceEquals(other, hq);
            }
            catch
            {
                return true;
            }
        }

        private static CombatHUD Hud()
        {
            try { return SceneSingleton<CombatHUD>.i; }
            catch { return null; }
        }

        private static void Bind()
        {
            if (_updateMarkers == null)
                _updateMarkers = AccessTools.Method(typeof(CombatHUD), "UpdateMarkers");
            if (_targetList == null)
                _targetList = AccessTools.Field(typeof(CombatHUD), "targetList");
            if (_currentStation == null)
                _currentStation = AccessTools.Field(typeof(CombatHUD), "currentWeaponStation");
            if (_markers == null)
                _markers = AccessTools.Field(typeof(CombatHUD), "markers");
        }

        private static MethodInfo UpdateMarkersFn()
        {
            Bind();
            return _updateMarkers;
        }

        private static FieldInfo TargetList()
        {
            Bind();
            return _targetList;
        }

        private static FieldInfo CurrentStationFn()
        {
            Bind();
            return _currentStation;
        }

        private static List<HUDUnitMarker> Markers(CombatHUD hud)
        {
            Bind();
            if (_markers == null || hud == null)
                return null;
            try { return _markers.GetValue(hud) as List<HUDUnitMarker>; }
            catch { return null; }
        }
    }
}
