using System;
using System.Runtime.CompilerServices;
using BetterCarts.Patches;
using Candide.Entities.Controllers.Other;
using Candide.GameModels;
using Candide.GameModels.Services;
using CandideServer;
using CandideServer.Entities;
using CandideServer.Entities.Controllers;
using CandideServer.MessageModels.Entities;
using Microsoft.Xna.Framework;
using Shared.Entity;

namespace BetterCarts;

internal static class CartPush {
    // vanilla signals use low values (a gate sends 1), so a push uses 128-255, one value per direction
    private const int FirstSignal = 128;
    private const int Directions = 128;
    // HoldMs must stay above SendIntervalMs, or a steady push stutters between two signals
    private const int SendIntervalMs = 50;
    private const int HoldMs = 100;
    private const int IdleMs = 2000;
    // kept below walking speed, so the player stays against the Cart and it never runs ahead of the push
    private const float PushSpeed = 40f;
    private const float Accel = 300f;
    private const float Decel = 600f;
    private const float PassOnFalloff = 0.6f;
    private const float MinPassOnSpeed = 10f;
    private const float PassOnMinDot = 0.5f;
    private const float LinkGap = 4f;
    private const float StretchTiles = 1f;
    private const int MaxChainWalk = 256;

    private sealed class SendState {
        public long NextSendTick;
    }

    private sealed class PushState {
        public bool Active;
        public Vector2 Direction;
        public float Speed;
        public float TargetSpeed;
        public long HoldUntil;
    }

    private static readonly ConditionalWeakTable<Cart2Controller, SendState> SendStates =
        new ConditionalWeakTable<Cart2Controller, SendState>();

    private static readonly ConditionalWeakTable<ServerCart2Controller, PushState> PushStates =
        new ConditionalWeakTable<ServerCart2Controller, PushState>();

    private static long _lastActivity;

    private static bool On {
        get { return ModConfig.Enabled.Value && ModConfig.CartPushEnabled.Value; }
    }

    private static bool Idle(long now) {
        return now - _lastActivity > IdleMs;
    }

