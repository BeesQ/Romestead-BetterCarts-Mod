using System;
using System.Runtime.CompilerServices;
using Candide.Entities.Controllers.Other;
using Candide.GameModels;
using Candide.Sound;
using Candide.Sound.AudioEvent;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Shared.Entity;

namespace BetterCarts;

internal static class CartRollSound {
    private const string MoveSound = "event:/items/Cart/cart_move";
    private const string PushingParameter = "Pushing";
    private const float StartDelay = 0.1f;
    private const float StopDelay = 0.1f;
    private const float MinSpeed = 2f;
    // vanilla's wheel code treats a longer jump in one frame as a teleport, not rolling
    private const float MaxStep = 32f;
    private const int MaxChainWalk = 256;

    // the game's sound factory is internal, so it is bound by name; the feature table runs this at startup
    private static readonly Func<AudioEventArgs, bool, bool, EntityWrapper, AudioEvent> CreateAudioEvent =
        AccessTools.MethodDelegate<Func<AudioEventArgs, bool, bool, EntityWrapper, AudioEvent>>(
            AccessTools.Method(typeof(SoundPlayer).Assembly.GetType("Candide.Sound.AudioEventFactory", true), "CreateAudioEvent"));

    private sealed class RollState {
        public Vector2 LastPosition;
        public bool Seen;
        // starts above StopDelay, so a Cart seen for the first time counts as standing still
        public float StillTime = 1f;
        public float RollTime;
        public bool Sounding;
        public AudioEvent Sound;
    }

    private static readonly ConditionalWeakTable<Cart2Controller, RollState> States =
        new ConditionalWeakTable<Cart2Controller, RollState>();

    private static bool On {
        get {
            return ModConfig.Enabled.Value
                && ((ModConfig.LoadCartPush.Value && ModConfig.CartPushEnabled.Value)
                    || (ModConfig.LoadConnectRange.Value && ModConfig.ConnectRangeEnabled.Value && ModConfig.ConnectRange.Value > 0));
        }
    }

    internal static void Update(Cart2Controller cart) {
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed) {
            return;
        }
        RollState state = States.GetOrCreateValue(cart);
        float fdt = Math.Max(cartEntity.Fdt, 0f);
        // measured from the position, not Velocity: whether a joiner's game gets a pushed Cart's velocity was never read
        Vector2 position = cartEntity.Position2;
        float moved = state.Seen ? Vector2.Distance(position, state.LastPosition) : 0f;
        state.LastPosition = position;
        state.Seen = true;
        if (fdt > 0f && moved > MinSpeed * fdt && moved <= MaxStep) {
            state.StillTime = 0f;
        }
        else {
            state.StillTime = Math.Min(state.StillTime + fdt, 1f);
        }
        if (On && state.StillTime <= StopDelay && OwnsSound(cart, cartEntity)) {
            state.RollTime += fdt;
            if (state.RollTime > StartDelay && (state.Sounding || state.StillTime == 0f)) {
                if (state.Sound == null || state.Sound.HasStoppedPlaying()) {
                    state.Sound?.Unload();
                    state.Sound = CreateAudioEvent(new AudioEventArgs(MoveSound), false, false, cartEntity);
                }
                state.Sound.PlayIfStopped(new FmodParameter(PushingParameter, 1f));
                state.Sounding = true;
            }
        }
        else {
            state.RollTime = 0f;
            state.Sounding = false;
            state.Sound?.SetFmodParameter(new FmodParameter(PushingParameter, 0f));
        }
        if (state.Sound != null) {
            state.Sound.SetPosition(cartEntity.Position);
            state.Sound.Update(fdt);
        }
    }

    internal static void Unload(Cart2Controller cart) {
        if (cart == null || !States.TryGetValue(cart, out RollState state)) {
            return;
        }
        state.Sound?.Unload();
        States.Remove(cart);
    }

    private static bool OwnsSound(Cart2Controller cart, EntityWrapper cartEntity) {
        Guid? next = cart.FollowingId;
        if (!next.HasValue) {
            return true;
        }
        if (!GameState.Entities.TryGetValue(next.Value, out EntityWrapper ahead) || ahead == null
            || ahead.BaseGuid != cartEntity.BaseGuid) {
            return false;
        }
        for (int step = 0; step < MaxChainWalk; step++) {
            if (ahead == null || ahead.Removed || !(ahead.Controller is Cart2Controller aheadCart)) {
                return false;
            }
            next = aheadCart.FollowingId;
            if (!next.HasValue) {
                return true;
            }
            if (!GameState.Entities.TryGetValue(next.Value, out ahead)) {
                return false;
            }
        }
        return false;
    }
}
