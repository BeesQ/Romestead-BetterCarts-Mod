using System;
using Candide.Entities.Controllers.Other;
using CandideServer.Entities.Controllers;
using CandideServer.MessageModels.Entities;
using CandideServer.ServerManagers;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Shared.Entity;

namespace BetterCarts.Patches;

internal static class CartPushPatch {
    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.OnEntityCollision))]
    private static class ClientTouch {
        private static bool Prepare() { return ModConfig.LoadCartPush.Value; }

        private static void Postfix(Cart2Controller __instance, EntityWrapper otherEntity, Vector3 otherOldVelocity) {
            try {
                CartPush.NoteClientTouch(__instance, otherEntity, otherOldVelocity);
            }
            catch (Exception ex) {
                ModLog.Fault("CartPushPatch.ClientTouch.Postfix", ex);
                throw;
            }
        }
    }

    // the Cart controllers ignore signals, so a host without Better Carts simply drops a push
    [HarmonyPatch(typeof(EntityServerManager), nameof(EntityServerManager.HandleEntitySignalMessage))]
    private static class ReceiveSignal {
        private static bool Prepare() { return ModConfig.LoadCartPush.Value; }

        private static bool Prefix(EntitySignalMessage msg) {
            try {
                return !CartPush.TryReceive(msg);
            }
            catch (Exception ex) {
                ModLog.Fault("CartPushPatch.ReceiveSignal.Prefix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.Update), typeof(GameTime))]
    private static class Move {
        private static bool Prepare() { return ModConfig.LoadCartPush.Value; }

        private static void Postfix(ServerCart2Controller __instance) {
            try {
                CartPush.Tick(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartPushPatch.Move.Postfix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.OnEntityCollision))]
    private static class PassOn {
        private static bool Prepare() { return ModConfig.LoadCartPush.Value; }

        private static void Postfix(ServerCart2Controller __instance, EntityWrapper otherEntity) {
            try {
                CartPush.NoteServerTouch(__instance, otherEntity);
            }
            catch (Exception ex) {
                ModLog.Fault("CartPushPatch.PassOn.Postfix", ex);
                throw;
            }
        }
    }

    // reporting success without setting a velocity keeps StuckTimer at zero, so a paused link is not dropped as stuck
    [HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.UpdateCartVelocity))]
    private static class PauseFollow {
        private static bool Prepare() { return ModConfig.LoadCartPush.Value; }

        private static bool Prefix(EntityWrapper entity, EntityWrapper target, ref bool __result) {
            try {
                if (!CartPush.PausesFollow(entity, target)) {
                    return true;
                }
                __result = true;
                return false;
            }
            catch (Exception ex) {
                ModLog.Fault("CartPushPatch.PauseFollow.Prefix", ex);
                throw;
            }
        }
    }

    // false is vanilla's no-link answer and lets the collision go on to item pickup; true would skip the pickup
    [HarmonyPatch(typeof(ServerCart2Controller), "HandleConnectToOtherCart")]
    private static class NoLink {
        private static bool Prepare() { return ModConfig.LoadCartPush.Value; }

        private static bool Prefix(ServerCart2Controller __instance, EntityWrapper otherEntity, ref bool __result) {
            try {
                if (!CartPush.BlocksLink(__instance, otherEntity)) {
                    return true;
                }
                __result = false;
                return false;
            }
            catch (Exception ex) {
                ModLog.Fault("CartPushPatch.NoLink.Prefix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.Update), typeof(GameTime))]
    private static class RollSound {
        private static bool Prepare() { return ModConfig.LoadCartPush.Value; }

        private static void Postfix(Cart2Controller __instance) {
            try {
                CartRollSound.Update(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartPushPatch.RollSound.Postfix", ex);
                throw;
            }
        }
    }

    [HarmonyPatch(typeof(Cart2Controller), nameof(Cart2Controller.EntityUnload))]
    private static class UnloadSound {
        private static bool Prepare() { return ModConfig.LoadCartPush.Value; }

        private static void Postfix(Cart2Controller __instance) {
            try {
                CartRollSound.Unload(__instance);
            }
            catch (Exception ex) {
                ModLog.Fault("CartPushPatch.UnloadSound.Postfix", ex);
                throw;
            }
        }
    }
}