    internal static void NoteClientTouch(Cart2Controller cart, EntityWrapper other, Vector3 otherVelocity) {
        if (cart == null || other == null || !On) {
            return;
        }
        var localPlayer = GameState.LocalPlayer;
        if (localPlayer == null || other.Id != localPlayer.EntityId) {
            return;
        }
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed) {
            return;
        }
        Vector2 away = cartEntity.Position2 - other.Position2;
        if (away.LengthSquared() < 0.0001f) {
            return;
        }
        // only a player walking into the Cart pushes it, not a Cart that runs into a player standing still
        if (Vector2.Dot(new Vector2(otherVelocity.X, otherVelocity.Y), away) <= 0f) {
            return;
        }
        SendState state = SendStates.GetOrCreateValue(cart);
        long now = Environment.TickCount64;
        if (now < state.NextSendTick) {
            return;
        }
        state.NextSendTick = now + SendIntervalMs;
        if (IsPulled(cart)) {
            return;
        }
        EntityService.SendEntitySignalMessage(new EntitySignalMessage {
            EntityId = cartEntity.Id,
            Signal = Encode(away)
        });
    }

    internal static bool TryReceive(EntitySignalMessage message) {
        if (message == null || message.Signal < FirstSignal) {
            return false;
        }
        if (!ServerGameState.Entities.TryGetValue(message.EntityId, out ServerEntityModel model)) {
            return false;
        }
        EntityWrapper cartEntity = model.EntityWrapper;
        if (cartEntity == null || cartEntity.Removed || !(cartEntity.Controller is ServerCart2Controller cart)) {
            return false;
        }
        if (On && !IsPulled(cart, cartEntity) && !ConnectRangePatch.IsMoving(cart)) {
            long now = Environment.TickCount64;
            PushState state = PushStates.GetOrCreateValue(cart);
            state.Active = true;
            state.Direction = Decode(message.Signal);
            state.TargetSpeed = PushSpeed;
            state.HoldUntil = now + HoldMs;
            _lastActivity = now;
        }
        return true;
    }

    internal static void Tick(ServerCart2Controller cart) {
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed) {
            return;
        }
        long now = Environment.TickCount64;
        Vector3 velocity = cartEntity.Velocity;
        // a moving Cart is still checked when idle, so a push left active over a game pause cannot keep sliding
        if (Idle(now) && velocity.X == 0f && velocity.Y == 0f) {
            return;
        }
        bool on = On;
        if (PushStates.TryGetValue(cart, out PushState state) && state.Active) {
            if (on && !IsPulled(cart, cartEntity) && !ConnectRangePatch.IsMoving(cart)) {
                Move(state, cartEntity, now);
            }
            else {
                Stop(state, cartEntity);
            }
        }
        if (on && !Idle(now)) {
            BreakStretchedLink(cart, cartEntity);
        }
    }

    internal static void NoteServerTouch(ServerCart2Controller cart, EntityWrapper other) {
        long now = Environment.TickCount64;
        if (other == null || Idle(now) || !(other.Controller is ServerCart2Controller otherCart)) {
            return;
        }
        if (!PushStates.TryGetValue(cart, out PushState state) || !state.Active || !On) {
            return;
        }
        float passed = state.Speed * PassOnFalloff;
        if (passed < MinPassOnSpeed) {
            return;
        }
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed || other.Removed) {
            return;
        }
        Vector2 away = other.Position2 - cartEntity.Position2;
        float length = away.Length();
        Vector2 direction = length > 0.01f ? away / length : state.Direction;
        bool known = PushStates.TryGetValue(otherCart, out PushState otherState);
        if (known && otherState.Active && Vector2.Dot(otherState.Direction, direction) < PassOnMinDot) {
            return;
        }
        if (IsPulled(otherCart, other) || ConnectRangePatch.IsMoving(otherCart)) {
            return;
        }
        if (!known) {
            otherState = PushStates.GetOrCreateValue(otherCart);
        }
        otherState.TargetSpeed = otherState.Active ? Math.Max(otherState.TargetSpeed, passed) : passed;
        otherState.Direction = direction;
        otherState.HoldUntil = Math.Max(otherState.HoldUntil, now + HoldMs);
        otherState.Active = true;
        _lastActivity = now;
    }

    internal static bool PausesFollow(EntityWrapper entity, EntityWrapper target) {
        if (entity == null || target == null || Idle(Environment.TickCount64)) {
            return false;
        }
        if (!(entity.Controller is ServerCart2Controller cart) || !(target.Controller is ServerCart2Controller leader)) {
            return false;
        }
        return On && (IsPushed(cart) || IsPushed(leader));
    }

    internal static bool BlocksLink(ServerCart2Controller cart, EntityWrapper other) {
        if (other == null || Idle(Environment.TickCount64) || !(other.Controller is ServerCart2Controller otherCart)) {
            return false;
        }
        return On && (IsPushed(cart) || IsPushed(otherCart));
    }

    private static bool IsPushed(ServerCart2Controller cart) {
        return PushStates.TryGetValue(cart, out PushState state) && state.Active;
    }

    private static void Move(PushState state, EntityWrapper cartEntity, long now) {
        float goal = now < state.HoldUntil ? state.TargetSpeed : 0f;
        float step = cartEntity.Fdt;
        if (state.Speed < goal) {
            state.Speed = Math.Min(state.Speed + Accel * step, goal);
        }
        else {
            state.Speed = Math.Max(state.Speed - Decel * step, goal);
        }
        // a free Cart keeps its velocity until something sets it, so the step that reaches zero must still write it
        cartEntity.Velocity = new Vector3(state.Direction.X * state.Speed, state.Direction.Y * state.Speed, cartEntity.Velocity.Z);
        if (state.Speed <= 0f) {
            state.Active = false;
            return;
        }
        _lastActivity = now;
    }

    // a pull whose puller stands still sets no velocity, so a push that moved the Cart zeroes it here or the Cart slides on
    private static void Stop(PushState state, EntityWrapper cartEntity) {
        if (state.Speed > 0f) {
            cartEntity.Velocity = new Vector3(0f, 0f, cartEntity.Velocity.Z);
        }
        state.Active = false;
        state.Speed = 0f;
    }

    private static void BreakStretchedLink(ServerCart2Controller cart, EntityWrapper cartEntity) {
        Guid? leaderId = cart.FollowingId;
        if (!leaderId.HasValue) {
            return;
        }
        EntityWrapper leaderEntity = cartEntity.System.GetEntityById(leaderId);
        if (leaderEntity == null || leaderEntity.Removed || !(leaderEntity.Controller is ServerCart2Controller leader)) {
            return;
        }
        if (!IsPushed(cart) && !IsPushed(leader)) {
            return;
        }
        float gap = cartEntity.Shape.BoundingBox.Width / 2f + leaderEntity.Shape.BoundingBox.Width / 2f + LinkGap;
        float limit = gap + StretchTiles * WorldInfo.TileSize;
        if (Vector2.DistanceSquared(cartEntity.Position2, leaderEntity.Position2) <= limit * limit) {
            return;
        }
        if (IsPulled(cart, cartEntity)) {
            return;
        }
        cart.Detach();
    }

    private static bool IsPulled(Cart2Controller cart) {
        Guid? next = cart.FollowingId;
        for (int step = 0; step < MaxChainWalk && next.HasValue; step++) {
            if (!GameState.Entities.TryGetValue(next.Value, out EntityWrapper entity) || entity == null || entity.Removed) {
                return true;
            }
            if (!(entity.Controller is Cart2Controller ahead)) {
                return true;
            }
            next = ahead.FollowingId;
        }
        return next.HasValue;
    }

    private static bool IsPulled(ServerCart2Controller cart, EntityWrapper cartEntity) {
        Guid? next = cart.FollowingId;
        for (int step = 0; step < MaxChainWalk && next.HasValue; step++) {
            EntityWrapper entity = cartEntity.System.GetEntityById(next);
            if (entity == null || entity.Removed) {
                return true;
            }
            if (!(entity.Controller is ServerCart2Controller ahead)) {
                return true;
            }
            next = ahead.FollowingId;
        }
        return next.HasValue;
    }

    private static byte Encode(Vector2 direction) {
        double turns = Math.Atan2(direction.Y, direction.X) / (2.0 * Math.PI);
        int index = (int)Math.Round(turns * Directions);
        index = ((index % Directions) + Directions) % Directions;
        return (byte)(FirstSignal + index);
    }

    private static Vector2 Decode(byte signal) {
        double angle = (signal - FirstSignal) * (2.0 * Math.PI / Directions);
        return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
    }
}
