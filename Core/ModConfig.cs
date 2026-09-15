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
    internal static ConfigEntry<bool> DiagMemory;
    internal static ConfigEntry<bool> DiagCensus;
    internal static ConfigEntry<bool> DiagCapacity;
    internal static ConfigEntry<bool> DiagPickup;
    internal static ConfigEntry<bool> DiagChain;
    internal static ConfigEntry<bool> DiagStateDump;
    internal static ConfigEntry<bool> DiagLogFile;

    // the environment block dumps every entry this class binds, so a single log answers "what settings" without asking the reporter
    internal static readonly List<ConfigEntryBase> Bound = new List<ConfigEntryBase>();

    internal static void Init(ConfigFile config) {
        Bound.Clear();
        Enabled = Track(config.Bind("General", "Enabled", true,
            new ConfigDescription("Enables all mod features.", null,
        SectionTag("General", 0), EntryTag("All features", 0))));
        ChainOverflowEnabled = Track(config.Bind("Chain Overflow", "Enabled", true,
            new ConfigDescription("Passes items picked up by a full Cart to the next Cart in the chain with a free slot.", null,
                SectionTag("Chain Overflow", 1), EntryTag("Pass overflow along the chain", 0))));
        CollectRangeEnabled = Track(config.Bind("Collect Range", "Enabled", true,
            new ConfigDescription("Carts automatically pick up loose items within range.", null,
                SectionTag("Collect Range", 6), EntryTag("Automatic pickup", 0))));
        CollectRange = Track(config.Bind("Collect Range", "Range", 2,
            new ConfigDescription("Pickup range in tiles per side. 0 = vanilla (touch only).",
                new AcceptableValueRange<int>(0, 10),
                EntryTag("Range", 1))));
        DepositRangeEnabled = Track(config.Bind("Deposit Range", "Enabled", true,
            new ConfigDescription("Carts deposit matching cargo into Material Storages within range.", null,
                SectionTag("Deposit Range", 7), EntryTag("Automatic deposit", 0))));
        DepositRange = Track(config.Bind("Deposit Range", "Range", 2,
            new ConfigDescription("Deposit range in tiles per side. 0 = vanilla (park on the storage).",
                new AcceptableValueRange<int>(0, 10),
                EntryTag("Range", 1))));
        ConnectRangeEnabled = Track(config.Bind("Connect Range", "Enabled", true,
            new ConfigDescription("Pulls nearby free Carts toward the Cart you are pulling to connect them without touching.", null,
                SectionTag("Connect Range", 8), EntryTag("Automatic connect", 0))));
        ConnectRange = Track(config.Bind("Connect Range", "Range", 2,
            new ConfigDescription("Connection range in tiles per side. 0 = vanilla (touch only).",
                new AcceptableValueRange<int>(0, 10),
                EntryTag("Range", 1))));
        BucketPriorityEnabled = Track(config.Bind("Bucket Priority", "Enabled", true,
            new ConfigDescription("Prioritizes empty Buckets when taking items from a Cart.", null,
                SectionTag("Bucket Priority", 2), EntryTag("Prefer empty Buckets", 0))));
        CartReleaseFixEnabled = Track(config.Bind("Cart Release Fix", "Enabled", true,
            new ConfigDescription("Prevents grabbing another Cart with the same interact press used to release a Cart.", null,
                SectionTag("Cart Release Fix", 3), EntryTag("Release without re-grabbing", 0))));
        CartCapacityEnabled = Track(config.Bind("Cart Capacity", "Enabled", true,
            new ConfigDescription("Sets base capacity for vanilla Carts only. The Mercury blessing adds the configured bonus. High capacities may cause stutter and tall cargo stacks. Lowering capacity immediately blocks further pickup while full; excess cargo is ejected on world load if Eject Overflow is enabled. In multiplayer, host settings apply; all players need the mod to see cargo beyond the normal 4 items. Extra capacity affects saved Cart data. Before uninstalling, restore vanilla capacities and blessing bonus, enable Eject Overflow, and load each affected world once.", null,
                SectionTag("Cart Capacity", 4), EntryTag("Set capacity per Cart type", 0))));
        CartCapacityEjectOverflow = Track(config.Bind("Cart Capacity", "Eject Overflow", true,
            new ConfigDescription("Drops excess cargo beside Carts when a world loads. If disabled, excess cargo remains until manually unloaded.", null,
                EntryTag("Eject Overflow", 1, hidden: !CartCapacityEnabled.Value))));
        CartCapacityBlessingBonus = Track(config.Bind("Cart Capacity", "Blessing Bonus", 1,
            new ConfigDescription("Extra capacity granted by the Mercury blessing.", new AcceptableValueRange<int>(0, 64),
                EntryTag("Blessing Bonus", 2, hidden: !CartCapacityEnabled.Value))));
        CartCapacity.BindTypeEntries(config);
        CartOverlaysEnabled = Track(config.Bind("Cart Overlays", "Enabled", true,
            new ConfigDescription("Shows information above Carts. Visible only to players with the mod installed.", null,
                SectionTag("Cart Overlays", 5), EntryTag("Show info above Carts", 0))));
        CartOverlayShowAboveVanilla = Track(config.Bind("Cart Overlays", "Show Above Vanilla Capacity", true,
            new ConfigDescription("Shows the cargo count on Carts carrying more than 5 items.", null,
                EntryTag("Show above vanilla capacity", 1, hidden: !CartOverlaysEnabled.Value))));
        CartOverlayShowVanilla = Track(config.Bind("Cart Overlays", "Show For Vanilla Capacity", false,
            new ConfigDescription("Shows the cargo count on Carts carrying 1–5 items.", null,
                EntryTag("Show for vanilla capacity", 2, hidden: !CartOverlaysEnabled.Value))));
        CartOverlayShowEmpty = Track(config.Bind("Cart Overlays", "Show For Empty Carts", false,
            new ConfigDescription("Shows the cargo count on empty Carts.", null,
                EntryTag("Show for empty Carts", 3, hidden: !CartOverlaysEnabled.Value))));
        CartOverlayDisconnectMessage = Track(config.Bind("Cart Overlays", "Disconnect Message", true,
            new ConfigDescription("Notifies the pulling player when a Cart disconnects from the chain by itself.", null,
                EntryTag("Show a message when a Cart disconnects", 4, hidden: !CartOverlaysEnabled.Value))));
        StockpileRangeEnabled = Track(config.Bind("Stockpile Range", "Enabled", true,
            new ConfigDescription("Carts collect resources from building output stockpiles within range. Solid resources use free slots; bucket resources fill empty Buckets.", null,
                SectionTag("Stockpile Range", 9), EntryTag("Take from stockpiles", 0))));
        StockpileRange = Track(config.Bind("Stockpile Range", "Range", 2,
            new ConfigDescription("Stockpile collection range in tiles per side. 0 = disabled.",
                new AcceptableValueRange<int>(0, 10),
                EntryTag("Range", 1))));
        StockpileWhilePulled = Track(config.Bind("Stockpile Range", "While Pulled", true,
            new ConfigDescription("Collects from stockpiles while a player pulls the Cart or its chain.", null,
                EntryTag("While pulled", 2))));
        StockpileWhileParked = Track(config.Bind("Stockpile Range", "While Parked", false,
            new ConfigDescription("Collects from stockpiles while the Cart is not being pulled.", null,
                EntryTag("While parked", 3))));

        // BepInEx writes the .cfg sorted alphabetically by section, so this name is what puts the section at the bottom of the file; Order 10 puts it last in Mod Settings Menu as well
        Diagnostics = Track(config.Bind("Troubleshooting", "Diagnostics", false,
            new ConfigDescription("Enables diagnostic logging to BepInEx/LogOutput.log. Requires a restart to apply and show diagnostic settings.", null,
        SectionTag("Troubleshooting", 10), EntryTag("Write diagnostic logs (needs restart)", 0))));
        DiagSave = Track(config.Bind("Troubleshooting", "Save Watch", true,
            new ConfigDescription("Logs save duration, changes during saving and save failures.", null,
                EntryTag("Watch saving", 1, hidden: !Diagnostics.Value))));
        // Runs a background thread for the whole session.
        DiagMemory = Track(config.Bind("Troubleshooting", "Memory Watch", false,
            new ConfigDescription("Logs memory usage during saves, game pauses and stalls using a background thread. Requires a restart.", null,
                EntryTag("Watch memory and stalls (needs restart)", 2, hidden: !Diagnostics.Value))));
        DiagCensus = Track(config.Bind("Troubleshooting", "World Census", true,
            new ConfigDescription("Logs entity, Cart and extra cargo counts in each world every 30 seconds.", null,
                EntryTag("Count the world periodically", 3, hidden: !Diagnostics.Value))));
        DiagCapacity = Track(config.Bind("Troubleshooting", "Cart Capacity", true,
            new ConfigDescription("Logs Cart adoption, extra cargo pinning and release, and updates sent to other players.", null,
                EntryTag("Trace Cart Capacity", 4, hidden: !Diagnostics.Value))));
        DiagPickup = Track(config.Bind("Troubleshooting", "Cart Pickup", true,
            new ConfigDescription("Logs pickups and deposits through Collect Range, Deposit Range and Stockpile Range.", null,
                EntryTag("Trace pickup and deposit", 5, hidden: !Diagnostics.Value))));
        DiagChain = Track(config.Bind("Troubleshooting", "Cart Chain", true,
            new ConfigDescription("Logs Cart connections, disconnections and overflow transfers along the chain.", null,
                EntryTag("Trace the Cart chain", 6, hidden: !Diagnostics.Value))));
        DiagStateDump = Track(config.Bind("Troubleshooting", "Cart State Dump", false,
            new ConfigDescription("Logs every Cart's complete contents every tick. Produces thousands of lines per minute; enable only for targeted troubleshooting.", null,
                EntryTag("Dump full Cart state every tick", 7, hidden: !Diagnostics.Value))));
        // Diagnostic output still reaches BepInEx/LogOutput.log when this is disabled.
        DiagLogFile = Track(config.Bind("Troubleshooting", "Log File", false,
            new ConfigDescription("Also writes diagnostics to BetterCarts.log next to the mod. Writes continuously to disk. Requires a restart.", null,
                EntryTag("Also write a BetterCarts.log file (needs restart)", 8, hidden: !Diagnostics.Value))));
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
