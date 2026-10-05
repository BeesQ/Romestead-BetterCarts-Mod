using System;
using System.Collections.Generic;
using CandideServer.Entities.Controllers;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Shared.Entity;

namespace BetterCarts.Patches;

[HarmonyPatch(typeof(ServerCart2Controller), nameof(ServerCart2Controller.Update), typeof(GameTime))]
internal static class CollectRangePatch
{
    private static readonly List<EntityWrapper> Candidates = new List<EntityWrapper>();

    private static bool Prepare() { return ModConfig.LoadCollectRange.Value; }

    private static void Postfix(ServerCart2Controller __instance)
    {
        try
        {
            if (!ModConfig.Enabled.Value || !ModConfig.CollectRangeEnabled.Value)
            {
                return;
            }
            int range = ModConfig.CollectRange.Value;
            if (range <= 0)
            {
                return;
            }
            EntityWrapper cart = __instance.Entity;
            if (cart == null || cart.Removed)
            {
                return;
            }
            float radius = range * WorldInfo.TileSize;
            // the world reuses this list for every circle query, so the loop walks a copy
            Candidates.Clear();
            Candidates.AddRange(cart.System.GetEntitiesTouchingCircleArea(cart.Position2, radius, cart.Position.Z, cart.Position.Z + 8f));
            foreach (EntityWrapper item in Candidates)
            {
                if (!ServerCart2Controller.CanBeAutoPicked(item))
                {
                    continue;
                }
                if (!CartAccess.PickupEntity(__instance, item))
                {
                    break;
                }
            }
            Candidates.Clear();
        }
        catch (Exception ex)
        {
            ModLog.Fault("CollectRangePatch.Postfix", ex);
            throw;
        }
    }
}
