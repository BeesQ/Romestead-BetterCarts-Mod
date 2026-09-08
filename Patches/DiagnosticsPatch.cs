using System;
using System.Diagnostics;
using CandideServer.Entities;
using CandideServer.Entities.Controllers;
using CandideServer.Saving;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Shared.Entity;

namespace BetterCarts.Patches;

// diagnostics only: this file changes no behaviour and every body is a no-op while Logging is off
internal static class DiagnosticsPatch {
    [HarmonyPatch(typeof(GameSaveManager), nameof(GameSaveManager.SaveGameState))]
    private static class Snapshot {
        private static void Prefix(bool forOffThreadSerializing) {
            ModLog.Guard("SaveGameState.Prefix",
                () => ModDiagnostics.NoteSnapshot(forOffThreadSerializing, starting: true));
        }

        private static void Postfix(bool forOffThreadSerializing) {
            ModLog.Guard("SaveGameState.Postfix",
                () => ModDiagnostics.NoteSnapshot(forOffThreadSerializing, starting: false));
        }
    }

    // the serialize runs inside this method before the file write is handed to a task, so prefix to postfix brackets the window that matters
    [HarmonyPatch(typeof(GameSaveManager), nameof(GameSaveManager.SaveGameToDirectory))]
    private static class Serialize {
        private static void Prefix(out long __state) {
            __state = Stopwatch.GetTimestamp();
            ModLog.Guard("SaveGameToDirectory.Prefix", ModDiagnostics.NoteSerializeStart);
        }

        private static void Postfix(long __state) {
            long ticks = Stopwatch.GetTimestamp() - __state;
            long ms = ticks * 1000L / Stopwatch.Frequency;
            ModLog.Guard("SaveGameToDirectory.Postfix", () => ModDiagnostics.NoteSerializeEnd(ms));
        }
    }

    [HarmonyPatch(typeof(ServerEntitySystemManager), nameof(ServerEntitySystemManager.UpdateEntityParameter))]
    private static class ParameterWrite {
        private static void Prefix(EntityWrapper entity, string key) {
            ModLog.Guard("UpdateEntityParameter.Prefix", () => ModDiagnostics.NoteParameterWrite(entity, key));
        }
    }

    [HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.Update), typeof(GameTime))]
    private static class Census {
        private static void Postfix() {
            ModLog.Guard("Diagnostics.Census", ModDiagnostics.Tick);
        }
    }
}
