using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace BetterCarts;

internal static class ModLog {
    private const string Tag = "[BetterCarts] ";
    private const int MaxTrackedKeys = 512;

    private static ManualLogSource _log;
    private static readonly Dictionary<string, string> LastSeen = new Dictionary<string, string>();
    private static readonly Queue<string> TrackedOrder = new Queue<string>();
    private static readonly HashSet<string> ReportedFaults = new HashSet<string>();

    internal static void Init(ManualLogSource log) {
        _log = log;
    }

    internal static bool Enabled {
        get { return _log != null && ModConfig.Logging != null && ModConfig.Logging.Value; }
    }

    internal static bool AdvancedEnabled {
        get {
            return Enabled && ModConfig.AdvancedLogging != null && ModConfig.AdvancedLogging.Value;
        }
    }

    internal static void Info(string message) {
        if (Enabled) {
            _log.LogInfo(Prefix() + message);
        }
    }

    internal static void Advanced(string message) {
        if (AdvancedEnabled) {
            _log.LogInfo(Prefix() + message);
        }
    }

    internal static void Warn(string message) {
        if (Enabled) {
            _log.LogWarning(Prefix() + message);
        }
    }

    // per-tick paths call this; the line is only emitted when its content actually changes
    internal static void OnChange(string key, string message) {
        Emit(Enabled, key, message);
    }

    internal static void AdvancedOnChange(string key, string message) {
        Emit(AdvancedEnabled, key, message);
    }

    // runs the body and reports the first exception from each site with its stack, then rethrows so a real break still surfaces as a break
    internal static void Guard(string site, Action body) {
        try {
            body();
        }
        catch (Exception ex) {
            Fault(site, ex);
            throw;
        }
    }

    internal static void Fault(string site, Exception ex) {
        if (_log == null) {
            return;
        }
        lock (ReportedFaults) {
            if (!ReportedFaults.Add(site)) {
                return;
            }
        }
        _log.LogError(Prefix() + "FAULT in " + site + " - this is a Better Carts bug, please report it with this block");
        _log.LogError(Prefix() + ex);
    }

    internal static void Reset(string key) {
        lock (LastSeen) {
            LastSeen.Remove(key);
        }
    }

    private static string Prefix() {
        return Tag + "[t" + Environment.CurrentManagedThreadId + "] ";
    }

    private static void Emit(bool enabled, string key, string message) {
        if (!enabled) {
            return;
        }
        lock (LastSeen) {
            if (LastSeen.TryGetValue(key, out string previous) && previous == message) {
                return;
            }
            if (!LastSeen.ContainsKey(key)) {
                TrackedOrder.Enqueue(key);
                while (TrackedOrder.Count > MaxTrackedKeys) {
                    LastSeen.Remove(TrackedOrder.Dequeue());
                }
            }
            LastSeen[key] = message;
        }
        _log.LogInfo(Prefix() + message);
    }
}
