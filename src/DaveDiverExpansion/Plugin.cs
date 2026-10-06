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

    /// <summary>Verbose logging, gated behind the global "DebugLog" option (off by default).</summary>
    internal static void Debug(string message)
    {
        try { if (DebugLog != null && DebugLog.Value) Log.LogInfo(message); }
        catch { }
    }

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

        // When debug logging is enabled, dump the effective config so a single reporter log is
        // self-contained (which [Sushi]/[SuperDave]/speed features were actually on).
        if (DebugLog.Value)
        {
            try
            {
                Log.LogInfo("==== [Config] effective values ====");
                foreach (var kv in Config)
                {
                    try { Log.LogInfo($"[Config] {kv.Key.Section}/{kv.Key.Key} = {kv.Value.BoxedValue}"); }
                    catch { }
                }
                Log.LogInfo("==== [Config] end ====");
            }
            catch { }
        }

        // Force IL2CPP class init of every type we patch BEFORE installing detours. Doing this
        // lazily during PatchAll() lets the invoke dispatcher re-enter a half-installed detour
        // and fatally recurse (see SushiBarTweaks.PreInitTypes).
        SushiBarTweaks.PreInitTypes();

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
    public const string PLUGIN_VERSION = "2.0.4";
}

