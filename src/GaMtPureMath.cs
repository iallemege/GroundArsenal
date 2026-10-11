using System;
using System.Threading;

namespace GroundArsenal
{
    internal static class GaMtPureMath
    {
        internal const int ParallelThreshold = 16;
        internal static void FillScores(int count, float[] scoreOut, Action<int, int> scoreRange)
        {
            if (count <= 0 || scoreOut == null || scoreRange == null) return;
            if (count < ParallelThreshold || Environment.ProcessorCount <= 1) { scoreRange(0, count); return; }
            int workers = Environment.ProcessorCount;
            if (workers > 4) workers = 4;
            int chunk = count / workers;
            if (chunk < 8) { scoreRange(0, count); return; }
            int remaining = workers;
            object gate = new object();
            for (int w = 0; w < workers; w++)
            {
                int start = w * chunk;
                int end = (w == workers - 1) ? count : start + chunk;
                ThreadPool.QueueUserWorkItem(state =>
                {
                    try { int[] range = (int[])state; scoreRange(range[0], range[1]); }
                    finally { lock (gate) { remaining--; Monitor.PulseAll(gate); } }
                }, new int[] { start, end });
            }
            lock (gate) { while (remaining > 0) Monitor.Wait(gate, 50); }
        }
    }
}