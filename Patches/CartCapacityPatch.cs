using System;
using CandideServer.Entities.Controllers;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Shared.Entity;

namespace BetterCarts.Patches;

internal static class CartCapacityPatch {
    [HarmonyPatch(typeof(ServerCart2Controller), "PickupEntity")]
    private static class Capacity {
        private static bool Prepare() { return ModConfig.LoadCartCapacity.Value; }

        // Priority.First keeps this ahead of Iron Cart's false-returning prefix; it must return true unless it deliberately blocks
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ServerCart2Controller __instance, ref bool __result, out bool __state) {
            try {
                __state = false;
                if (!CartCapacity.TryGetEnforcedCapacity(__instance.Entity, out int capacity)) {
                    return true;
                }
                int occupied = CartCargo.GetOccupied(__instance);
                if (occupied < capacity) {
                    return true;
                }
                __state = true;
                __result = false;
                if (ModLog.AdvancedEnabled) {
                    ModLog.AdvancedOnChange("block:" + __instance.Entity.Id,
                        "BLOCK cart=" + __instance.Entity.Id + " occupied=" + occupied + " >= cap=" + capacity);
                }
                return false;
            }
            catch (Exception ex) {
                ModLog.Fault("CartCapacityPatch.Capacity.Prefix", ex);
                throw;
            }
        }

        // Priority.First puts the extend ahead of ChainOverflowPatch, so a Cart fills its own configured slots before it spills
        [HarmonyPriority(Priority.First)]
        private static void Postfix(ServerCart2Controller __instance, EntityWrapper entity, ref bool __result, bool __state) {
            try {
                if (__result) {
                    CartCargo.Invalidate(__instance);
                    return;
                }
                if (__state) {
                    return;
                }
                if (!CartCapacity.TryGetEnforcedCapacity(__instance.Entity, out int capacity)) {
                    return;
                }
                if (entity == null || entity.Removed || entity.CarrierId.HasValue) {
                    return;
                }
                if (!CartCargo.CanTakeExtra(__instance)) {
                    return;
                }
                if (CartCargo.GetOccupied(__instance) >= capacity) {
                    return;
                }
                if (ModLog.AdvancedEnabled) {
                    ModLog.Advanced("EXTEND cart=" + __instance.Entity.Id + " taking " + entity.Id + " (cap=" + capacity
                        + " occupied=" + CartCargo.GetOccupied(__instance) + ")");
                }
                CartCargo.PinExtra(__instance, entity);
                __result = true;
            }
            catch (Exception ex) {
                ModLog.Fault("CartCapacityPatch.Capacity.Postfix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.Update), typeof(GameTime))]
    private static class Extras {
        private static bool Prepare() { return ModConfig.LoadCartCapacity.Value; }

        private static void Postfix(ServerCart2Controller __instance) {
            try {
                CartCargo.Tick(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartCapacityPatch.Extras.Postfix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.OnRemove),
       typeof(EntityRemoveInfo))]
    private static class ReleaseOnRemove {
        private static bool Prepare() { return ModConfig.LoadCartCapacity.Value; }

        private static void Postfix(ServerCart2Controller __instance,
            EntityRemoveInfo entityRemoveInfo) {
            try {
                EntityRemoveType reason = entityRemoveInfo.RemoveType;
                bool clearStoredCargo = reason != EntityRemoveType.RemoveFromSimulation
                    && reason != EntityRemoveType.Unloaded
                    && reason != EntityRemoveType.ChangingWorld;

                CartCargo.ReleaseAll(__instance, clearStoredCargo);
            }
            catch (Exception ex) {
                ModLog.Fault("CartCapacityPatch.ReleaseOnRemove.Postfix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.EntityInitialize))]
    private static class Flags {
        private static bool Prepare() { return ModConfig.LoadCartCapacity.Value; }

        private static void Postfix() {
            try {
                CartCapacity.NoteWorldLoaded("server");
            }
            catch (Exception ex) {
                ModLog.Fault("CartCapacityPatch.Flags.Postfix", ex);
                throw;
            }
        }
    }
}
