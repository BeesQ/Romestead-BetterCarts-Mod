using System;
using Candide.Entities.Controllers.Other;
using Candide.LegacyUI;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shared.Entity;

namespace BetterCarts.Patches;

// client-side only: it draws what the server already decided, and a player without Better Carts simply sees no number
internal static class CartOverlayPatch {
    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.Update), typeof(GameTime))]
    private static class Track {
        private static bool Prepare() { return ModConfig.LoadCartOverlays.Value; }

        // Low runs this after the client cargo pin on the same tick, so a recount never sees extras whose CarrierId is not set yet
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(Cart2Controller __instance) {
            try {
                CartOverlay.Track(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartOverlayPatch.Track.Postfix", ex);
                throw;
            }
        }
    }

    // every cargo change reaches the client as a parameter sync, the host included, so this is where a take or drop shows up first
    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.OnServerSetState), typeof(int))]
    private static class Refresh {
        private static bool Prepare() { return ModConfig.LoadCartOverlays.Value; }

        private static void Postfix(Cart2Controller __instance) {
            try {
                CartOverlay.MarkDirty(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartOverlayPatch.Refresh.Postfix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.OnRemove), typeof(EntityRemoveInfo))]
    private static class Forget {
        private static bool Prepare() { return ModConfig.LoadCartOverlays.Value; }

        private static void Postfix(Cart2Controller __instance) {
            try {
                CartOverlay.Forget(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartOverlayPatch.Forget.Postfix", ex);
                throw;
            }
        }
    }

    // PickupTextManager, not FloatingTextSystem: this block samples PointClamp, which is what the pixel font needs. Drawing the same text in the float-text block filters it and looks soft
    [HarmonyPatch(typeof(PickupTextManager), nameof(PickupTextManager.Draw))]
    private static class Draw {
        private static bool Prepare() { return ModConfig.LoadCartOverlays.Value; }

        private static void Postfix(SpriteBatch batch, Matrix cameraMatrix) {
            try {
                CartOverlay.Draw(batch, cameraMatrix);
            }
            catch (Exception ex) {
                ModLog.Fault("CartOverlayPatch.Draw.Postfix", ex);
                throw;
            }
        }
    }
}
