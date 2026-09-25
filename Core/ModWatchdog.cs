using System;
using System.Globalization;
using System.Reflection;
using System.Runtime;
using System.Threading;
using CandideServer.Saving;
using HarmonyLib;

namespace BetterCarts;

internal static class ModWatchdog {
    private const int SampleMs = 250;
    private const long WakeupSlackMs = 500;
    private const long PumpStallMs = 2000;
    private const long IdleIntervalMs = 30000;
    private const long SaveIntervalMs = 1000;
    private const long LohStepBytes = 256L * 1024L * 1024L;

    private static readonly object StartLock = new object();

    private static bool _started;
    private static long _lastPumpMs;
    private static bool _pumpStallOpen;
    private static long _nextLineMs;
    private static long _lastReportedLoh;
    private static int _lastReportedGen2;

    private static volatile bool _saving;
    private static long _saveAllocated;
    private static long _saveLohStart;
    private static long _saveLohPeak;
    private static long _saveCommittedPeak;
    private static int _saveGen0;
    private static int _saveGen1;
    private static int _saveGen2;
    private static long _saveMaxPumpGap;
    private static long _saveMaxSuspend;

    internal static void NotePump() {
        Volatile.Write(ref _lastPumpMs, Environment.TickCount64);
        if (!_started) {
            Start();
        }
    }

    internal static void NoteSaveStart() {
        if (!ModLog.MemoryEnabled) {
            return;
        }
        Interlocked.Exchange(ref _saveAllocated, GC.GetTotalAllocatedBytes(false));
        long loh = LargeObjectHeapBytes();
        Interlocked.Exchange(ref _saveLohStart, loh);
        Interlocked.Exchange(ref _saveLohPeak, loh);
        Interlocked.Exchange(ref _saveCommittedPeak, GC.GetGCMemoryInfo().TotalCommittedBytes);
        Interlocked.Exchange(ref _saveMaxPumpGap, 0);
        Interlocked.Exchange(ref _saveMaxSuspend, 0);
        _saveGen0 = GC.CollectionCount(0);
        _saveGen1 = GC.CollectionCount(1);
        _saveGen2 = GC.CollectionCount(2);
        _saving = true;
    }

    internal static void NoteSaveEnd() {
        if (!_saving) {
            return;
        }
        _saving = false;
        if (!ModLog.MemoryEnabled) {
            return;
        }
        long allocated = GC.GetTotalAllocatedBytes(false) - Interlocked.Read(ref _saveAllocated);
        ModLog.MemoryWarn("WATCHDOG save cost allocatedMB=" + Mb(allocated)
            + " lohStartMB=" + Mb(Interlocked.Read(ref _saveLohStart))
            + " lohPeakMB=" + Mb(Interlocked.Read(ref _saveLohPeak))
            + " lohEndMB=" + Mb(LargeObjectHeapBytes())
            + " committedPeakMB=" + Mb(Interlocked.Read(ref _saveCommittedPeak))
            + " gen0=" + (GC.CollectionCount(0) - _saveGen0)
            + " gen1=" + (GC.CollectionCount(1) - _saveGen1)
            + " gen2=" + (GC.CollectionCount(2) - _saveGen2)
            + " maxPumpGapMs=" + Interlocked.Read(ref _saveMaxPumpGap)
            + " maxSuspendMs=" + Interlocked.Read(ref _saveMaxSuspend));
    }

    private static void Start() {
        lock (StartLock) {
            if (_started) {
                return;
            }
            _started = true;
        }
        try {
            if (ModLog.MemoryEnabled) {
                ModLog.Memory("WATCHDOG start sampleMs=" + SampleMs
                    + " serverGC=" + GCSettings.IsServerGC
                    + " latency=" + GCSettings.LatencyMode
                    + " heapMB=" + Mb(GC.GetTotalMemory(false))
                    + " workingSetMB=" + Mb(Environment.WorkingSet));
            }
            Thread thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Name = "BetterCarts Watchdog";
            thread.Start();
        }
        catch (Exception ex) {
            ModLog.Fault("ModWatchdog.Start", ex);
        }
    }

    // this thread exists to be measured, not to do work: the gap between its OWN wakeups is the only
    // reading that separates "every managed thread was stopped" from "one thread is blocked alone"
    private static void Loop() {
        long previous = Environment.TickCount64;
        while (true) {
            Thread.Sleep(SampleMs);
            long now = Environment.TickCount64;
            long wakeupGap = now - previous;
            previous = now;
            if (!ModLog.MemoryEnabled) {
                continue;
            }
            try {
                Sample(now, wakeupGap - SampleMs);
            }
            catch (Exception ex) {
                ModLog.Fault("ModWatchdog.Sample", ex);
                return;
            }
        }
    }

