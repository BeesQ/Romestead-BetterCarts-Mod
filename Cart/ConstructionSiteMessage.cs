using System;
using System.Runtime.CompilerServices;
using Candide.Entities.Controllers.Other;
using Candide.GameModels;
using Candide.Graphics;
using CandideCreator.Shared.Helpers;
using CandideServer;
using CandideServer.Entities;
using CandideServer.Entities.Controllers;
using CandideServer.ServerManagers;
using Microsoft.Xna.Framework;
using Shared.Entity;

namespace BetterCarts;

internal static class ConstructionSiteMessage {
    // vanilla Carts react only to effect 0, so a game without Better Carts ignores this one
    private const byte EffectId = 201;
    private const int SendIntervalMs = 3000;
    private const int MaxChainWalk = 32;
    private const float TextHeight = 40f;
    private const string Message = "Construction Site Protection is ON,\ncan't pick up this item";

    private sealed class SendState {
        public long NextSendTick;
    }

    private static readonly ConditionalWeakTable<ServerCart2Controller, SendState> SendStates =
        new ConditionalWeakTable<ServerCart2Controller, SendState>();

    private static bool _shown;

    private static bool Announcing {
        get {
            if (ModConfig.Enabled == null || !ModConfig.Enabled.Value) {
                return false;
            }
            if (ModConfig.CartOverlaysEnabled == null || !ModConfig.CartOverlaysEnabled.Value) {
                return false;
            }
            return ModConfig.CartOverlayProtectionMessage != null && ModConfig.CartOverlayProtectionMessage.Value;
        }
    }

    internal static void NoteRefused(ServerCart2Controller cart) {
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed) {
            return;
        }
        SendState state = SendStates.GetOrCreateValue(cart);
        long now = Environment.TickCount64;
        if (now < state.NextSendTick) {
            return;
        }
        state.NextSendTick = now + SendIntervalMs;
        if (PulledByPlayer(cart)) {
            EntityServerManager.SendEntityVfxMessage(cartEntity.Id, EffectId);
        }
    }

    internal static void NoteEffect(Cart2Controller cart, byte vfx) {
        if (vfx != EffectId || _shown || cart == null || !Announcing) {
            return;
        }
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed || !cart.FollowingId.HasValue) {
            return;
        }
        if (!CartDisconnect.PulledByLocalPlayer(cart.FollowingId.Value)) {
            return;
        }
        _shown = true;
        FloatingTextSystem.AddNewFloatText(Message,
            cartEntity.Position.ToScreenSpace() + new Vector2(0f, -TextHeight),
            StyleHelper.TextGreenColor);
    }

    internal static void Reset() {
        _shown = false;
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
            return PlayerServerManager.IsPlayerEntity(entity);
        }
        return false;
    }
}
