using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using DaveDiverExpansion.Features;
using DaveDiverExpansion.Features.SuperDave;

namespace DaveDiverExpansion;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    internal static ConfigEntry<bool> DebugLog;
    private Harmony _harmony;

    public override void Load()
    {
        Log = base.Log;
        DebugLog = Config.Bind(
            "Debug", "DebugLog", false,
            "Enable verbose debug logging for all features");
        Log.LogInfo($"Loading {MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION}");

        // Initialize features
        AutoPickup.Init(Config);
        DiveMap.Init(Config);
        QuickSceneSwitch.Init(Config);
        iDiverExtension.Init(Config);
        AutoSeahorseRace.Init(Config);
        BettingExpansion.Init(Config);

        // SuperDave 3.0 features (ported from SuperDave / SuperDave 2.0)
        SuperDaveCore.Init(Config);
        WalkSpeed.Init(Config);
        DiveBuffs.Init(Config);
        DroneTrap.Init(Config);
        ToxicAura.Init(Config);
        AutoCallDrone.Init(Config);
        SushiBarTweaks.Init(Config);
        HarpoonHead.Init(Config);
        WeaponControl.Init(Config);
        SuperDaveHotkeys.Init(Config);

        AuraHud.Init(Config); // status HUD (aura + common toggles)

        ConfigUI.Init(Config); // Must be after other features so it discovers their ConfigEntries
        // Apply Harmony patches
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();

        Log.LogInfo("Plugin loaded. Patches applied.");
    }
}

internal static class MyPluginInfo
{
    public const string PLUGIN_GUID = "com.davediver.expansion2.0";
    public const string PLUGIN_NAME = "dave-diver-expansion2.0";
    public const string PLUGIN_VERSION = "2.0.2";
}