    private static void Sample(long now, long overrun) {
        bool line = false;

        if (overrun >= WakeupSlackMs) {
            ModLog.MemoryWarn("WATCHDOG process suspended for " + overrun
                + " ms - every managed thread was stopped, not just the server");
            Max(ref _saveMaxSuspend, overrun);
            line = true;
        }

        long pumpGap = now - Volatile.Read(ref _lastPumpMs);
        Max(ref _saveMaxPumpGap, pumpGap);
        if (pumpGap >= PumpStallMs) {
            if (!_pumpStallOpen) {
                _pumpStallOpen = true;
                ModLog.MemoryWarn("WATCHDOG server pump has not run for " + pumpGap
                    + " ms while this thread is still awake - the server thread alone is blocked");
                line = true;
            }
        }
        else if (_pumpStallOpen) {
            _pumpStallOpen = false;
            ModLog.MemoryWarn("WATCHDOG server pump recovered, it was blocked for about " + pumpGap + " ms");
            line = true;
        }

        long loh = LargeObjectHeapBytes();
        if (_saving) {
            Max(ref _saveLohPeak, loh);
            Max(ref _saveCommittedPeak, GC.GetGCMemoryInfo().TotalCommittedBytes);
        }

        int gen2 = GC.CollectionCount(2);
        if (gen2 != _lastReportedGen2 || Math.Abs(loh - _lastReportedLoh) >= LohStepBytes) {
            line = true;
        }
        if (now >= _nextLineMs) {
            line = true;
        }
        if (!line) {
            return;
        }

        _lastReportedLoh = loh;
        _lastReportedGen2 = gen2;
        _nextLineMs = now + (_saving ? SaveIntervalMs : IdleIntervalMs);
        GCMemoryInfo info = GC.GetGCMemoryInfo();
        ModLog.MemoryWarn("WATCHDOG " + (_saving ? "saving" : "idle")
            + " heapMB=" + Mb(GC.GetTotalMemory(false))
            + " lohMB=" + Mb(loh)
            + " committedMB=" + Mb(info.TotalCommittedBytes)
            + " allocatedMB=" + Mb(GC.GetTotalAllocatedBytes(false))
            + " gc0=" + GC.CollectionCount(0)
            + " gc1=" + GC.CollectionCount(1)
            + " gc2=" + gen2
            + " pausePct=" + info.PauseTimePercentage.ToString("0.0", CultureInfo.InvariantCulture)
            + " pumpGapMs=" + pumpGap);
    }

    // index 3 of GenerationInfo is the large object heap, which is where every save buffer lands; the
    // span is empty until the first collection and cannot be cached in a field because it is a ref struct
    private static long LargeObjectHeapBytes() {
        ReadOnlySpan<GCGenerationInfo> generations = GC.GetGCMemoryInfo().GenerationInfo;
        return generations.Length <= 3 ? 0L : generations[3].SizeAfterBytes;
    }

    private static void Max(ref long target, long candidate) {
        long seen = Interlocked.Read(ref target);
        while (candidate > seen) {
            long swapped = Interlocked.CompareExchange(ref target, candidate, seen);
            if (swapped == seen) {
                return;
            }
            seen = swapped;
        }
    }

    private static string Mb(long bytes) {
        return (bytes / (1024L * 1024L)).ToString(CultureInfo.InvariantCulture);
    }

    [HarmonyPatch]
    private static class Pump {
        private static MethodBase _target;
        private static readonly Action Body = NotePump;

        private static bool Prepare() {
            if (!ModConfig.DiagnosticsArmed) {
                return false;
            }
            // Memory Watch is read HERE as well as at every write site, because this prefix runs on the server tick
            // thread: with the box off the patch is never installed and the watchdog is not in the running game at all
            if (ModConfig.DiagMemory == null || !ModConfig.DiagMemory.Value) {
                return false;
            }
            if (_target == null) {
                _target = AccessTools.Method(typeof(GameSaveManager), "Update");
            }
            if (_target == null) {
                ModLog.Error("WATCHDOG could not resolve GameSaveManager.Update - the watchdog will not run");
                return false;
            }
            return true;
        }

        private static MethodBase TargetMethod() {
            return _target;
        }

        // the delegate is cached in a static field because this prefix runs on every server tick and a
        // fresh closure per tick would be a real allocation cost inside the thing being measured
        private static void Prefix() {
            ModLog.Watch("GameSaveManager.Update.Prefix", Body);
        }
    }
}
