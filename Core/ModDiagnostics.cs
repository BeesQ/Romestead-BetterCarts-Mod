using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx.Configuration;
using CandideServer.Entities;
using CandideServer.Entities.Controllers;
using Shared.Entity;

namespace BetterCarts;

internal static class ModDiagnostics {
    private const long CensusIntervalMs = 30000;
    private const long HeartbeatIntervalMs = 1000;
    private const int MaxDistinctKeysPerSave = 32;

    private static bool _environmentLogged;
    private static long _nextCensusTick;
    private static long _nextHeartbeatTick;

    private static volatile bool _serializing;
    private static int _serializeThread;
    private static long _serializeStartTimestamp;
    private static int _writesDuringSave;
    private static int _insertsDuringSave;
    private static int _entitiesCreatedDuringSave;
    private static readonly HashSet<string> KeysSeenThisSave = new HashSet<string>();

    internal static void Tick() {
        if (!ModLog.Enabled) {
            return;
        }
        if (!_environmentLogged) {
            _environmentLogged = true;
            LogEnvironment();
        }
        long now = Environment.TickCount64;
        // the tick thread is the only thing that can emit this, so its ABSENCE during a save window is itself the finding
        if (_serializing && now >= _nextHeartbeatTick) {
            _nextHeartbeatTick = now + HeartbeatIntervalMs;
            ModLog.Warn("SAVE alive ms=" + ElapsedMs()
                + " writes=" + Volatile.Read(ref _writesDuringSave)
                + " inserts=" + Volatile.Read(ref _insertsDuringSave)
                + " created=" + Volatile.Read(ref _entitiesCreatedDuringSave)
                + " serializerThread=" + _serializeThread);
        }
        if (now < _nextCensusTick) {
            return;
        }
        _nextCensusTick = now + CensusIntervalMs;
        LogCensus();
    }

    private static void LogEnvironment() {
        ModLog.Info("=== ENVIRONMENT ===");
        AssemblyName self = typeof(ModDiagnostics).Assembly.GetName();
        ModLog.Info("mod=" + self.Name + " " + self.Version + " runtime=" + Environment.Version
            + " os=" + Environment.OSVersion.VersionString + " cores=" + Environment.ProcessorCount);
        LogNeighbourAssemblies();
        ModLog.Info("--- settings ---");
        foreach (ConfigEntryBase entry in ModConfig.Bound) {
            if (entry == null) {
                continue;
            }
            ModLog.Info("  [" + entry.Definition.Section + "] " + entry.Definition.Key + " = " + entry.BoxedValue);
        }
        ModLog.Info("=== END ENVIRONMENT ===");
    }

    private static void LogNeighbourAssemblies() {
        StringBuilder builder = new StringBuilder();
        int total = 0;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            if (assembly == null || assembly.IsDynamic) {
                continue;
            }
            total++;
            string name = assembly.GetName().Name;
            if (name == null) {
                continue;
            }
            if (name.IndexOf("cart", StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("ModSettings", StringComparison.OrdinalIgnoreCase) < 0) {
                continue;
            }
            if (builder.Length > 0) {
                builder.Append(", ");
            }
            builder.Append(name).Append(' ').Append(assembly.GetName().Version);
        }
        ModLog.Info("assemblies=" + total + " related=[" + (builder.Length == 0 ? "none" : builder.ToString()) + "]");
    }

    private static void LogCensus() {
        StringBuilder builder = new StringBuilder();
        builder.Append("CENSUS");
        int worlds = 0;
        foreach (var pair in ServerEntitySystemManager.WorldIdToSystemMap) {
            EntitySystem system = pair.Value == null ? null : pair.Value.System;
            if (system == null) {
                continue;
            }
            worlds++;
            int carts = 0;
            int extras = 0;
            int longest = 0;
            foreach (EntityWrapper entity in system.GetEntityWrappers()) {
                if (entity == null || entity.Removed) {
                    continue;
                }
                if (!(entity.Controller is ServerCart2Controller cart)) {
                    continue;
                }
                carts++;
                var parameters = cart.Parameters;
                if (parameters == null) {
                    continue;
                }
                string packed = parameters.GetString(CartCargoSync.CargoKey, string.Empty);
                if (string.IsNullOrEmpty(packed)) {
                    continue;
                }
                extras += CountEntries(packed);
                if (packed.Length > longest) {
                    longest = packed.Length;
                }
            }
            builder.Append(" | world entities=").Append(system.EntityIdMap.Count)
                .Append('/').Append(system.MaxEntities)
                .Append(" highWater=").Append(system.HighestActive)
                .Append(" carts=").Append(carts)
                .Append(" extras=").Append(extras)
                .Append(" longestCargo=").Append(longest);
        }
        if (worlds == 0) {
            return;
        }
        ModLog.Info(builder.ToString());
    }

