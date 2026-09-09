using System.Collections.Generic;
using BepInEx.Configuration;

namespace BetterCarts;

internal static class ModConfig {
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<bool> ChainOverflowEnabled;
    internal static ConfigEntry<bool> DepositRangeEnabled;
    internal static ConfigEntry<int> DepositRange;
    internal static ConfigEntry<bool> CollectRangeEnabled;
    internal static ConfigEntry<int> CollectRange;
    internal static ConfigEntry<bool> ConnectRangeEnabled;
    internal static ConfigEntry<int> ConnectRange;
    internal static ConfigEntry<bool> BucketPriorityEnabled;
    internal static ConfigEntry<bool> CartReleaseFixEnabled;
    internal static ConfigEntry<bool> CartCapacityEnabled;
    internal static ConfigEntry<int> CartCapacityBlessingBonus;
    internal static ConfigEntry<bool> CartCapacityEjectOverflow;
    internal static ConfigEntry<bool> CartOverlaysEnabled;
    internal static ConfigEntry<bool> CartOverlayShowAboveVanilla;
    internal static ConfigEntry<bool> CartOverlayShowVanilla;
    internal static ConfigEntry<bool> CartOverlayShowEmpty;
    internal static ConfigEntry<bool> CartOverlayDisconnectMessage;
    internal static ConfigEntry<bool> StockpileRangeEnabled;
    internal static ConfigEntry<int> StockpileRange;
    internal static ConfigEntry<bool> StockpileWhilePulled;
    internal static ConfigEntry<bool> StockpileWhileParked;
    internal static ConfigEntry<bool> Diagnostics;
    internal static ConfigEntry<bool> DiagSave;
    internal static ConfigEntry<bool> DiagCensus;
    internal static ConfigEntry<bool> DiagCapacity;
    internal static ConfigEntry<bool> DiagPickup;
    internal static ConfigEntry<bool> DiagChain;
    internal static ConfigEntry<bool> DiagStateDump;

    // the environment block dumps every entry this class binds, so a single log answers "what settings" without asking the reporter
    internal static readonly List<ConfigEntryBase> Bound = new List<ConfigEntryBase>();

