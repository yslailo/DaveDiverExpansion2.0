using BepInEx.Configuration;
using HarmonyLib;

namespace DaveDiverExpansion.Features.SuperDave;

/// <summary>
/// Infinite salvage drones and crab traps (ported from SuperDave).
/// Auto-drop crab traps is handled inside AutoPickup.
/// </summary>
public static class DroneTrap
{
    public static ConfigEntry<bool> InfiniteDrones;
    public static ConfigEntry<bool> InfiniteCrabTraps;

    public static void Init(ConfigFile config)
    {
        InfiniteDrones = config.Bind(
            "Diving", "Diving - Infinite Drones", false,
            "Set to true to enable infinite salvage drones.");
        InfiniteCrabTraps = config.Bind(
            "Diving", "Diving - Infinite Crab Traps", false,
            "Set to true to enable infinite crab traps (the popup still shows 0 but you can keep dropping them).");
    }
}

[HarmonyPatch(typeof(PlayerCharacter), "IsCrabTrapAvailable", MethodType.Getter)]
static class DroneTrapCrabTrapPatch
{
    static void Postfix(ref bool __result)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && DroneTrap.InfiniteCrabTraps.Value)
                __result = true;
        }
        catch { }
    }
}
