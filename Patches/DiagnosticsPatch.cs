using System;
using System.Reflection;
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

        private static Exception Finalizer(Exception __exception) {
            if (__exception != null) {
                try {
                    ModDiagnostics.NoteSnapshotFailed(__exception);
                }
                catch {
                }
            }
            return __exception;
        }
    }

    // the serialize runs inside this method before the file write is handed to a task, so prefix to postfix brackets the window that matters
    [HarmonyPatch(typeof(GameSaveManager), nameof(GameSaveManager.SaveGameToDirectory))]
    private static class Serialize {
        private static void Prefix() {
            ModLog.Guard("SaveGameToDirectory.Prefix", ModDiagnostics.NoteSerializeStart);
        }

        private static void Postfix() {
            ModLog.Guard("SaveGameToDirectory.Postfix", ModDiagnostics.NoteSerializeEnd);
        }

        private static Exception Finalizer(Exception __exception) {
            if (__exception != null) {
                try {
                    ModDiagnostics.NoteSerializeFailed(__exception);
                }
                catch {
                }
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ServerEntitySystemManager), nameof(ServerEntitySystemManager.UpdateEntityParameter))]
    private static class ParameterWrite {
        private static void Prefix(EntityWrapper entity, string key) {
            ModLog.Guard("UpdateEntityParameter.Prefix", () => ModDiagnostics.NoteParameterWrite(entity, key));
        }
    }

    [HarmonyPatch]
    private static class EntityCreated {
        private static MethodBase _target;

        private static bool Prepare() {
            if (_target == null) {
                _target = AccessTools.Method(typeof(EntitySystem), "NewEntity");
            }
            if (_target == null) {
                ModLog.Warn("DIAG entity-creation counter disabled: EntitySystem.NewEntity not found");
            }
            return _target != null;
        }

        private static MethodBase TargetMethod() {
            return _target;
        }

        private static void Postfix() {
            ModLog.Guard("NewEntity.Postfix", ModDiagnostics.NoteEntityCreated);
        }
    }

    [HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.Update), typeof(GameTime))]
    private static class Census {
        private static void Postfix() {
            ModLog.Guard("Diagnostics.Census", ModDiagnostics.Tick);
        }
    }
}
