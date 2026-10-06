using System;
using Candide.Entities.Controllers.Other;
using CandideServer.Entities.Controllers;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Shared.Entity;

namespace BetterCarts.Patches;

internal static class ConstructionSiteProtectionPatch {
    [ThreadStatic]
    private static bool _clientPrediction;

    [ThreadStatic]
    private static ServerCart2Controller _askingCart;

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
                    if (_askingCart != null) {
                        ConstructionSiteMessage.NoteRefused(_askingCart);
                    }
                }
            }
            catch (Exception ex) {
                ModLog.Fault("ConstructionSiteProtectionPatch.PickupCheck.Postfix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.Update), typeof(GameTime))]
    private static class AskingCart {
        private static bool Prepare() { return ModConfig.LoadConstructionSiteProtection.Value; }

        private static void Prefix(ServerCart2Controller __instance, out ServerCart2Controller __state) {
            try {
                __state = _askingCart;
                _askingCart = __instance;
            }
            catch (Exception ex) {
                ModLog.Fault("ConstructionSiteProtectionPatch.AskingCart.Prefix", ex);
                throw;
            }
        }

        private static void Finalizer(ServerCart2Controller __state) {
            _askingCart = __state;
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
