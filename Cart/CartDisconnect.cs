using System;
using Candide.Entities.Controllers.Other;
using Shared.Entity;

namespace BetterCarts;

internal static class CartDisconnect {
    // a manual release clears FollowingId locally BEFORE the server echoes the change back, so only an automatic detach still holds a target when this parameter lands. The non-null to null edge is the whole discriminator - no timer, nothing sent over the wire
    internal static void NoteClientDetach(Cart2Controller cart, Guid? previousTarget) {
        if (cart == null || !OverlayText.CanShow(OverlayText.Disconnect) || !previousTarget.HasValue || cart.FollowingId.HasValue) {
            return;
        }
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed) {
            return;
        }
        if (!OverlayText.PulledByLocalPlayer(previousTarget.Value)) {
            return;
        }
        OverlayText.Show(OverlayText.Disconnect, cartEntity);
    }
}
