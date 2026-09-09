using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace BetterCarts;

internal static class ModLog {
    private const string Tag = "[BetterCarts] ";
    private const string FileName = "BetterCarts.log";
    private const int MaxTrackedKeys = 512;

    private static ManualLogSource _log;
    private static StreamWriter _file;
    private static bool _fileAttempted;
    private static readonly object FileLock = new object();
    private static readonly Dictionary<string, string> LastSeen = new Dictionary<string, string>();
    private static readonly Queue<string> TrackedOrder = new Queue<string>();
    private static readonly HashSet<string> ReportedFaults = new HashSet<string>();

    internal static void Init(ManualLogSource log) {
        _log = log;
    }

    internal static bool Enabled {
        get { return _log != null && ModConfig.Diagnostics != null && ModConfig.Diagnostics.Value; }
    }

    private static bool Channel(ConfigEntry<bool> entry) {
        return Enabled && entry != null && entry.Value;
    }

    internal static bool SaveEnabled { get { return Channel(ModConfig.DiagSave); } }
    internal static bool CensusEnabled { get { return Channel(ModConfig.DiagCensus); } }
    internal static bool CapacityEnabled { get { return Channel(ModConfig.DiagCapacity); } }
    internal static bool PickupEnabled { get { return Channel(ModConfig.DiagPickup); } }
    internal static bool ChainEnabled { get { return Channel(ModConfig.DiagChain); } }
    internal static bool StateDumpEnabled { get { return Channel(ModConfig.DiagStateDump); } }
    internal static bool AdvancedEnabled { get { return StateDumpEnabled; } }

    internal static void Save(string message) { Emit(SaveEnabled, false, message); }
    internal static void SaveWarn(string message) { Emit(SaveEnabled, true, message); }
    internal static void Census(string message) { Emit(CensusEnabled, false, message); }
    internal static void Capacity(string message) { Emit(CapacityEnabled, false, message); }
    internal static void Pickup(string message) { Emit(PickupEnabled, false, message); }
    internal static void Chain(string message) { Emit(ChainEnabled, false, message); }
    internal static void StateDump(string message) { Emit(StateDumpEnabled, false, message); }

    internal static void CapacityOnChange(string key, string message) { EmitOnChange(CapacityEnabled, key, message); }
    internal static void PickupOnChange(string key, string message) { EmitOnChange(PickupEnabled, key, message); }
    internal static void ChainOnChange(string key, string message) { EmitOnChange(ChainEnabled, key, message); }
    internal static void StateDumpOnChange(string key, string message) { EmitOnChange(StateDumpEnabled, key, message); }

    internal static void Info(string message) { Capacity(message); }
    internal static void Warn(string message) { Emit(CapacityEnabled, true, message); }
    internal static void Advanced(string message) { StateDump(message); }
    internal static void OnChange(string key, string message) { CapacityOnChange(key, message); }
    internal static void AdvancedOnChange(string key, string message) { StateDumpOnChange(key, message); }

    internal static void Error(string message) {
        if (_log != null) {
            _log.LogError(Prefix() + message);
        }
        WriteFile("ERROR " + message);
    }

    internal static void Guard(string site, Action body) {
        try {
            body();
        }
        catch (Exception ex) {
            Fault(site, ex);
            throw;
        }
    }

    internal static void Watch(string site, Action body) {
        try {
            body();
        }
        catch (Exception ex) {
            Fault(site, ex);
        }
    }

    internal static void Fault(string site, Exception ex) {
        lock (ReportedFaults) {
            if (!ReportedFaults.Add(site)) {
                return;
            }
        }
        string header = "FAULT in " + site + " - this is a Better Carts bug, please report it with this block";
        if (_log != null) {
            _log.LogError(Prefix() + header);
            _log.LogError(Prefix() + ex);
        }
        WriteFile("ERROR " + header);
        WriteFile("ERROR " + ex);
    }

    internal static void Reset(string key) {
        lock (LastSeen) {
            LastSeen.Remove(key);
        }
    }

    private static string Prefix() {
        return Tag + "[t" + Environment.CurrentManagedThreadId + "] ";
    }

    private static void Emit(bool enabled, bool warning, string message) {
        if (!enabled) {
            return;
        }
        if (warning) {
            _log.LogWarning(Prefix() + message);
        }
        else {
            _log.LogInfo(Prefix() + message);
        }
        WriteFile(message);
    }

    private static void EmitOnChange(bool enabled, string key, string message) {
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
        WriteFile(message);
    }

    // opened on first write rather than at Init, because Init runs before the config is bound and an eager open silently gave up forever
    private static StreamWriter EnsureFile() {
        lock (FileLock) {
            if (_file != null) {
                return _file;
            }
            if (_fileAttempted || ModConfig.Diagnostics == null || !ModConfig.Diagnostics.Value) {
                return null;
            }
            _fileAttempted = true;
            try {
                string folder = Path.GetDirectoryName(typeof(ModLog).Assembly.Location);
                if (string.IsNullOrEmpty(folder)) {
                    if (_log != null) {
                        _log.LogWarning(Prefix() + "could not locate the mod folder, writing to the BepInEx log only");
                    }
                    return null;
                }
                _file = new StreamWriter(Path.Combine(folder, FileName), false, new UTF8Encoding(false));
                _file.AutoFlush = true;
                AssemblyName self = typeof(ModLog).Assembly.GetName();
                _file.WriteLine("Better Carts " + self.Version + " diagnostic log, opened "
                    + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                if (_log != null) {
                    _log.LogInfo(Prefix() + "diagnostic log: " + Path.Combine(folder, FileName));
                }
            }
            catch (Exception ex) {
                _file = null;
                if (_log != null) {
                    _log.LogWarning(Prefix() + "could not open " + FileName + ": " + ex.Message);
                }
            }
            return _file;
        }
    }

    private static void WriteFile(string message) {
        StreamWriter file = _file ?? EnsureFile();
        if (file == null) {
            return;
        }
        try {
            lock (FileLock) {
                file.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " [t"
                    + Environment.CurrentManagedThreadId + "] " + message);
            }
        }
        catch {
            _file = null;
        }
    }
}
