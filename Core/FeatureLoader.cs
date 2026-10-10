using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BetterCarts.Patches;
using HarmonyLib;

namespace BetterCarts;

internal static class FeatureLoader {
    private static readonly List<string> FailedNames = new List<string>();

    internal static IReadOnlyList<string> Failed {
        get { return FailedNames; }
    }

    private sealed class Feature {
        internal Feature(string name, ModConfig.LoadSwitch load, params Type[] types)
            : this(name, () => load.Value, load.Fail, types) {
        }

        internal Feature(string name, Func<bool> wanted, Action fail, params Type[] types) {
            Name = name;
            Wanted = wanted;
            Fail = fail;
            Types = types;
        }

        internal readonly string Name;
        internal readonly Func<bool> Wanted;
        internal readonly Action Fail;
        internal readonly Type[] Types;
    }

    internal static void LoadAll() {
        // every feature's [HarmonyPatch] class must be listed here, or it is never applied
        Feature[] features = {
            new Feature("Chain Overflow", ModConfig.LoadChainOverflow, typeof(ChainOverflowPatch), typeof(CartAccess)),
            // Cart Capacity's reach fallback lives in this patch, so it also loads for Cart Capacity
            new Feature("Grab Priority", () => ModConfig.LoadGrabPriority.Value || ModConfig.LoadCartCapacity.Value,
                ModConfig.LoadGrabPriority.Fail, typeof(GrabPriorityPatch)),
            new Feature("Cart Release Fix", ModConfig.LoadCartReleaseFix, typeof(CartReleaseFixPatch)),
            new Feature("Cart Capacity", ModConfig.LoadCartCapacity, typeof(CartCapacityPatch), typeof(CartCapacityClientPatch)),
            new Feature("Cart Overlays", ModConfig.LoadCartOverlays, typeof(CartOverlayPatch), typeof(CartDisconnectPatch),
                typeof(ConstructionSiteMessagePatch)),
            new Feature("Construction Site Protection", ModConfig.LoadConstructionSiteProtection, typeof(ConstructionSiteProtectionPatch)),
            new Feature("Collect Range", ModConfig.LoadCollectRange, typeof(CollectRangePatch), typeof(CartAccess)),
            new Feature("Deposit Range", ModConfig.LoadDepositRange, typeof(DepositRangePatch)),
            new Feature("Connect Range", ModConfig.LoadConnectRange, typeof(ConnectRangePatch)),
            new Feature("Stockpile Range", ModConfig.LoadStockpileRange, typeof(StockpileRangePatch)),
            new Feature("Diagnostic Logs", () => ModConfig.DiagnosticsArmed, () => ModConfig.DiagnosticsArmed = false,
                typeof(DiagnosticsPatch), typeof(ModWatchdog))
        };
        foreach (Feature feature in features) {
            if (feature.Wanted()) {
                Load(feature);
            }
        }
        if (FailedNames.Count > 0) {
            LoadFailureNotice();
        }
    }

    private static void Load(Feature feature) {
        string id = feature.Name.Replace(" ", string.Empty);
        Harmony harmony = new Harmony(BetterCartsPlugin.PluginGuid + "." + id);
        try {
            List<Type> types = new List<Type>();
            foreach (Type type in feature.Types) {
                AddWithNested(type, types);
            }
            // static fields bind private game members by name, so a renamed member fails here instead of every tick
            foreach (Type type in types) {
                if (!type.ContainsGenericParameters) {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                }
            }
            foreach (Type type in types) {
                harmony.CreateClassProcessor(type).Patch();
            }
        }
        catch (Exception ex) {
            try {
                harmony.UnpatchSelf();
            }
            catch (Exception unpatchEx) {
                ModLog.Fault("FeatureLoader." + id + ".Unpatch", unpatchEx);
            }
            feature.Fail();
            FailedNames.Add(feature.Name);
            ModLog.Error(feature.Name + " failed to load and is turned off for this session, usually because a game update"
                + " changed what it patches. The other features are not affected; the cause follows:");
            ModLog.Fault("FeatureLoader." + id, ex);
        }
    }

    private static void LoadFailureNotice() {
        Harmony harmony = new Harmony(BetterCartsPlugin.PluginGuid + ".FailureNotice");
        try {
            List<Type> types = new List<Type>();
            AddWithNested(typeof(FailureNoticePatch), types);
            foreach (Type type in types) {
                harmony.CreateClassProcessor(type).Patch();
            }
        }
        catch (Exception ex) {
            ModLog.Fault("FeatureLoader.FailureNotice", ex);
        }
    }

    private static void AddWithNested(Type type, List<Type> types) {
        types.Add(type);
        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)) {
            AddWithNested(nested, types);
        }
    }
}
