using System.Collections.Generic;
using BepInEx.Configuration;
using Common.Contents;
using DR;
using HarmonyLib;

namespace DaveDiverExpansion.Features.SuperDave;

/// <summary>
/// Walk / move speed modifiers ported from SuperDave:
/// boat, farm, fish farm and sushi bar.
/// </summary>
public static class WalkSpeed
{
    public static ConfigEntry<float> BoatWalkSpeedBoost;
    public static ConfigEntry<float> FarmWalkMultiplier;
    public static ConfigEntry<float> FishFarmWalkMultiplier;
    public static ConfigEntry<float> SushiSpeedBoost;
    public static ConfigEntry<float> SushiStaffWalkMultiplier;

    public static void Init(ConfigFile config)
    {
        BoatWalkSpeedBoost = config.Bind(
            "Boat", "Boat - Walk Speed Boost", 0f,
            "Speed boost applied to Dave when walking on the boat (float, default 0f [set to 0 to disable]).");
        FarmWalkMultiplier = config.Bind(
            "Farm", "Farm - Walk Multiplier", 0f,
            "Multiplier applied to Dave when walking/sprinting on the farm (float, default 0f [< 1 == faster, > 1 == slower, set to 0 to disable]). " +
            "[NOTE: values above ~5 can let Dave walk off screen and get stuck].");
        FishFarmWalkMultiplier = config.Bind(
            "FishFarm", "FishFarm - Walk Multiplier", 0f,
            "Multiplier applied to Dave when walking/sprinting on the fish farm (float, default 0f [set to 0 to disable]).");
        SushiSpeedBoost = config.Bind(
            "Sushi", "Sushi - Speed Boost", 0f,
            "Permanent speed boost when working in the sushi bar (float, default 0f [set to 0 to disable]).");
        SushiStaffWalkMultiplier = config.Bind(
            "Sushi", "Sushi - Staff Walk Multiplier", 0f,
            "Multiplier applied to sushi bar staff walking speed [also affects Dave on top of 'Sushi - Speed Boost'] (float, default 0f [set to 0 to disable]).");
    }
}

[HarmonyPatch(typeof(LobbyPlayer), "LateUpdate")]
static class WalkSpeedBoatPatch
{
    private static readonly HashSet<int> _patched = new();

    static void Postfix(LobbyPlayer __instance)
    {
        try
        {
            if (!SuperDaveCore.Enabled.Value || WalkSpeed.BoatWalkSpeedBoost.Value <= 0f) return;
            int id = __instance.GetInstanceID();
            if (!_patched.Add(id)) return;
            __instance.m_MoveSpeed += WalkSpeed.BoatWalkSpeedBoost.Value;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(Farm.FarmPlayerView), "Setup")]
static class WalkSpeedFarmPatch
{
    static void Postfix(Farm.FarmPlayerView __instance)
    {
        try
        {
            if (!SuperDaveCore.Enabled.Value || WalkSpeed.FarmWalkMultiplier.Value <= 0f) return;
            __instance.m_Speed_Min *= WalkSpeed.FarmWalkMultiplier.Value;
            __instance.m_Speed_Max *= WalkSpeed.FarmWalkMultiplier.Value;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(FishFarm.FishFarmPlayerView), "Move")]
static class WalkSpeedFishFarmPatch
{
    private static readonly HashSet<int> _patched = new();

    static void Postfix(FishFarm.FishFarmPlayerView __instance)
    {
        try
        {
            if (!SuperDaveCore.Enabled.Value || WalkSpeed.FishFarmWalkMultiplier.Value <= 0f) return;
            int id = __instance.GetInstanceID();
            if (!_patched.Add(id)) return;
            __instance.Dave_Speed *= WalkSpeed.FishFarmWalkMultiplier.Value;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(DaveMoveValue), "speedMultiplier", MethodType.Getter)]
static class WalkSpeedSushiPatch
{
    static bool Prefix(ref float __result)
    {
        try
        {
            if (!SuperDaveCore.Enabled.Value || WalkSpeed.SushiSpeedBoost.Value <= 0f) return true;
            __result = WalkSpeed.SushiSpeedBoost.Value;
            return false;
        }
        catch { }
        return true;
    }
}

[HarmonyPatch(typeof(SushiBarStaffBase), "CalcMoveSpeed")]
static class WalkSpeedSushiStaffPatch
{
    static void Postfix(ref float __result)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && WalkSpeed.SushiStaffWalkMultiplier.Value > 0f)
                __result *= WalkSpeed.SushiStaffWalkMultiplier.Value;
        }
        catch { }
    }
}
