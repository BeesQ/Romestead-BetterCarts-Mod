using System;
using System.Collections.Generic;
using CandideCreator.Shared.Algorithms;
using CandideServer;
using CandideServer.Models;
using CandideServer.World;
using Microsoft.Xna.Framework;
using Shared.Data;
using Shared.Entity;
using Shared.Models.Construction;

namespace BetterCarts;

internal static class ConstructionSiteProtection {
    private static readonly List<EntityWrapper> ReuseArea = new List<EntityWrapper>();

    internal static bool Active {
        get { return ModConfig.LoadConstructionSiteProtection.Value && ModConfig.Enabled.Value && ModConfig.ConstructionSiteProtectionEnabled.Value; }
    }

    internal static bool IsOnSite(EntityWrapper entity) {
        return FindSite(entity.WorldId, entity.Position2) != null;
    }

    internal static bool IsProtected(EntityWrapper item) {
        ConstructionMaterialsAggregate materials = item.ConstructionMaterials;
        if (materials == null || materials.IsEmpty || ServerGameState.Config.DisableConstructionMaterialRequirements) {
            return false;
        }
        ConstructionSiteModel site = FindSite(item.WorldId, item.Position2);
        if (site == null) {
            return false;
        }
        ConstructionModel construction = ConstructionDataBase.GetConstructionOrNull(site.ConstructionId);
        if (construction == null) {
            return false;
        }
        Dictionary<string, int> required = construction.ConstructionMaterialsRequirement.MaterialAmounts;
        if (!Supplies(required, materials)) {
            return false;
        }
        CollectArea(site);
        foreach (KeyValuePair<string, int> need in required) {
            if (need.Value <= 0) {
                continue;
            }
            int taken = materials.GetAmountForResource(need.Key);
            if (taken > 0 && LooseAmount(site, need.Key) - taken < need.Value) {
                return true;
            }
        }
        return false;
    }

    private static bool Supplies(Dictionary<string, int> required, ConstructionMaterialsAggregate materials) {
        foreach (KeyValuePair<string, int> need in required) {
            if (need.Value > 0 && materials.GetAmountForResource(need.Key) > 0) {
                return true;
            }
        }
        return false;
    }

    private static ConstructionSiteModel FindSite(Guid worldId, Vector2 position) {
        if (!ServerRunState.WorldToConstructionSitesTree.TryGetValue(worldId, out AabbTree<ConstructionSiteModel> tree)) {
            return null;
        }
        Point tile = ToTile(position);
        foreach (ConstructionSiteModel site in tree.QueryFast(new Rectangle(tile.X, tile.Y, 1, 1))) {
            if (site.TileBounds.Contains(tile)) {
                return site;
            }
        }
        return null;
    }

    private static void CollectArea(ConstructionSiteModel site) {
        ReuseArea.Clear();
        var collisions = ServerWorldHandler.GetEntityCollisionsOrNull(site.WorldId);
        if (collisions == null) {
            return;
        }
        float tileSize = WorldInfo.TileSize;
        Rectangle bounds = site.TileBounds;
        Rectangle area = new Rectangle((int)(bounds.X * tileSize), (int)(bounds.Y * tileSize),
            (int)(bounds.Width * tileSize), (int)(bounds.Height * tileSize));
        collisions.GetEntitiesInRectangleArea(area, ReuseArea);
    }

    private static int LooseAmount(ConstructionSiteModel site, string resourceId) {
        int total = 0;
        foreach (EntityWrapper other in ReuseArea) {
            if (other.Removed || other.CarrierId.HasValue || other.ConstructionMaterials == null) {
                continue;
            }
            if (!site.TileBounds.Contains(ToTile(other.Position2))) {
                continue;
            }
            total += other.ConstructionMaterials.GetAmountForResource(resourceId);
        }
        return total;
    }

    private static Point ToTile(Vector2 position) {
        float tileSize = WorldInfo.TileSize;
        return new Point((int)(position.X / tileSize), (int)(position.Y / tileSize));
    }
}
