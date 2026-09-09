using System;
using System.Reflection;
using CandideServer.Entities;
using CandideServer.Entities.Controllers;
using CandideServer.Saving;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Shared.Entity;

namespace BetterCarts.Patches;

// Diagnostics only. Prepare returns false when the master switch is off, so these patches are never installed.
internal static class DiagnosticsPatch {
    private static bool Armed() {
        return ModConfig.Diagnostics != null && ModConfig.Diagnostics.Value;
    }

    private static MethodBase FirstMethod(Type owner, params string[] names) {
        foreach (string name in names) {
            MethodBase found = AccessTools.Method(owner, name);
            if (found != null) {
                return found;
            }
        }
        return null;
    }

    [HarmonyPatch(typeof(GameSaveManager), nameof(GameSaveManager.SaveGameState))]
    private static class Snapshot {
        private static bool Prepare() { return Armed(); }

        private static void Prefix(bool forOffThreadSerializing) {
            ModLog.Watch("SaveGameState.Prefix",
                () => ModDiagnostics.NoteSnapshot(forOffThreadSerializing, starting: true));
        }

        private static void Postfix(bool forOffThreadSerializing) {
            ModLog.Watch("SaveGameState.Postfix",
                () => ModDiagnostics.NoteSnapshot(forOffThreadSerializing, starting: false));
        }

        private static Exception Finalizer(Exception __exception) {
            if (__exception != null) {
                // never Watch here either: anything thrown out of a finalizer REPLACES the exception being reported and destroys the evidence
                try {
                    ModDiagnostics.NoteSnapshotFailed(__exception);
                }
                catch {
                }
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(GameSaveManager), nameof(GameSaveManager.SaveGameToDirectory))]
    private static class Serialize {
        private static bool Prepare() { return Armed(); }

        private static void Prefix() {
            ModLog.Watch("SaveGameToDirectory.Prefix", ModDiagnostics.NoteSerializeStart);
        }

        private static void Postfix() {
            ModLog.Watch("SaveGameToDirectory.Postfix", ModDiagnostics.NoteSerializeEnd);
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
        private static bool Prepare() { return Armed(); }

        private static void Prefix(EntityWrapper entity, string key) {
            ModLog.Watch("UpdateEntityParameter.Prefix", () => ModDiagnostics.NoteParameterWrite(entity, key));
        }
    }

    [HarmonyPatch]
    private static class EntityCreated {
        private static MethodBase _target;

        private static bool Prepare() {
            if (!Armed()) {
                return false;
            }
            _target = _target ?? FirstMethod(typeof(EntitySystem), "NewEntity");
            if (_target == null) {
                ModLog.Error("DIAG entity-creation counter disabled: EntitySystem.NewEntity not found");
            }
            return _target != null;
        }

        private static MethodBase TargetMethod() { return _target; }

        private static void Postfix() {
            ModLog.Watch("NewEntity.Postfix", ModDiagnostics.NoteEntityCreated);
        }
    }

    [HarmonyPatch]
    private static class EntityRemoved {
        private static MethodBase _target;

        private static bool Prepare() {
            if (!Armed()) {
                return false;
            }
            _target = _target ?? FirstMethod(typeof(EntitySystem), "RemoveEntity", "DestroyEntity", "FreeEntity", "Remove");
            if (_target == null) {
                ModLog.Error("DIAG entity-removal counter disabled: no removal method found on EntitySystem");
            }
            return _target != null;
        }

        private static MethodBase TargetMethod() { return _target; }

        private static void Postfix() {
            ModLog.Watch("RemoveEntity.Postfix", ModDiagnostics.NoteEntityRemoved);
        }
    }

    [HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.Update), typeof(GameTime))]
    private static class Census {
        private static bool Prepare() { return Armed(); }

        private static void Postfix() {
            ModLog.Watch("Diagnostics.Census", ModDiagnostics.Tick);
        }
    }
}
