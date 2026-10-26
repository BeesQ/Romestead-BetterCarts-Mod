using System;
using Candide.Entities.Controllers.Other;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace BetterCarts.Patches;

internal static class CartRollSoundPatch {
    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.Update), typeof(GameTime))]
    private static class Roll {
        private static bool Prepare() { return ModConfig.LoadCartPush.Value || ModConfig.LoadConnectRange.Value; }

        private static void Postfix(Cart2Controller __instance) {
            try {
                CartRollSound.Update(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartRollSoundPatch.Roll.Postfix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.EntityUnload))]
    private static class Forget {
        private static bool Prepare() { return ModConfig.LoadCartPush.Value || ModConfig.LoadConnectRange.Value; }

        private static void Postfix(Cart2Controller __instance) {
            try {
                CartRollSound.Unload(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartRollSoundPatch.Forget.Postfix", ex);
                throw;
            }
        }
    }
}
