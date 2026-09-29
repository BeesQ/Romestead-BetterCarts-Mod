using CandideServer;
using CandideServer.Entities;
using CandideServer.Entities.Controllers;
using CandideServer.Helpers;
using CandideServer.SyncStrategies;
using CandideServer.World;
using Microsoft.Xna.Framework;
using Shared.Entity;
using Shared.Entity.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace BetterCarts;

internal static class CartCargo {
    private const int SweepIntervalMs = 100;
    private const float StackHeight = 6f;
    private const float ScanTiles = 2f;
    private const float EjectTiles = 1f;

    private static readonly string[] SlotKeys = {
        ServerCart2Controller.Carried1IdKey, ServerCart2Controller.Carried2IdKey,
        ServerCart2Controller.Carried3IdKey, ServerCart2Controller.Carried4IdKey,
        ServerCart2Controller.Carried5IdKey
    };

    private sealed class CartState {
        internal readonly List<Guid> Extras = new List<Guid>();
        internal long NextSweepTick;
        internal bool Adopted;
        internal string Written;
        internal readonly List<Guid> WrittenIds = new List<Guid>();
        internal long OccupiedStamp = -1;
        internal int Occupied;
        internal bool EjectDone;
    }

    private static readonly ConditionalWeakTable<ServerCart2Controller, CartState> States =
        new ConditionalWeakTable<ServerCart2Controller, CartState>();

    private static readonly List<EntityWrapper> ReuseCount = new List<EntityWrapper>();
    private static readonly List<EntityWrapper> ReuseSweep = new List<EntityWrapper>();
    private static readonly List<EntityWrapper> ReuseUnslotted = new List<EntityWrapper>();
    private static readonly List<EntityWrapper> ReuseDrop = new List<EntityWrapper>();
    private static readonly List<Guid> ReuseOrder = new List<Guid>();
    private static readonly List<Guid> ReuseAdopt = new List<Guid>();
    private static readonly HashSet<Guid> ReuseAdoptSeen = new HashSet<Guid>();
    private static readonly HashSet<Guid> ReuseSlotted = new HashSet<Guid>();
    private static readonly HashSet<Guid> ReuseSweepIds = new HashSet<Guid>();
    private static readonly HashSet<Guid> ReuseItemIds = new HashSet<Guid>();
    private static readonly HashSet<Guid> ReuseOrderIds = new HashSet<Guid>();

