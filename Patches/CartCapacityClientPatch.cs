using System;
using Candide.Entities.Controllers.Other;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Shared.Entity;

namespace BetterCarts.Patches;

// client-side only: it renders what the server already decided. A joining player without Better Carts installed still gets the host's capacity, but sees extra cargo lying on the ground instead of on the Cart
internal static class CartCapacityClientPatch {
    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.OnServerSetState))]
    private static class Sync {
        private static bool Prepare() { return ModConfig.LoadCartCapacity.Value; }

        private static void Postfix(Cart2Controller __instance) {
            try {
                CartCargoClient.SyncSlots(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartCapacityClientPatch.Sync.Postfix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.Update), typeof(GameTime))]
    private static class Hold {
        private static bool Prepare() { return ModConfig.LoadCartCapacity.Value; }

        private static void Postfix(Cart2Controller __instance) {
            try {
                CartCargoClient.UpdateSlots(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartCapacityClientPatch.Hold.Postfix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.OnRemove), typeof(EntityRemoveInfo))]
    private static class Release {
        private static bool Prepare() { return ModConfig.LoadCartCapacity.Value; }

        private static void Postfix(Cart2Controller __instance) {
            try {
                CartCargoClient.ReleaseAll(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartCapacityClientPatch.Release.Postfix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.EntityInitialize))]
    private static class Flags {
        private static bool Prepare() { return ModConfig.LoadCartCapacity.Value; }

        private static void Postfix() {
            try {
                CartCapacity.NoteWorldLoaded("client");
            }
            catch (Exception ex) {
                ModLog.Fault("CartCapacityClientPatch.Flags.Postfix", ex);
                throw;
            }
        }
    }
}
