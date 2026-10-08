using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Candide;
using Candide.Entities.Controllers.Other;
using Candide.GameModels;
using Candide.Graphics;
using CandideCreator.Shared.Graphics;
using CandideCreator.Shared.Helpers;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shared.Entity;

namespace BetterCarts;

internal static class CartOverlay {
    private const int RefreshIntervalMs = 150;

    private struct ParsedParameter {
        internal string Raw;
        internal Guid? Id;
    }

    private sealed class OverlayState {
        internal OverlayState(Cart2Controller owner) {
            Owner = new WeakReference<Cart2Controller>(owner);
        }

        internal readonly WeakReference<Cart2Controller> Owner;
        internal int Count = -1;
        internal string Text = string.Empty;
        internal string MeasuredText;
        internal StaticSpriteFont MeasuredFont;
        internal float MeasuredScale;
        internal float TextWidth;
        internal float TextHeight;
        internal long NextRefreshTick;
        internal string CargoRaw;
        internal readonly List<Guid> Extras = new List<Guid>();
        internal readonly HashSet<Guid> ExtrasSeen = new HashSet<Guid>();
        internal readonly Dictionary<string, ParsedParameter> Parameters =
            new Dictionary<string, ParsedParameter>(StringComparer.Ordinal);
        internal readonly HashSet<string> SeenKeys = new HashSet<string>(StringComparer.Ordinal);
        internal readonly List<string> RemovedKeys = new List<string>();
        internal readonly HashSet<Guid> Candidates = new HashSet<Guid>();
    }

    private static readonly ConditionalWeakTable<Cart2Controller, OverlayState> States =
        new ConditionalWeakTable<Cart2Controller, OverlayState>();

    // Draw walks this instead of the weak table, which allocated an enumerator and took a lock every frame. Same insertion order; the weak owner keeps a removed Cart collectable
    private static readonly List<OverlayState> Ordered = new List<OverlayState>();

    internal static bool Showing {
        get {
            if (ModConfig.Enabled == null || !ModConfig.Enabled.Value) {
                return false;
            }
            if (ModConfig.CartOverlaysEnabled == null || !ModConfig.CartOverlaysEnabled.Value) {
                return false;
            }
            return Flag(ModConfig.CartOverlayShowAboveVanilla)
                || Flag(ModConfig.CartOverlayShowVanilla)
                || Flag(ModConfig.CartOverlayShowEmpty);
        }
    }

