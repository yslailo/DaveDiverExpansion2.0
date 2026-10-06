using System.Collections.Generic;
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
    public static ConfigEntry<bool> InfiniteDrinkPatience;
    public static ConfigEntry<float> MoneyBoost;
    public static ConfigEntry<bool> InfiniteWasabi;
    public static ConfigEntry<float> StaffCookMultiplier;

    public static void Init(ConfigFile config)
    {
        InfiniteCustomerPatience = config.Bind(
            "Sushi", "Sushi - Infinite Customer Patience", false,
            "Set to true to freeze the food-wait patience so customers never storm off because food is too slow. " +
            "Implemented by skipping the food patience tickers (WaitForServing / WaitForAngry). " +
            "WaitingSpeedParameter is deliberately left untouched so walk speeds are never affected.");
        InfiniteDrinkPatience = config.Bind(
            "Sushi", "Sushi - Infinite Drink Patience", false,
            "Also freeze the drink-wait patience ticker (WaitForDrinkServing) so a drink order can never time out " +
            "into a disappointment. Requires 'Sushi - Infinite Customer Patience'. Leave false to keep retail behaviour.");
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

    /// <summary>Food-wait patience tickers (WaitForServing / WaitForAngry) must be skipped this frame.</summary>
    public static bool FreezeFoodPatience
    {
        get
        {
            try { return SuperDaveCore.Enabled.Value && InfiniteCustomerPatience.Value; }
            catch { return false; }
        }
    }

    /// <summary>Drink-wait patience ticker (WaitForDrinkServing) must be skipped this frame.</summary>
    public static bool FreezeDrinkPatience
    {
        get
        {
            try { return SuperDaveCore.Enabled.Value && InfiniteCustomerPatience.Value && InfiniteDrinkPatience.Value; }
            catch { return false; }
        }
    }

    /// <summary>
    /// Force IL2CPP class initialisation of every type we patch, BEFORE Harmony.PatchAll() runs.
    ///
    /// Il2CppInterop resolves a patch target's declaring type lazily, which runs
    /// il2cpp_runtime_class_init. If that happens while a detour is being installed, the IL2CPP
    /// invoke dispatcher can mis-resolve the target and re-enter the patched method forever
    /// (fatal stack overflow, process dies before "Patches applied"). Initialising the classes
    /// up front moves that class-init work out of the patching window.
    /// </summary>
    public static void PreInitTypes()
    {
        System.Type[] types =
        {
            typeof(SushiBarCustomer),
            typeof(SushiBarContext),
            typeof(SushiBarContext.WasabiGratersData),
            typeof(SushiBarStaffBase),
        };
        foreach (var t in types)
        {
            try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(t.TypeHandle); }
            catch { }
        }
    }
}

// ============================================================================
// Infinite patience (2.0.4)
//
// Four tickers advance the shared impatience gauge with
//     [customer + 0xBC] += Time.deltaTime * WaitingSpeedParameter
// and fire their "gave up / timed out" callback once the gauge reaches the max:
//     WaitForOrder (等点单) / WaitForAngry (忍耐) / WaitForServing (等上菜) / WaitForDrinkServing (等饮料)
//
// We freeze the FOOD ones (WaitForServing / WaitForAngry) by skipping the whole
// tick method, so the gauge stays at 0 and the timeout branch never runs.
// WaitingSpeedParameter itself is NEVER written: Move() multiplies the walk-out
// speed by it, so a stale 0 there would strand the customer mid-floor.
// ============================================================================

[HarmonyPatch(typeof(SushiBarCustomer), "WaitForServing")]
static class SushiFreezeWaitForServingPatch
{
    static bool Prefix() => !SushiBarTweaks.FreezeFoodPatience;
}

[HarmonyPatch(typeof(SushiBarCustomer), "WaitForAngry")]
static class SushiFreezeWaitForAngryPatch
{
    static bool Prefix() => !SushiBarTweaks.FreezeFoodPatience;
}

[HarmonyPatch(typeof(SushiBarCustomer), "WaitForDrinkServing")]
static class SushiFreezeWaitForDrinkPatch
{
    static bool Prefix() => !SushiBarTweaks.FreezeDrinkPatience;
}

[HarmonyPatch(typeof(SushiBarCustomer), "LateUpdate")]
static class SushiBarCustomerPatch
{
    static void Postfix(SushiBarCustomer __instance)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value)
            {
                // Infinite patience is implemented in the WaitForServing / WaitForAngry
                // ticker prefixes above. Do NOT touch WaitingSpeedParameter here: Move()
                // multiplies the walk-out speed by it, so a stale 0 strands customers.
                if (SushiBarTweaks.MoneyBoost.Value > 0f)
                    __instance.RevenueBuffParameter = SushiBarTweaks.MoneyBoost.Value;
            }
        }
        catch { }
    }
}

// WARNING: never add a Harmony patch for an EMPTY IL2CPP method (a native body that is just
// "ret 0"). IL2CPP folds identical empty bodies onto one shared stub; patching one of them
// corrupts the invoke dispatcher for that stub and makes it re-enter the patched method
// forever -> fatal stack overflow during PatchAll. SushiBarCustomer.WalkOut() and
// SushiBarCustomer.AfterEatingBehavior() are both empty and must NOT be patched.
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
