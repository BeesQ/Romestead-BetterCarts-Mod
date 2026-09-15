using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx.Configuration;
using CandideServer.Entities;
using CandideServer.Entities.Controllers;
using CandideServer.Saving;
using Shared.Entity;

namespace BetterCarts;

internal static class ModDiagnostics {
    private const long CensusIntervalMs = 30000;
    private const long HeartbeatIntervalMs = 1000;
    private const int MaxDistinctKeysPerSave = 32;
    private const int MaxSerializerErrors = 20;

    private static readonly string[] SlotKeys = { "c1", "c2", "c3", "c4", "c5" };

    private static bool _environmentLogged;
    private static long _nextCensusTick;
    private static long _nextHeartbeatTick;
    private static bool _serializerErrorsUnavailable;

    private static volatile bool _serializing;
    private static int _serializeThread;
    private static long _serializeStartTimestamp;
    private static int _writesDuringSave;
    private static int _insertsDuringSave;
    private static int _entitiesCreatedDuringSave;
    private static int _entitiesRemovedDuringSave;
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
        if (_serializing && ModLog.SaveEnabled && now >= _nextHeartbeatTick) {
            _nextHeartbeatTick = now + HeartbeatIntervalMs;
            ModLog.SaveWarn("SAVE alive ms=" + ElapsedMs()
                + " writes=" + Volatile.Read(ref _writesDuringSave)
                + " inserts=" + Volatile.Read(ref _insertsDuringSave)
                + " created=" + Volatile.Read(ref _entitiesCreatedDuringSave)
                + " removed=" + Volatile.Read(ref _entitiesRemovedDuringSave)
                + " serializerThread=" + _serializeThread);
        }
        if (!ModLog.CensusEnabled || now < _nextCensusTick) {
            return;
        }
        _nextCensusTick = now + CensusIntervalMs;
        LogCensus();
    }

    private static void LogEnvironment() {
        ModLog.Error("> BetterCarts Diagnostics are ON <");
        ModLog.Error("=== ENVIRONMENT ===");
        AssemblyName self = typeof(ModDiagnostics).Assembly.GetName();
        ModLog.Error("mod=" + self.Name + " " + self.Version + " runtime=" + Environment.Version
            + " os=" + Environment.OSVersion.VersionString + " cores=" + Environment.ProcessorCount);
        LogNeighbourAssemblies();
        ModLog.Error("--- settings ---");
        foreach (ConfigEntryBase entry in ModConfig.Bound) {
            if (entry == null) {
                continue;
            }
            ModLog.Error("  [" + entry.Definition.Section + "] " + entry.Definition.Key + " = " + entry.BoxedValue);
        }
        ModLog.Error("=== END ENVIRONMENT ===");
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
        ModLog.Error("assemblies=" + total + " related=[" + (builder.Length == 0 ? "none" : builder.ToString()) + "]");
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
            if (carts == 0) {
                continue;
            }
            worlds++;
            builder.Append(" | world entities=").Append(system.EntityIdMap.Count)
                .Append('/').Append(system.MaxEntities)
                // HighestActive is a high-water index, not a live count, and reading it as a count produced active > entities
                .Append(" highWater=").Append(system.HighestActive)
                .Append(" carts=").Append(carts)
                .Append(" extras=").Append(extras)
                .Append(" longestCargo=").Append(longest);
        }
        if (worlds == 0) {
            return;
        }
        ModLog.Census(builder.ToString());
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

    // ---- save window ---------------------------------------------------

    internal static void NoteSnapshot(bool offThread, bool starting) {
        ModLog.Save("SAVE snapshot " + (starting ? "start" : "end") + " offThread=" + offThread);
    }

    internal static void NoteSnapshotFailed(Exception ex) {
        ModLog.Error("SAVE snapshot FAILED - the exception below escaped a vanilla save path");
        ModLog.Error(ex.ToString());
    }

    internal static void NoteSerializeStart() {
        _serializeThread = Environment.CurrentManagedThreadId;
        Interlocked.Exchange(ref _serializeStartTimestamp, Stopwatch.GetTimestamp());
        Interlocked.Exchange(ref _writesDuringSave, 0);
        Interlocked.Exchange(ref _insertsDuringSave, 0);
        Interlocked.Exchange(ref _entitiesCreatedDuringSave, 0);
        Interlocked.Exchange(ref _entitiesRemovedDuringSave, 0);
        lock (KeysSeenThisSave) {
            KeysSeenThisSave.Clear();
        }
        _nextHeartbeatTick = Environment.TickCount64 + HeartbeatIntervalMs;
        _serializing = true;
        ModWatchdog.NoteSaveStart();
        ModLog.Save("SAVE serialize start");
    }

    internal static void NoteSerializeEnd() {
        long ms = ElapsedMs();
        _serializing = false;
        ModLog.Save("SAVE serialize end ms=" + ms
            + " paramWrites=" + Volatile.Read(ref _writesDuringSave)
            + " ofWhichInserts=" + Volatile.Read(ref _insertsDuringSave)
            + " entitiesCreated=" + Volatile.Read(ref _entitiesCreatedDuringSave)
            + " entitiesRemoved=" + Volatile.Read(ref _entitiesRemovedDuringSave));
        ModWatchdog.NoteSaveEnd();
        LogSerializerErrors();
    }

    // a postfix never runs when an exception escapes the original, which is why the fatal saves logged nothing at all
    internal static void NoteSerializeFailed(Exception ex) {
        long ms = ElapsedMs();
        _serializing = false;
        ModWatchdog.NoteSaveEnd();
        ModLog.Error("SAVE serialize FAILED after ms=" + ms
            + " paramWrites=" + Volatile.Read(ref _writesDuringSave)
            + " inserts=" + Volatile.Read(ref _insertsDuringSave)
            + " created=" + Volatile.Read(ref _entitiesCreatedDuringSave)
            + " removed=" + Volatile.Read(ref _entitiesRemovedDuringSave)
            + " serializerThread=" + _serializeThread);
        ModLog.Error(ex.ToString());
        for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException) {
            ModLog.Error("SAVE inner exception: " + inner);
        }
    }

    // the serializer COLLECTS errors instead of throwing, so a save can fail, say so in chat and still
    // return normally - which is why an observed "Error while saving world" left no trace in the log at all
    private static void LogSerializerErrors() {
        if (_serializerErrorsUnavailable) {
            return;
        }
        try {
            object serializer = ReadMember(typeof(GameSaveManager), null, "GameStateSaveSerializer");
            if (serializer == null) {
                _serializerErrorsUnavailable = true;
                ModLog.Save("SAVE serializer errors unavailable - GameStateSaveSerializer was not readable");
                return;
            }
            if (!(ReadMember(serializer.GetType(), serializer, "Errored") is bool errored)) {
                _serializerErrorsUnavailable = true;
                ModLog.Save("SAVE serializer errors unavailable - Errored was not readable");
                return;
            }
            if (!errored) {
                return;
            }
            ModLog.Error("SAVE serializer REPORTED ERRORS - the save did not throw, so this block is the only record");
            IEnumerable errors = ReadMember(serializer.GetType(), serializer, "Errors") as IEnumerable;
            if (errors == null) {
                ModLog.Error("  the Errors list was not readable");
                return;
            }
            int shown = 0;
            foreach (object error in errors) {
                shown++;
                if (shown > MaxSerializerErrors) {
                    ModLog.Error("  further errors suppressed");
                    return;
                }
                ModLog.Error("  error " + shown + ": " + error);
            }
            if (shown == 0) {
                ModLog.Error("  Errored was true but the Errors list was empty");
            }
        }
        catch (Exception ex) {
            _serializerErrorsUnavailable = true;
            ModLog.Fault("ModDiagnostics.LogSerializerErrors", ex);
        }
    }

    private static object ReadMember(Type owner, object instance, string name) {
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | (instance == null ? BindingFlags.Static : BindingFlags.Instance);
        PropertyInfo property = owner.GetProperty(name, flags);
        if (property != null && property.CanRead) {
            return property.GetValue(instance);
        }
        FieldInfo field = owner.GetField(name, flags);
        return field == null ? null : field.GetValue(instance);
    }

    internal static void NoteEntityCreated() {
        if (_serializing) {
            Interlocked.Increment(ref _entitiesCreatedDuringSave);
        }
    }

    internal static void NoteEntityRemoved() {
        if (_serializing) {
            Interlocked.Increment(ref _entitiesRemovedDuringSave);
        }
    }

    // ---- parameter writes ----------------------------------------------

    internal static void NoteParameterWrite(EntityWrapper entity, string key) {
        if (key == null) {
            return;
        }
        bool insert = false;
        var parameters = entity == null || entity.Controller == null ? null : entity.Controller.Parameters;
        if (parameters != null && parameters.Dictionary != null) {
            insert = !parameters.Dictionary.ContainsKey(key);
        }
        RouteByKey(entity, key, insert);
        if (!_serializing || !ModLog.SaveEnabled) {
            return;
        }
        Interlocked.Increment(ref _writesDuringSave);
        if (insert) {
            Interlocked.Increment(ref _insertsDuringSave);
        }
        string tracked = key + (insert ? " insert" : string.Empty);
        lock (KeysSeenThisSave) {
            if (KeysSeenThisSave.Count >= MaxDistinctKeysPerSave || !KeysSeenThisSave.Add(tracked)) {
                return;
            }
        }
        ModLog.SaveWarn("SAVE param write key=" + key + " insert=" + insert
            + " ms=" + ElapsedMs() + " serializerThread=" + _serializeThread);
    }

    private static void RouteByKey(EntityWrapper entity, string key, bool insert) {
        if (!(entity != null && entity.Controller is ServerCart2Controller)) {
            return;
        }
        if (key == "following") {
            ModLog.Chain("CHAIN follow link changed insert=" + insert);
            return;
        }
        if (key == CartCargoSync.CargoKey) {
            ModLog.Capacity("CAPACITY extra cargo written insert=" + insert);
            return;
        }
        for (int i = 0; i < SlotKeys.Length; i++) {
            if (key == SlotKeys[i]) {
                ModLog.Pickup("PICKUP slot " + key + " changed insert=" + insert);
                return;
            }
        }
    }
}