    internal static void Track(Cart2Controller cart) {
        if (cart == null) {
            return;
        }
        if (!Showing) {
            if (States.TryGetValue(cart, out OverlayState hidden)) {
                hidden.NextRefreshTick = 0;
                hidden.Count = -1;
            }
            return;
        }
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed) {
            Forget(cart);
            return;
        }
        if (!States.TryGetValue(cart, out OverlayState state)) {
            state = new OverlayState(cart);
            States.Add(cart, state);
            PruneCollected();
            Ordered.Add(state);
        }
        long now = Environment.TickCount64;
        if (now < state.NextRefreshTick) {
            return;
        }
        state.NextRefreshTick = now + RefreshIntervalMs;
        int count = CountCargo(cart, cartEntity, state);
        if (count != state.Count) {
            state.Count = count;
            state.Text = count.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal static void MarkDirty(Cart2Controller cart) {
        if (cart != null && States.TryGetValue(cart, out OverlayState state)) {
            state.NextRefreshTick = 0;
        }
    }

    internal static void Forget(Cart2Controller cart) {
        if (cart != null && States.TryGetValue(cart, out OverlayState state)) {
            States.Remove(cart);
            Ordered.Remove(state);
        }
    }

    private static void PruneCollected() {
        for (int i = Ordered.Count - 1; i >= 0; i--) {
            if (!Ordered[i].Owner.TryGetTarget(out _)) {
                Ordered.RemoveAt(i);
            }
        }
    }

    // the batch is already open and carries no camera transform, exactly like PickupTextManager's own draw, so every position is transformed here and nothing calls Begin or End
    internal static void Draw(SpriteBatch batch, Matrix cameraMatrix) {
        if (batch == null || !Showing) {
            return;
        }
        Vector2 cameraCenter = Globals.Game.Camera.CurrentPositionCenter;
        float cull = batch.GraphicsDevice.Viewport.Width * OverlayText.ViewportCullFactor;
        cull *= cull;
        float scale = Globals.InterfaceScale * OverlayText.TextScale;
        Vector2 scaleVector = new Vector2(scale, scale);
        Vector2? scaleArgument = scaleVector;
        Vector2 anchorOffset = new Vector2(0f, -OverlayText.AnchorHeight);

        for (int i = 0; i < Ordered.Count; i++) {
            OverlayState state = Ordered[i];
            if (state.Count < 0 || !Visible(state.Count)) {
                continue;
            }
            if (!state.Owner.TryGetTarget(out Cart2Controller cart) || cart == null) {
                continue;
            }
            EntityWrapper cartEntity = cart.Entity;
            if (cartEntity == null || cartEntity.Removed || !GameState.Entities.ContainsKey(cartEntity.Id)) {
                continue;
            }
            Vector2 anchor = cartEntity.Position.ToScreenSpace() + anchorOffset;
            if (Vector2.DistanceSquared(anchor, cameraCenter) > cull) {
                continue;
            }
            if (OverlayText.CullWhenZoomedOut && DeferredRenderer.IsOutsideFoW(cartEntity)) {
                continue;
            }
            Vector2 position = Vector2.Transform(anchor, cameraMatrix);
            position.X = MathF.Round(position.X);
            position.Y = MathF.Round(position.Y);
            Measure(state, scale, scaleVector);
            float halfWidth = state.TextWidth / 2f;
            if (OverlayText.DrawPlate) {
                float plateWidth = state.TextWidth + OverlayText.PlatePaddingX;
                float plateHeight = state.TextHeight + OverlayText.PlatePaddingY;
                batch.DrawRect(position - new Vector2(plateWidth / 2f, 1f), plateWidth, plateHeight, OverlayText.PlateColor);
            }
            if (OverlayText.DrawShadow) {
                DrawCentered(batch, state.Text, position + new Vector2(OverlayText.ShadowOffset, OverlayText.ShadowOffset), halfWidth, scaleArgument, OverlayText.ShadowColor);
                DrawCentered(batch, state.Text, position + new Vector2(OverlayText.ShadowOffset, -OverlayText.ShadowOffset), halfWidth, scaleArgument, OverlayText.ShadowColor);
                DrawCentered(batch, state.Text, position + new Vector2(-OverlayText.ShadowOffset, OverlayText.ShadowOffset), halfWidth, scaleArgument, OverlayText.ShadowColor);
                DrawCentered(batch, state.Text, position + new Vector2(-OverlayText.ShadowOffset, -OverlayText.ShadowOffset), halfWidth, scaleArgument, OverlayText.ShadowColor);
            }
            DrawCentered(batch, state.Text, position, halfWidth, scaleArgument, OverlayText.TextColor);
        }
    }

    private static void Measure(OverlayState state, float scale, Vector2 scaleVector) {
        if (ReferenceEquals(state.MeasuredText, state.Text) && ReferenceEquals(state.MeasuredFont, OverlayText.Font)
            && state.MeasuredScale == scale) {
            return;
        }
        // X2 and Y2 are width and height ONLY because the measured position is Vector2.Zero - pass a real position and they become far-edge coordinates instead
        Bounds bounds = OverlayText.Font.TextBounds(state.Text, Vector2.Zero, scaleVector);
        state.TextWidth = bounds.X2;
        state.TextHeight = bounds.Y2;
        state.MeasuredText = state.Text;
        state.MeasuredFont = OverlayText.Font;
        state.MeasuredScale = scale;
    }

    // the game's DrawHorizontallyCentered minus its TextBounds call, which it repeats on every draw - five measurements per label per frame with the shadow on. Same DrawText arguments, so the output is pixel-identical
    private static void DrawCentered(SpriteBatch batch, string text, Vector2 position, float halfWidth, Vector2? scale, Color color) {
        OverlayText.Font.DrawText(batch, text, new Vector2(position.X - halfWidth, position.Y), color, 0f, default(Vector2), scale);
    }

    private static bool Visible(int count) {
        if (count == 0) {
            return Flag(ModConfig.CartOverlayShowEmpty);
        }
        if (count > CartCapacity.VanillaBlessed) {
            return Flag(ModConfig.CartOverlayShowAboveVanilla);
        }
        return Flag(ModConfig.CartOverlayShowVanilla);
    }

    // a cart can name the SAME item in several slot parameters, so counting entries reports five for a cart holding two. Distinct ids are the only correct count, and CarrierId is what separates cargo from the other Guids a cart stores
    private static int CountCargo(Cart2Controller cart, EntityWrapper cartEntity, OverlayState state) {
        var parameters = cart.Parameters;
        if (parameters == null) {
            return 0;
        }
        string raw = parameters.GetString(CartCargoSync.CargoKey, string.Empty);
        if (!string.Equals(state.CargoRaw, raw, StringComparison.Ordinal)) {
            CartCargoSync.Unpack(raw, state.Extras, state.ExtrasSeen);
            state.CargoRaw = raw;
        }

        state.SeenKeys.Clear();
        var dictionary = parameters.Dictionary;
        if (dictionary != null) {
            foreach (var pair in dictionary) {
                if (string.Equals(pair.Key, CartCargoSync.CargoKey, StringComparison.Ordinal)) {
                    continue;
                }
                state.SeenKeys.Add(pair.Key);
                if (!state.Parameters.TryGetValue(pair.Key, out ParsedParameter parsed)
                    || !string.Equals(parsed.Raw, pair.Value, StringComparison.Ordinal)) {
                    parsed.Raw = pair.Value;
                    parsed.Id = Guid.TryParse(pair.Value, out Guid id) ? id : (Guid?)null;
                    state.Parameters[pair.Key] = parsed;
                }
            }
        }

        state.RemovedKeys.Clear();
        state.Candidates.Clear();
        foreach (var pair in state.Parameters) {
            if (!state.SeenKeys.Contains(pair.Key)) {
                state.RemovedKeys.Add(pair.Key);
            }
            else if (pair.Value.Id.HasValue) {
                state.Candidates.Add(pair.Value.Id.Value);
            }
        }
        foreach (string key in state.RemovedKeys) {
            state.Parameters.Remove(key);
        }
        state.RemovedKeys.Clear();
        foreach (Guid id in state.Extras) {
            state.Candidates.Add(id);
        }

        int count = 0;
        // Resolve every scheduled refresh, even with unchanged parameters. Late arrivals stay candidates.
        foreach (Guid id in state.Candidates) {
            if (GameState.Entities.TryGetValue(id, out EntityWrapper item)
                && item != null && !item.Removed && item.Carriable && item.CarrierId == cartEntity.Id) {
                count++;
            }
        }
        return count;
    }

    private static bool Flag(BepInEx.Configuration.ConfigEntry<bool> entry) {
        return entry != null && entry.Value;
    }
}