    private static int CountEntries(string packed) {
        int count = 1;
        for (int i = 0; i < packed.Length; i++) {
            if (packed[i] == ',') {
                count++;
            }
        }
        return count;
    }

    private static long ElapsedMs() {
        long start = Interlocked.Read(ref _serializeStartTimestamp);
        if (start == 0) {
            return 0;
        }
        return (Stopwatch.GetTimestamp() - start) * 1000L / Stopwatch.Frequency;
    }

    internal static void NoteSnapshot(bool offThread, bool starting) {
        if (!ModLog.Enabled) {
            return;
        }
        ModLog.Info("SAVE snapshot " + (starting ? "start" : "end") + " offThread=" + offThread);
    }

    internal static void NoteSnapshotFailed(Exception ex) {
        ModLog.Error("SAVE snapshot FAILED - the exception below escaped the snapshot, which is a vanilla save path");
        ModLog.Error(ex.ToString());
    }

    internal static void NoteSerializeStart() {
        _serializeThread = Environment.CurrentManagedThreadId;
        Interlocked.Exchange(ref _serializeStartTimestamp, Stopwatch.GetTimestamp());
        Interlocked.Exchange(ref _writesDuringSave, 0);
        Interlocked.Exchange(ref _insertsDuringSave, 0);
        Interlocked.Exchange(ref _entitiesCreatedDuringSave, 0);
        lock (KeysSeenThisSave) {
            KeysSeenThisSave.Clear();
        }
        _nextHeartbeatTick = Environment.TickCount64 + HeartbeatIntervalMs;
        _serializing = true;
        if (ModLog.Enabled) {
            ModLog.Info("SAVE serialize start");
        }
    }

    internal static void NoteSerializeEnd() {
        long ms = ElapsedMs();
        _serializing = false;
        if (!ModLog.Enabled) {
            return;
        }
        int writes = Volatile.Read(ref _writesDuringSave);
        int inserts = Volatile.Read(ref _insertsDuringSave);
        int created = Volatile.Read(ref _entitiesCreatedDuringSave);
        ModLog.Info("SAVE serialize end ms=" + ms + " paramWritesDuringSave=" + writes
            + " ofWhichInserts=" + inserts + " entitiesCreated=" + created);
        if (writes > 0) {
            ModLog.Warn("SAVE " + writes + " entity parameter writes landed while the save was serializing"
                + " (serializer thread " + _serializeThread + ")");
        }
    }

    // a postfix never runs when an exception escapes the original, which is why the fatal saves logged nothing at all
    internal static void NoteSerializeFailed(Exception ex) {
        long ms = ElapsedMs();
        _serializing = false;
        ModLog.Error("SAVE serialize FAILED after ms=" + ms
            + " paramWrites=" + Volatile.Read(ref _writesDuringSave)
            + " inserts=" + Volatile.Read(ref _insertsDuringSave)
            + " created=" + Volatile.Read(ref _entitiesCreatedDuringSave)
            + " serializerThread=" + _serializeThread);
        ModLog.Error(ex.ToString());
        for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException) {
            ModLog.Error("SAVE inner exception: " + inner);
        }
    }

    internal static void NoteEntityCreated() {
        if (_serializing) {
            Interlocked.Increment(ref _entitiesCreatedDuringSave);
        }
    }

    internal static void NoteParameterWrite(EntityWrapper entity, string key) {
        if (!_serializing || !ModLog.Enabled) {
            return;
        }
        Interlocked.Increment(ref _writesDuringSave);
        bool insert = false;
        if (entity != null && entity.Controller != null) {
            var parameters = entity.Controller.Parameters;
            if (parameters != null && parameters.Dictionary != null) {
                insert = !parameters.Dictionary.ContainsKey(key);
            }
        }
        if (insert) {
            Interlocked.Increment(ref _insertsDuringSave);
        }
        string tracked = key + (insert ? " insert" : string.Empty);
        lock (KeysSeenThisSave) {
            if (KeysSeenThisSave.Count >= MaxDistinctKeysPerSave || !KeysSeenThisSave.Add(tracked)) {
                return;
            }
        }
        ModLog.Warn("PARAM WRITE DURING SAVE key=" + key + " insert=" + insert
            + " ms=" + ElapsedMs() + " serializerThread=" + _serializeThread);
    }
}