    // Stockpile Range's capacity pre-check. A carrier COUNT is exact for every mod's extra slots, unlike reading Carried1..5, which caps every cart at the vanilla five no matter what Cart Capacity allows
    internal static bool HasFreeSlot(ServerCart2Controller cart) {
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed) {
            return false;
        }
        return GetOccupied(cart) < CartCapacity.GetKnownCapacity(cartEntity);
    }

    // vanilla runs PickupEntity for every touching entity every tick, so the count is memoized and only invalidated when it really changes
    internal static int GetOccupied(ServerCart2Controller cart) {
        CartState state = States.GetOrCreateValue(cart);
        Adopt(cart, state);
        long now = Environment.TickCount64;
        if (state.OccupiedStamp == now) {
            return state.Occupied;
        }
        CollectCarried(cart, ReuseCount);
        // a just-pinned item is not in the collision index until its position is written, so the rect query can under-report for a tick. Slot parameters plus our own extras is a floor that is never stale - without it two EXTENDs in one tick both saw the same count and pushed a Cart past its capacity, and a refusal sampled mid-fill recorded a capacity one too low
        state.Occupied = Math.Max(ReuseCount.Count, SlottedCount(cart) + state.Extras.Count);
        state.OccupiedStamp = now;
        return state.Occupied;
    }

    internal static void Invalidate(ServerCart2Controller cart) {
        States.GetOrCreateValue(cart).OccupiedStamp = -1;
    }

    // how many slot parameters name CARGO right now, including another mod's. Compared against the carrier count, this detects the tick where a Cart's bookkeeping and its real cargo disagree
    // a Cart's parameters hold several Guids that are NOT cargo - owner_character_id, following, wheels_guid - so counting every Guid-shaped value inflated this by one or two and made Carts refuse early. Carriable is the same property CanBePickedUp uses and the same one CollectCarried filters on, so both halves now measure the same thing without knowing any mod's key names
    private static int SlottedCount(ServerCart2Controller cart) {
        EntityWrapper cartEntity = cart.Entity;
        var parameters = cart.Parameters;
        var dictionary = parameters == null ? null : parameters.Dictionary;
        if (cartEntity == null || dictionary == null) {
            return 0;
        }
        ReuseSlotted.Clear();
        foreach (var pair in dictionary) {
            if (string.Equals(pair.Key, CartCargoSync.CargoKey, StringComparison.Ordinal)) {
                continue;
            }
            if (pair.Value == null || pair.Value.Length != 36 || !Guid.TryParse(pair.Value, out Guid id)
                || id == Guid.Empty) {
                continue;
            }
            EntityWrapper item = cartEntity.System.GetEntityById(id);
            // a slot still names an item a player just took until vanilla clears it
            if (item != null && !item.Removed && item.Carriable && item.CarrierId == cartEntity.Id) {
                ReuseSlotted.Add(id);
            }
        }
        return ReuseSlotted.Count;
    }

    internal static bool CanTakeExtra(ServerCart2Controller cart) {
        CartState state = States.GetOrCreateValue(cart);
        Adopt(cart, state);
        return state.Extras.Count < CartCargoSync.MaxExtras;
    }

    internal static void PinExtra(ServerCart2Controller cart, EntityWrapper item) {
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed || item == null || item.Removed
            || (item.CarrierId.HasValue && item.CarrierId != cartEntity.Id)) {
            return;
        }
        CartState state = States.GetOrCreateValue(cart);
        Adopt(cart, state);
        ClearThrowState(item);
        Pin(cartEntity, item);
        if (!state.Extras.Contains(item.Id)) {
            state.Extras.Add(item.Id);
            if (ModLog.AdvancedEnabled) {
                ModLog.Advanced("PIN extra " + Short(item.Id) + " on cart " + Short(cartEntity.Id)
                    + " (extras=" + state.Extras.Count + ")");
            }
        }
        state.OccupiedStamp = -1;
        Publish(cartEntity, state);
    }

    internal static void Tick(ServerCart2Controller cart) {
        CartState state = States.GetOrCreateValue(cart);
        Adopt(cart, state);
        HoldExtras(cart, state);
        long now = Environment.TickCount64;
        if (now < state.NextSweepTick) {
            return;
        }
        state.NextSweepTick = now + SweepIntervalMs;
        Sweep(cart, state);
    }

    // vanilla OnRemove only clears the five slots it knows about, so everything the mod pinned has to be freed here or it is stranded in the save
    internal static void ReleaseAll(ServerCart2Controller cart, bool clearStoredCargo = true) {
        CartState state = States.GetOrCreateValue(cart);
        state.Extras.Clear();
        state.OccupiedStamp = -1;
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null) {
            return;
        }
        CollectCarried(cart, ReuseSweep);
        foreach (EntityWrapper item in ReuseSweep) {
            if (!IsSlotted(cart, item.Id)) {
                Release(cartEntity, item);
            }
        }
        if (clearStoredCargo) {
            Publish(cartEntity, state);
        }
    }

    // a Cart loaded from the save already knows its extras from the parameter, so adoption is exact instead of inferred from whatever happens to be carried
    private static void Adopt(ServerCart2Controller cart, CartState state) {
        if (state.Adopted) {
            return;
        }
        var parameters = cart.Parameters;
        if (parameters == null || cart.Entity == null || cart.Entity.Removed) {
            return;
        }
        state.Adopted = true;
        string stored = parameters.GetString(CartCargoSync.CargoKey, string.Empty);
        if (!string.IsNullOrEmpty(stored) && ModLog.AdvancedEnabled) {
            ModLog.Advanced("ADOPT cart=" + Short(cart.Entity.Id) + " bc_cargo=\"" + stored + "\"");
        }
        CartCargoSync.Unpack(stored, ReuseAdopt, ReuseAdoptSeen);
        state.Extras.Clear();
        foreach (Guid id in ReuseAdopt) {
            state.Extras.Add(id);
        }
        state.Written = CartCargoSync.Pack(state.Extras);
        state.WrittenIds.Clear();
        state.WrittenIds.AddRange(state.Extras);
        state.OccupiedStamp = -1;
    }

    private static void HoldExtras(ServerCart2Controller cart, CartState state) {
        if (state.Extras.Count == 0) {
            return;
        }
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed) {
            return;
        }
        bool changed = false;
        for (int i = state.Extras.Count - 1; i >= 0; i--) {
            EntityWrapper item = cartEntity.System.GetEntityById(state.Extras[i]);
            if (item == null || item.Removed) {
                // A saved entity can exist without an active simulation wrapper.
                if (ServerGameState.Entities.ContainsKey(state.Extras[i])) {
                    continue;
                }

                // Its authoritative model is gone too.
                state.Extras.RemoveAt(i);
                changed = true;
                continue;
            }
            // the player grabbed it off the Cart; vanilla's UpdateCarriedItem performs exactly this reset, and without it the item stays collisionless and no Cart can pick it up again
            if (item.CarrierId.HasValue && item.CarrierId != cartEntity.Id) {
                if (ModLog.AdvancedEnabled) {
                    ModLog.Advanced("UNPIN extra " + Short(item.Id) + " - carrier changed to " + Short(item.CarrierId.Value));
                }
                item.NoEntityCollision = false;
                item.NoTerrainCollision = false;
                state.Extras.RemoveAt(i);
                changed = true;
                continue;
            }
            // extras adopted from a save or the sweep skip PinExtra's cleanup
            if (item.IsThrown || item.ThrowerId.HasValue) {
                ClearThrowState(item);
            }
            Pin(cartEntity, item);
            Stack(cartEntity, item);
        }
        if (changed) {
            state.OccupiedStamp = -1;
            Publish(cartEntity, state);
        }
    }

    private static void Sweep(ServerCart2Controller cart, CartState state) {
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null || cartEntity.Removed) {
            return;
        }
        CollectCarried(cart, ReuseSweep);
        ReuseSweepIds.Clear();
        foreach (EntityWrapper carried in ReuseSweep) {
            ReuseSweepIds.Add(carried.Id);
        }

        // A spatial query discovers cargo; it must not erase known membership.
        foreach (Guid id in state.Extras) {
            EntityWrapper item = cartEntity.System.GetEntityById(id);
            if (item == null || item.Removed) {
                // HoldExtras retained a still-existing server model. Defer this
                // reconciliation until its wrapper returns instead of dropping IDs.
                return;
            }

            if (item.CarrierId == cartEntity.Id && ReuseSweepIds.Add(id)) {
                ReuseSweep.Add(item);
            }
        }

        DumpSweep(cart, cartEntity);

        // cargo the mod pinned carries no slot parameter; vanilla c1..c5 and other mods' extra slots all do, so a parameter lookup separates them without knowing any mod's keys
        ReuseUnslotted.Clear();
        foreach (EntityWrapper item in ReuseSweep) {
            if (!IsSlotted(cart, item.Id)) {
                ReuseUnslotted.Add(item);
            }
        }

        bool enforced = CartCapacity.TryGetEnforcedCapacity(cartEntity, out int capacity);
        int slotted = ReuseSweep.Count - ReuseUnslotted.Count;
        bool ejecting = !state.EjectDone && enforced && CartCapacity.Ejecting;
        state.EjectDone = true;
        int keep = ReuseUnslotted.Count;
        if (ejecting) {
            keep = Math.Max(0, Math.Min(ReuseUnslotted.Count, capacity - slotted));
        }

        StableOrder(state, ReuseUnslotted);
        state.Extras.Clear();
        ReuseDrop.Clear();
        for (int i = 0; i < ReuseOrder.Count; i++) {
            if (i < keep) {
                state.Extras.Add(ReuseOrder[i]);
                continue;
            }
            EntityWrapper item = Find(ReuseUnslotted, ReuseOrder[i]);
            if (item != null) {
                ReuseDrop.Add(item);
            }
        }
        if (ejecting) {
            EjectSurplus(cart, cartEntity, capacity, slotted);
        }

        state.OccupiedStamp = -1;
        Publish(cartEntity, state);
    }

    private static void EjectSurplus(ServerCart2Controller cart, EntityWrapper cartEntity, int capacity, int slotted) {
        int extras = ReuseDrop.Count;
        int surplus = slotted - capacity;
        for (int i = SlotKeys.Length - 1; i >= 0 && ReuseDrop.Count - extras < surplus; i--) {
            ref Guid? slot = ref SlotRef(cart, i);
            if (!slot.HasValue) {
                continue;
            }
            EntityWrapper item = cartEntity.System.GetEntityById(slot.Value);
            if (item == null || item.Removed || item.CarrierId != cartEntity.Id) {
                continue;
            }
            // vanilla ClearSlot leaves both the field and the parameter set
            slot = null;
            ServerEntitySystemManager.UpdateEntityParameter(cartEntity, SlotKeys[i], string.Empty,
                SyncStrategy.Everyone());
            ReuseDrop.Add(item);
        }
        for (int i = 0; i < ReuseDrop.Count; i++) {
            if (ModLog.AdvancedEnabled) {
                ModLog.Advanced("DROP " + Short(ReuseDrop[i].Id) + " from cart " + Short(cartEntity.Id)
                    + " (slotted=" + slotted + " extras=" + extras + " cap=" + capacity + " dropped=" + ReuseDrop.Count + ")");
            }
            Drop(cartEntity, ReuseDrop[i], i, ReuseDrop.Count);
        }
        ReuseDrop.Clear();
    }

    private static ref Guid? SlotRef(ServerCart2Controller cart, int index) {
        switch (index) {
            case 0: return ref cart.Carried1;
            case 1: return ref cart.Carried2;
            case 2: return ref cart.Carried3;
            case 3: return ref cart.Carried4;
            default: return ref cart.Carried5;
        }
    }

    private static void Drop(EntityWrapper cartEntity, EntityWrapper item, int index, int count) {
        Release(cartEntity, item);
        double angle = Math.PI * 2.0 * index / count;
        float radius = WorldInfo.TileSize * EjectTiles;
        item.Position = cartEntity.Position + new Vector3((float)Math.Cos(angle) * radius,
            (float)Math.Sin(angle) * radius, StackHeight);
        item.Velocity = Vector3.Zero;
        item.System.CollisionGroup.UpdatePositionAndVelocity(item);
    }

    // the whole picture for one Cart in one line block: what it carries, which of those the parameters claim, and every parameter it has. This is what identifies cargo held by a mod whose slot keys we cannot see
    private static void DumpSweep(ServerCart2Controller cart, EntityWrapper cartEntity) {
        if (!ModLog.AdvancedEnabled) {
            return;
        }
        var parameters = cart.Parameters;
        var dictionary = parameters == null ? null : parameters.Dictionary;
        bool enforced = CartCapacity.TryGetEnforcedCapacity(cartEntity, out int capacity);
        StringBuilder builder = new StringBuilder();
        builder.Append("SWEEP cart=").Append(cartEntity.Id).Append(" type=").Append(cartEntity.BaseGuid)
            .Append(" carried=").Append(ReuseSweep.Count)
            .Append(" slotted=").Append(SlottedCount(cart))
            .Append(" cap=").Append(enforced ? capacity.ToString() : "none")
            .Append(" blessed=").Append(CartCapacity.Blessed);
        builder.Append(" | fields c1=").Append(Short(cart.Carried1)).Append(" c2=").Append(Short(cart.Carried2))
            .Append(" c3=").Append(Short(cart.Carried3)).Append(" c4=").Append(Short(cart.Carried4))
            .Append(" c5=").Append(Short(cart.Carried5));
        builder.Append(" | items");
        foreach (EntityWrapper item in ReuseSweep) {
            builder.Append(' ').Append(Short(item.Id)).Append(MatchedKey(dictionary, item.Id));
        }
        builder.Append(" | params");
        if (dictionary == null || dictionary.Count == 0) {
            builder.Append(" <none>");
        } else {
            foreach (var pair in dictionary) {
                builder.Append(' ').Append(pair.Key).Append('=')
                    .Append(pair.Value.Length == 36 ? Short(pair.Value) : pair.Value);
            }
        }
        ModLog.AdvancedOnChange("sweep:" + cartEntity.Id, builder.ToString());
    }

    private static string MatchedKey(IDictionary<string, string> dictionary, Guid itemId) {
        if (dictionary == null) {
            return "(?)";
        }
        string id = itemId.ToString();
        foreach (var pair in dictionary) {
            if (string.Equals(pair.Key, CartCargoSync.CargoKey, StringComparison.Ordinal)) {
                continue;
            }
            if (string.Equals(pair.Value, id, StringComparison.OrdinalIgnoreCase)) {
                return "(" + pair.Key + ")";
            }
        }
        return "(UNSLOTTED)";
    }

    private static string Short(Guid? id) {
        return id.HasValue ? Short(id.Value.ToString()) : "-";
    }

    private static string Short(Guid id) {
        return Short(id.ToString());
    }

    private static string Short(string id) {
        return id.Length >= 8 ? id.Substring(0, 8) : id;
    }

    // the client half of the feature learns about extra cargo ONLY from this parameter - server pins are invisible to it, exactly as vanilla c1..c5 are invisible until their key syncs
    private static void Publish(EntityWrapper cartEntity, CartState state) {
        // the same list always packs to the same text, so an unchanged list skips the encoding; a never-written state still publishes once
        if (state.Written != null && SameOrder(state.Extras, state.WrittenIds)) {
            return;
        }
        string packed = CartCargoSync.Pack(state.Extras);
        if (string.Equals(packed, state.Written, StringComparison.Ordinal)) {
            return;
        }
        state.Written = packed;
        state.WrittenIds.Clear();
        state.WrittenIds.AddRange(state.Extras);
        if (ModLog.AdvancedEnabled) {
            ModLog.Advanced("PUBLISH cart=" + Short(cartEntity.Id) + " bc_cargo=\"" + packed + "\"");
        }
        ServerEntitySystemManager.UpdateEntityParameter(cartEntity, CartCargoSync.CargoKey, packed,
            SyncStrategy.Everyone());
    }

    private static bool SameOrder(List<Guid> current, List<Guid> written) {
        if (current.Count != written.Count) {
            return false;
        }
        for (int i = 0; i < current.Count; i++) {
            if (current[i] != written[i]) {
                return false;
            }
        }
        return true;
    }

    // keeps items on the seat they already had, so a rebuild every 100 ms does not shuffle the cargo around
    private static void StableOrder(CartState state, List<EntityWrapper> items) {
        ReuseOrder.Clear();
        ReuseOrderIds.Clear();
        ReuseItemIds.Clear();
        foreach (EntityWrapper item in items) {
            ReuseItemIds.Add(item.Id);
        }
        foreach (Guid id in state.Extras) {
            if (ReuseItemIds.Contains(id)) {
                ReuseOrder.Add(id);
                ReuseOrderIds.Add(id);
            }
        }
        foreach (EntityWrapper item in items) {
            if (ReuseOrderIds.Add(item.Id)) {
                ReuseOrder.Add(item.Id);
            }
        }
    }

    private static EntityWrapper Find(List<EntityWrapper> items, Guid id) {
        foreach (EntityWrapper item in items) {
            if (item.Id == id) {
                return item;
            }
        }
        return null;
    }

    private static void CollectCarried(ServerCart2Controller cart, List<EntityWrapper> into) {
        into.Clear();
        EntityWrapper cartEntity = cart.Entity;
        if (cartEntity == null) {
            return;
        }
        var collisions = ServerWorldHandler.GetEntityCollisionsOrNull(cartEntity.WorldId);
        if (collisions == null) {
            return;
        }
        int radius = (int)(WorldInfo.TileSize * ScanTiles);
        Rectangle around = new Rectangle((int)cartEntity.Position2.X - radius, (int)cartEntity.Position2.Y - radius,
            radius * 2, radius * 2);
        collisions.GetEntitiesInRectangleArea(around, into);
        for (int i = into.Count - 1; i >= 0; i--) {
            EntityWrapper item = into[i];
            // Carriable matches what CanBePickedUp calls cargo, so attachments a Cart carries for other reasons never inflate the count
            if (item == null || item.Removed || !item.Mask.HasFlags(Component.Movable)
                || !item.Carriable || item.CarrierId != cartEntity.Id) {
                into.RemoveAt(i);
            }
        }
    }

    private static bool IsSlotted(ServerCart2Controller cart, Guid itemId) {
        var parameters = cart.Parameters;
        if (parameters == null) {
            return false;
        }
        var dictionary = parameters.Dictionary;
        if (dictionary == null || dictionary.Count == 0) {
            return false;
        }
        Span<char> id = stackalloc char[36];
        itemId.TryFormat(id, out _);
        foreach (var pair in dictionary) {
            // our own key holds a packed LIST; with exactly one extra it would equal that Guid and misread as a vanilla slot
            if (string.Equals(pair.Key, CartCargoSync.CargoKey, StringComparison.Ordinal)) {
                continue;
            }
            if (MemoryExtensions.Equals(pair.Value.AsSpan(), id, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        return false;
    }

    private static void Pin(EntityWrapper cartEntity, EntityWrapper item) {
        item.NoEntityCollision = true;
        item.NoTerrainCollision = true;
        item.CarrierId = cartEntity.Id;
    }

    private static void ClearThrowState(EntityWrapper item) {
        item.IsThrown = false;
        item.ThrowerId = null;

        ServerEntityModel model = item.ToServerEntity();
        if (model != null) {
            model.IsThrown = false;
            model.ThrowerId = null;
            model.ThrownCastSpellArgs = null;
            model.KillWhenThrowHit = false;
        }
    }

    private static void Release(EntityWrapper cartEntity, EntityWrapper item) {
        if (item.CarrierId == cartEntity.Id) {
            item.CarrierId = null;
        }
        item.NoEntityCollision = false;
        item.NoTerrainCollision = false;
    }

    // the server flat-stacks every carried item at cart + (0,0,6) and leaves the ring layout to the client; extras follow that split so CartCargoClient owns how they look
    private static void Stack(EntityWrapper cartEntity, EntityWrapper item) {
        item.Position = cartEntity.Position + new Vector3(0f, 0f, StackHeight);
        item.Velocity = cartEntity.Velocity;
        item.System.CollisionGroup.UpdatePositionAndVelocity(item);
    }
}
