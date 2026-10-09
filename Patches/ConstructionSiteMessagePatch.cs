using System;
using Candide.Entities.Controllers.Other;
using HarmonyLib;

namespace BetterCarts.Patches;

internal static class ConstructionSiteMessagePatch {
    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.ReceiveEntityVfxMessage))]
    private static class Receive {
        private static bool Prepare() { return ModConfig.LoadCartOverlays.Value; }

        private static void Postfix(Cart2Controller __instance, byte vfx) {
            try {
                ConstructionSiteMessage.NoteEffect(__instance, vfx);
            }
            catch (Exception ex) {
                ModLog.Fault("ConstructionSiteMessagePatch.Receive.Postfix", ex);
                throw;
            }
        }
    }
}
