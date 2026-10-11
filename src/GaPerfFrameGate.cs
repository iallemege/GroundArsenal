using System.Collections.Generic;
using UnityEngine;

namespace GroundArsenal
{
    internal static class GaPerfFrameGate
    {
        private static int _begunFrame = -1;
        private static int _recoverUntil = -1;
        private static float _lastDtMs;
        private static bool _softSkip;
        private static Camera _cam;
        private static int _camFrame = -1;
        private static FactionHQ _hq;
        private static int _hqFrame = -1;
        private static readonly List<Unit> _plot = new List<Unit>(128);
        private static float _nextPlot;
        private static Airbase[] _airbases;
        private static float _nextAirbases;
        private static Hangar[] _hangars;
        private static float _nextHangars;

        internal static Hangar[] Hangars()
        {
            if (Time.unscaledTime < _nextHangars && _hangars != null)
                return _hangars;
            _nextHangars = Time.unscaledTime + 2.2f;
            try { _hangars = UnityEngine.Object.FindObjectsOfType<Hangar>(); }
            catch { _hangars = _hangars ?? new Hangar[0]; }
            return _hangars;
        }

        internal static bool Paint()
        {
            Event e = Event.current;
            return e == null || e.type == EventType.Repaint;
        }

        internal const float SoftSkipMs = 50f;
        internal const float HitchRecoverMs = 80f;
        internal static float LastDtMs { get { return _lastDtMs; } }
        internal static bool Recovering { get { return Time.frameCount <= _recoverUntil; } }
        internal static bool SoftSkipping { get { return _softSkip; } }

        internal static void BeginFrame()
        {
            int f = Time.frameCount;
            if (_begunFrame == f)
                return;
            _begunFrame = f;
            _lastDtMs = Time.unscaledDeltaTime * 1000f;
            _softSkip = _lastDtMs >= SoftSkipMs;
            if (_lastDtMs >= HitchRecoverMs)
                _recoverUntil = f + 2;
        }

        internal static bool SkipPolish()
        {
            BeginFrame();
            return Recovering || _softSkip;
        }

        internal static Camera Cam()
        {
            int f = Time.frameCount;
            if (f == _camFrame)
                return _cam;
            _camFrame = f;
            _cam = Camera.main;
            return _cam;
        }

        internal static FactionHQ Hq()
        {
            int f = Time.frameCount;
            if (f == _hqFrame)
                return _hq;
            _hqFrame = f;
            _hq = null;
            try { GameManager.GetLocalHQ(out _hq); }
            catch { _hq = null; }
            return _hq;
        }

        internal static List<Unit> PlotUnits()
        {
            if (Time.unscaledTime < _nextPlot)
                return _plot;
            _nextPlot = Time.unscaledTime + 0.2f;
            _plot.Clear();
            List<Unit> scan = null;
            try { scan = UnitRegistry.allUnits; }
            catch { scan = null; }
            if (scan == null)
                return _plot;
            for (int i = 0; i < scan.Count; i++)
            {
                Unit u = scan[i];
                if (u == null || u.disabled)
                    continue;
                if (!(u is Aircraft) && !(u is GroundVehicle) && !(u is Ship))
                    continue;
                _plot.Add(u);
            }
            return _plot;
        }

        internal static Airbase[] Airbases()
        {
            if (Time.unscaledTime < _nextAirbases && _airbases != null)
                return _airbases;
            _nextAirbases = Time.unscaledTime + 2.2f;
            try { _airbases = UnityEngine.Object.FindObjectsOfType<Airbase>(); }
            catch { _airbases = _airbases ?? new Airbase[0]; }
            return _airbases;
        }
    }
}
