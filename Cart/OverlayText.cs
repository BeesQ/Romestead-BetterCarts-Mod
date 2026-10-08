using System;
using BepInEx.Configuration;
using Candide;
using Candide.Entities.Controllers.Other;
using Candide.GameModels;
using Candide.Graphics;
using Candide.Graphics.Fonts;
using Candide.LegacyUI;
using CandideCreator.Shared.Helpers;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Shared.Entity;

namespace BetterCarts;

internal static class OverlayText {
    // ===== Count above each Cart, drawn every frame by CartOverlay =====

    // Font
    // ArialPixel, Adventurer, Arial, Calibri, Courier
    internal static readonly StaticSpriteFont Font = PixelSpriteFont.ArialPixel;

    // Size
    // multiplies Globals.InterfaceScale, which is 2 by default. Whole numbers keep the glyph pixels even
    internal const float TextScale = 1f;

    // Height
    // world units above the cart, applied before the camera transform, so the gap scales with zoom
    internal const float AnchorHeight = 16f;

    // Color
    // any StyleHelper constant: TextNormalColor, TextYellowColor, TextGreenColor, TextWarningColor
    internal static readonly Color TextColor = StyleHelper.TextNormalColor;

    // Shadow (4-sided)
    internal static readonly bool DrawShadow = true;
    internal static readonly float ShadowOffset = 1f;
    internal static readonly Color ShadowColor = Color.Black;

    // Backing plate
    // vanilla's own plate is DimGray at 0.3 alpha with 4 x 2 padding
    internal static readonly bool DrawPlate = false;
    internal const float PlatePaddingX = 4f;
    internal const float PlatePaddingY = 2f;
    internal static readonly Color PlateColor = new Color(Color.DimGray, 0.3f);

    // Zoom cull
    // vanilla hides distant entities while zoomed out; at normal zoom this costs one bool test and hides nothing
    internal static readonly bool CullWhenZoomedOut = true;

    // Viewport cull
    internal const float ViewportCullFactor = 0.8f;

    // ===== Messages above a Cart, shown with the game's floating or pickup text =====

    // Height
    // world units above the cart where a message appears
    private const float MessageHeight = 40f;

    // Text type
    // Floating: the game's floating text, smooth outlined font, gone after 2 s
    // Pickup: the game's pickup text, ArialPixel like the count, fades over 5 s
    internal enum TextType {
        Floating,
        Pickup
    }

    internal static readonly Message Disconnect = new Message("Cart disconnected!",
        StyleHelper.TextWarningColor, TextType.Floating, oncePerWorldLoad: false, () => ModConfig.CartOverlayDisconnectMessage);

    internal static readonly Message ProtectionKept = new Message("Construction Site Protection is ON,\ncan't pick up this item",
        StyleHelper.TextGreenColor, TextType.Pickup, oncePerWorldLoad: true, () => ModConfig.CartOverlayProtectionMessage);

    private static readonly Message[] Messages = { Disconnect, ProtectionKept };

    private const int MaxChainWalk = 32;

    internal sealed class Message {
        internal Message(string text, Color color, TextType type, bool oncePerWorldLoad, Func<ConfigEntry<bool>> setting) {
            Text = text;
            Color = color;
            Type = type;
            OncePerWorldLoad = oncePerWorldLoad;
            Setting = setting;
        }

        internal readonly string Text;
        internal readonly Color Color;
        internal readonly TextType Type;
        internal readonly bool OncePerWorldLoad;
        internal readonly Func<ConfigEntry<bool>> Setting;
        internal bool Shown;
    }

    internal static bool CanShow(Message message) {
        if (ModConfig.Enabled == null || !ModConfig.Enabled.Value) {
            return false;
        }
        if (ModConfig.CartOverlaysEnabled == null || !ModConfig.CartOverlaysEnabled.Value) {
            return false;
        }
        ConfigEntry<bool> setting = message.Setting();
        if (setting == null || !setting.Value) {
            return false;
        }
        return !message.OncePerWorldLoad || !message.Shown;
    }

    internal static void Show(Message message, EntityWrapper cartEntity) {
        if (cartEntity == null || cartEntity.Removed || !CanShow(message)) {
            return;
        }
        message.Shown = true;
        Vector2 anchor = cartEntity.Position.ToScreenSpace() + new Vector2(0f, -MessageHeight);
        if (message.Type == TextType.Pickup) {
            // pickup text hangs down from its anchor and is sized in screen pixels, so lifting it by its height in world units keeps its bottom line at MessageHeight at every zoom
            float height = PixelSpriteFont.ArialPixel.MeasureString(message.Text).Y * ((float)Globals.InterfaceScale / Globals.CameraScale);
            PickupTextManager.AddNewText(message.Text, anchor - new Vector2(0f, height), message.Color);
        }
        else {
            FloatingTextSystem.AddNewFloatText(message.Text, anchor, message.Color);
        }
    }

    internal static void ResetForWorldLoad() {
        foreach (Message message in Messages) {
            message.Shown = false;
        }
    }

    // the same structural walk Connect Range uses: follow the chain until it stops being a cart, and the entity it ends at is whoever was pulling
    internal static bool PulledByLocalPlayer(Guid start) {
        var localPlayer = GameState.LocalPlayer;
        if (localPlayer == null) {
            return false;
        }
        Guid? next = start;
        for (int step = 0; step < MaxChainWalk && next.HasValue; step++) {
            if (!GameState.Entities.TryGetValue(next.Value, out EntityWrapper entity) || entity == null) {
                return false;
            }
            if (entity.Controller is Cart2Controller ahead) {
                next = ahead.FollowingId;
                continue;
            }
            return entity.Id == localPlayer.EntityId;
        }
        return false;
    }
}
