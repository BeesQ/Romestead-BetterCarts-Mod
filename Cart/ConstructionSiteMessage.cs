using System;
using System.Runtime.CompilerServices;
using Candide.Entities.Controllers.Other;
using CandideServer;
using CandideServer.Entities;
using CandideServer.Entities.Controllers;
using CandideServer.ServerManagers;
using Shared.Entity;

namespace BetterCarts;

internal static class ConstructionSiteMessage {
    // vanilla Carts react only to effect 0, so a game without Better Carts ignores these
    private const byte KeptEffectId = 201;
    private const byte StockpileEffectId = 202;
    private const int SendIntervalMs = 3000;
    private const int MaxChainWalk = 32;

    private sealed class SendState {
        public long NextSendTick;
    }

    private static readonly ConditionalWeakTable<ServerCart2Controller, SendState> KeptStates =
        new ConditionalWeakTable<ServerCart2Controller, SendState>();

    private static readonly ConditionalWeakTable<ServerCart2Controller, SendState> StockpileStates =
        new ConditionalWeakTable<ServerCart2Controller, SendState>();

    internal static void NoteRefused(ServerCart2Controller cart) {
        Send(cart, KeptStates, KeptEffectId);
    }

    internal static void NoteStockpileSkipped(ServerCart2Controller cart) {
        Send(cart, StockpileStates, StockpileEffectId);
    }

    internal static void NoteEffect(Cart2Controller cart, byte vfx) {
        OverlayText.Message message;
        if (vfx == KeptEffectId) {
            message = OverlayText.ProtectionKept;
        } else if (vfx == StockpileEffectId) {
            message = OverlayText.StockpileSkipped;
        } else {
            return;
        }
        if (cart == null || !OverlayText.CanShow(message)) {
            return;
        }
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed || !cart.FollowingId.HasValue) {
            return;
        }
        if (!OverlayText.PulledByLocalPlayer(cart.FollowingId.Value)) {
            return;
        }
        OverlayText.Show(message, cartEntity);
    }

    private static void Send(ServerCart2Controller cart, ConditionalWeakTable<ServerCart2Controller, SendState> states, byte effectId) {
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed) {
            return;
        }
        SendState state = states.GetOrCreateValue(cart);
        long now = Environment.TickCount64;
        if (now < state.NextSendTick) {
            return;
        }
        state.NextSendTick = now + SendIntervalMs;
        if (PulledByPlayer(cart)) {
            EntityServerManager.SendEntityVfxMessage(cartEntity.Id, effectId);
        }
    }

    private static bool PulledByPlayer(ServerCart2Controller cart) {
        ServerCart2Controller current = cart;
        for (int step = 0; step < MaxChainWalk; step++) {
            Guid? next = current.FollowingId;
            if (!next.HasValue || !ServerGameState.Entities.TryGetValue(next.Value, out ServerEntityModel model)) {
                return false;
            }
            EntityWrapper entity = model.EntityWrapper;
            if (entity == null) {
                return false;
            }
            if (entity.Controller is ServerCart2Controller ahead) {
                current = ahead;
                continue;
            }
            return true;
        }
        return false;
    }
}
