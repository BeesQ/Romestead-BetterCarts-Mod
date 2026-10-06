using System;
using Candide.Entities.Controllers.Other;
using CandideServer.Entities.Controllers;
using HarmonyLib;
using Shared.Entity;

namespace BetterCarts.Patches;

internal static class ConstructionSiteProtectionPatch {
    [ThreadStatic]
    private static bool _clientPrediction;

    [HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.CanBePickedUp))]
    private static class PickupCheck {
        private static bool Prepare() { return ModConfig.LoadConstructionSiteProtection.Value; }

        private static void Postfix(EntityWrapper otherEntity, ref bool __result) {
            try {
                if (!__result || _clientPrediction || !ConstructionSiteProtection.Active) {
                    return;
                }
                if (ConstructionSiteProtection.IsProtected(otherEntity)) {
                    __result = false;
                }
            }
            catch (Exception ex) {
                ModLog.Fault("ConstructionSiteProtectionPatch.PickupCheck.Postfix", ex);
                throw;
            }
        }
    }

    // the client's pickup prediction calls the same check from the client thread, where server state must not be read
    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.OnEntityCollision))]
    private static class ClientPrediction {
        private static bool Prepare() { return ModConfig.LoadConstructionSiteProtection.Value; }

        private static void Prefix() {
            try {
                _clientPrediction = true;
            }
            catch (Exception ex) {
                ModLog.Fault("ConstructionSiteProtectionPatch.ClientPrediction.Prefix", ex);
                throw;
            }
        }

        private static void Finalizer() {
            _clientPrediction = false;
        }
    }
}
