using BepInEx.Configuration;
using DR;
using HarmonyLib;
using SushiBar.Customer;

namespace DaveDiverExpansion.Features.SuperDave;

/// <summary>
/// Sushi bar tweaks ported from SuperDave:
/// infinite customer patience, money boost, infinite wasabi and faster staff cooking.
/// </summary>
public static class SushiBarTweaks
{
    public static ConfigEntry<bool> InfiniteCustomerPatience;
    public static ConfigEntry<float> MoneyBoost;
    public static ConfigEntry<bool> InfiniteWasabi;
    public static ConfigEntry<float> StaffCookMultiplier;

    public static void Init(ConfigFile config)
    {
        InfiniteCustomerPatience = config.Bind(
            "Sushi", "Sushi - Infinite Customer Patience", false,
            "Set to true to make customers never storm off if food/drinks are too slow.");
        MoneyBoost = config.Bind(
            "Sushi", "Sushi - Money Boost", 0f,
            "Money boost applied to all customer purchases at the sushi bar (float, default 0f [set to 0 to disable]).");
        InfiniteWasabi = config.Bind(
            "Sushi", "Sushi - Infinite Wasabi", false,
            "Set to true to never need to refill wasabi.");
        StaffCookMultiplier = config.Bind(
            "Sushi", "Sushi - Cook Multiplier", 0f,
            "Multiplier applied to sushi bar staff cooking speed (float, default 0f [> 1 == faster, set to 0 to disable]).");
    }
}

[HarmonyPatch(typeof(SushiBarCustomer), "LateUpdate")]
static class SushiBarCustomerPatch
{
    static void Postfix(SushiBarCustomer __instance)
    {
        try
        {
            if (!SuperDaveCore.Enabled.Value) return;
            if (SushiBarTweaks.InfiniteCustomerPatience.Value)
                __instance.WaitingSpeedParameter = (__instance.IsOrderWait || __instance.IsOrderWaitDrink) ? 0f : 1f;
            if (SushiBarTweaks.MoneyBoost.Value > 0f)
                __instance.RevenueBuffParameter = SushiBarTweaks.MoneyBoost.Value;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(SushiBarContext.WasabiGratersData), "UpdateWasabiCount")]
static class SushiBarWasabiPatch
{
    static bool Prefix(ref int count)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && SushiBarTweaks.InfiniteWasabi.Value)
                count = 0;
        }
        catch { }
        return true;
    }
}

[HarmonyPatch(typeof(SushiBarStaffBase), "CalcCookingTime")]
static class SushiBarCookTimePatch
{
    static void Postfix(ref float __result)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && SushiBarTweaks.StaffCookMultiplier.Value > 0f)
                __result /= SushiBarTweaks.StaffCookMultiplier.Value;
        }
        catch { }
    }
}

