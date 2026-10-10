using System.Runtime.Versioning;
using BepInEx;
using BepInEx.NET.Common;

[assembly: RequiresPreviewFeatures]

namespace BetterCarts;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class BetterCartsPlugin : BasePlugin {
    public const string PluginGuid = "com.beesq.romestead.bettercarts";
    public const string PluginName = "Better Carts";
    public const string PluginVersion = "1.5.0";

    public override void Load() {
        ModLog.Init(Log);
        ModConfig.Init(Config);
        FeatureLoader.LoadAll();
        MsmIntegration.Init(Log, Config);
        Log.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        CartCapacity.LogStartup();
    }
}
