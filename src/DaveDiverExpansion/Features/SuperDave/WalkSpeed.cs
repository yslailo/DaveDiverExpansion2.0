using System;
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

/// <summary>
/// Farm walk speed.
///
/// The original SuperDave only patched FarmPlayerView.Setup, so a multiplier changed while
/// already on the farm (e.g. from the F1 panel) never applied until the farm was reloaded.
/// Capture the untouched base speed per instance and re-apply on every Move so the value is live.
/// </summary>
internal static class FarmSpeed
{
    private static readonly Dictionary<int, (float min, float max)> _base = new();
    private static readonly HashSet<int> _logged = new();

    public static void Apply(Farm.FarmPlayerView view)
    {
        if (view == null) return;
        try
        {
            int id = view.GetInstanceID();
            if (!_base.TryGetValue(id, out var b))
            {
                b = (view.m_Speed_Min, view.m_Speed_Max);
                _base[id] = b;
            }

            float mult = SuperDaveCore.Enabled.Value ? WalkSpeed.FarmWalkMultiplier.Value : 0f;
            float min = mult > 0f ? b.min * mult : b.min;
            float max = mult > 0f ? b.max * mult : b.max;

            view.m_Speed_Min = min;
            view.m_Speed_Max = max;

            if (_logged.Add(id))
                Plugin.Debug($"[WalkSpeed] Farm: base=({b.min}, {b.max}) mult={mult} -> ({min}, {max})");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[WalkSpeed] Farm apply error: " + e.Message);
        }
    }
}

[HarmonyPatch(typeof(Farm.FarmPlayerView), "Setup")]
static class WalkSpeedFarmSetupPatch
{
    static void Postfix(Farm.FarmPlayerView __instance) => FarmSpeed.Apply(__instance);
}

[HarmonyPatch(typeof(Farm.FarmPlayerView), "Move")]
static class WalkSpeedFarmMovePatch
{
    static void Postfix(Farm.FarmPlayerView __instance) => FarmSpeed.Apply(__instance);
}

/// <summary>
/// Fish farm walk speed. Like the farm, capture the base speed per instance and re-apply every
/// Move so multiplier changes take effect live instead of only once.
/// </summary>
internal static class FishFarmSpeed
{
    private static readonly Dictionary<int, float> _base = new();

    public static void Apply(FishFarm.FishFarmPlayerView view)
    {
        if (view == null) return;
        try
        {
            int id = view.GetInstanceID();
            if (!_base.TryGetValue(id, out var b))
            {
                b = view.Dave_Speed;
                _base[id] = b;
            }

            float mult = SuperDaveCore.Enabled.Value ? WalkSpeed.FishFarmWalkMultiplier.Value : 0f;
            view.Dave_Speed = mult > 0f ? b * mult : b;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(FishFarm.FishFarmPlayerView), "Move")]
static class WalkSpeedFishFarmPatch
{
    static void Postfix(FishFarm.FishFarmPlayerView __instance) => FishFarmSpeed.Apply(__instance);
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