    internal static void Init(ConfigFile config) {
        Bound.Clear();
        Enabled = Track(config.Bind("General", "Enabled", true,
            new ConfigDescription("Master on/off for the whole mod.", null,
                SectionTag("General", 0), EntryTag("All features", 0))));
        ChainOverflowEnabled = Track(config.Bind("Chain Overflow", "Enabled", true,
            new ConfigDescription("When a full Cart picks up an item, the item is passed to the next Cart in the chain with a free slot.", null,
                SectionTag("Chain Overflow", 1), EntryTag("Pass overflow along the chain", 0))));
        CollectRangeEnabled = Track(config.Bind("Collect Range", "Enabled", true,
            new ConfigDescription("Carts automatically pick up loose items within range.", null,
                SectionTag("Collect Range", 6), EntryTag("Automatic pickup", 0))));
        CollectRange = Track(config.Bind("Collect Range", "Range", 2,
            new ConfigDescription("Collect reach in tiles per side. 0 = vanilla (touch only).",
                new AcceptableValueRange<int>(0, 10),
                EntryTag("Range", 1))));
        DepositRangeEnabled = Track(config.Bind("Deposit Range", "Enabled", true,
            new ConfigDescription("Carts deposit matching cargo into Material Storages within range.", null,
                SectionTag("Deposit Range", 7), EntryTag("Automatic deposit", 0))));
        DepositRange = Track(config.Bind("Deposit Range", "Range", 2,
            new ConfigDescription("Deposit reach in tiles per side, 0 = vanilla (park on the storage).",
                new AcceptableValueRange<int>(0, 10),
                EntryTag("Range", 1))));
        ConnectRangeEnabled = Track(config.Bind("Connect Range", "Enabled", true,
            new ConfigDescription("A free Cart is pulled toward a Cart the player is pulling once it is within range, so they connect without touching.", null,
                SectionTag("Connect Range", 8), EntryTag("Automatic connect", 0))));
        ConnectRange = Track(config.Bind("Connect Range", "Range", 2,
            new ConfigDescription("Connect reach in tiles per side. 0 = vanilla (touch only).",
                new AcceptableValueRange<int>(0, 10),
                EntryTag("Range", 1))));
        BucketPriorityEnabled = Track(config.Bind("Bucket Priority", "Enabled", true,
            new ConfigDescription("When taking an item off a Cart, prefer grabbing an empty Bucket over other cargo.", null,
                SectionTag("Bucket Priority", 2), EntryTag("Prefer empty Buckets", 0))));
        CartReleaseFixEnabled = Track(config.Bind("Cart Release Fix", "Enabled", true,
            new ConfigDescription("Releasing a pulled Cart with the interact key never grabs a different Cart on the same press.", null,
                SectionTag("Cart Release Fix", 3), EntryTag("Release without re-grabbing", 0))));
        CartCapacityEnabled = Track(config.Bind("Cart Capacity", "Enabled", true,
            new ConfigDescription("Sets how many items the vanilla Carts can carry - modded Carts are not supported. A Cart's value is its base capacity, and the Mercury blessing adds the bonus below on top, so 0 means a Cart that carries nothing. High values can cause stutter and stack the cargo into a tall tower above the Cart. Lowering a value stops a Cart picking up more straight away, and the next time you load that world the Cart drops whatever no longer fits in a circle beside itself. In multiplayer the host's values apply to everyone, and every player needs the mod installed to SEE cargo beyond the normal 4. Raising a Cart above its normal capacity is the only thing this mod writes to your save: change the Cart values and the blessing bonus back to vanilla, leave Eject Overflow on, and load each affected world once before uninstalling.", null,
                SectionTag("Cart Capacity", 4), EntryTag("Set capacity per Cart type", 0))));
        CartCapacityEjectOverflow = Track(config.Bind("Cart Capacity", "Eject Overflow", true,
            new ConfigDescription("When a world loads, a Cart carrying more than its capacity drops the surplus in a circle beside itself. Turn this off to leave that cargo on the Cart, where it stays until you unload it by hand.", null,
                EntryTag("Eject Overflow", 1, hidden: !CartCapacityEnabled.Value))));
        CartCapacityBlessingBonus = Track(config.Bind("Cart Capacity", "Blessing Bonus", 1,
            new ConfigDescription("How much the Mercury cart-capacity blessing adds on top of a Cart's base capacity. The default is 1.", new AcceptableValueRange<int>(0, 64),
                EntryTag("Blessing Bonus", 2, hidden: !CartCapacityEnabled.Value))));
        CartCapacity.BindTypeEntries(config);
        CartOverlaysEnabled = Track(config.Bind("Cart Overlays", "Enabled", true,
            new ConfigDescription("Draws extra information above Carts while you play. Every player needs the mod to see it.", null,
                SectionTag("Cart Overlays", 5), EntryTag("Show info above Carts", 0))));
        CartOverlayShowAboveVanilla = Track(config.Bind("Cart Overlays", "Show Above Vanilla Capacity", true,
            new ConfigDescription("Show the cargo count on a Cart carrying more than 5 items.", null,
                EntryTag("Show above vanilla capacity", 1, hidden: !CartOverlaysEnabled.Value))));
        CartOverlayShowVanilla = Track(config.Bind("Cart Overlays", "Show For Vanilla Capacity", false,
            new ConfigDescription("Show the cargo count on a Cart carrying between 1 and 5 items.", null,
                EntryTag("Show for vanilla capacity", 2, hidden: !CartOverlaysEnabled.Value))));
        CartOverlayShowEmpty = Track(config.Bind("Cart Overlays", "Show For Empty Carts", false,
            new ConfigDescription("Show the cargo count on a Cart carrying 0 items.", null,
                EntryTag("Show for empty Carts", 3, hidden: !CartOverlaysEnabled.Value))));
        CartOverlayDisconnectMessage = Track(config.Bind("Cart Overlays", "Disconnect Message", true,
            new ConfigDescription("Show a message above a Cart that comes loose from the chain by itself. Only the player who was pulling that Cart sees it.", null,
                EntryTag("Show a message when a Cart disconnects", 4, hidden: !CartOverlaysEnabled.Value))));
        StockpileRangeEnabled = Track(config.Bind("Stockpile Range", "Enabled", true,
            new ConfigDescription("Carts take resources from building output stockpiles within range. Solid resources go into free slots, bucket resources fill empty Buckets on the Cart.", null,
                SectionTag("Stockpile Range", 9), EntryTag("Take from stockpiles", 0))));
        StockpileRange = Track(config.Bind("Stockpile Range", "Range", 2,
            new ConfigDescription("Stockpile reach in tiles per side. 0 = vanilla (off).",
                new AcceptableValueRange<int>(0, 10),
                EntryTag("Range", 1))));
        StockpileWhilePulled = Track(config.Bind("Stockpile Range", "While Pulled", true,
            new ConfigDescription("Take resources while a player is pulling the Cart or its chain.", null,
                EntryTag("While pulled", 2))));
        StockpileWhileParked = Track(config.Bind("Stockpile Range", "While Parked", false,
            new ConfigDescription("Take resources while the Cart is parked (not pulled by a player).", null,
                EntryTag("While parked", 3))));
        // BepInEx writes the .cfg sorted alphabetically by section, so this name is what puts the section at the bottom of the file; Order 10 puts it last in Mod Settings Menu as well
        Diagnostics = Track(config.Bind("Troubleshooting", "Diagnostics", false,
            new ConfigDescription("ADVANCED. Master switch troubleshooting. Writes what the mod is doing to BepInEx/LogOutput.log and to BetterCarts.log next to the mod file, so a problem can be traced. Changing this takes effect after a restart (new settings will appear too), and while it is off the diagnostic code is never loaded at all. Leave it off unless you are chasing a bug or someone asked you to turn it on.", null,
                SectionTag("Troubleshooting", 10), EntryTag("Write diagnostic logs (needs restart)", 0))));
        DiagSave = Track(config.Bind("Troubleshooting", "Save Watch", true,
            new ConfigDescription("Records each save: how long it took, what changed while it was running, and the full details of any save that fails. About a dozen lines per save. Start here for crashes or freezes while saving.", null,
                EntryTag("Watch saving", 1, hidden: !Diagnostics.Value))));
        DiagCensus = Track(config.Bind("Troubleshooting", "World Census", true,
            new ConfigDescription("Records a periodic count of the entities, Carts and extra cargo in each world. One line every 30 seconds.", null,
                EntryTag("Count the world periodically", 2, hidden: !Diagnostics.Value))));
        DiagCapacity = Track(config.Bind("Troubleshooting", "Cart Capacity", true,
            new ConfigDescription("Records Cart Capacity decisions: which Carts were adopted, what extra cargo was pinned or released, and what was sent to other players. A moderate number of lines while Carts are being loaded and unloaded.", null,
                EntryTag("Trace Cart Capacity", 3, hidden: !Diagnostics.Value))));
        DiagPickup = Track(config.Bind("Troubleshooting", "Cart Pickup", true,
            new ConfigDescription("Records what Carts pick up and put down through Collect Range, Deposit Range and Stockpile Range. Busy while Carts are working.", null,
                EntryTag("Trace pickup and deposit", 4, hidden: !Diagnostics.Value))));
        DiagChain = Track(config.Bind("Troubleshooting", "Cart Chain", true,
            new ConfigDescription("Records Carts connecting, disconnecting and passing overflow along the chain. Busy while you are pulling Carts around.", null,
                EntryTag("Trace the Cart chain", 5, hidden: !Diagnostics.Value))));
        DiagStateDump = Track(config.Bind("Troubleshooting", "Cart State Dump", false,
            new ConfigDescription("Records the complete contents of every Cart, every tick. This produces THOUSANDS of lines per minute and will bury everything else in the log. Only turn it on if you were asked to, and turn it off again straight after.", null,
                EntryTag("Dump full Cart state every tick", 6, hidden: !Diagnostics.Value))));
    }

    private static ConfigEntry<T> Track<T>(ConfigEntry<T> entry) {
        Bound.Add(entry);
        return entry;
    }

    private static object SectionTag(string section, int order) {
        return new { Section = section, DisplayName = section, Order = order };
    }

    internal static object EntryTag(string displayName, int order) {
        return new { DisplayName = displayName, Order = order };
    }

    internal static object EntryTag(string displayName, int order, bool hidden) {
        return new { DisplayName = displayName, Order = order, Hidden = hidden };
    }
}
