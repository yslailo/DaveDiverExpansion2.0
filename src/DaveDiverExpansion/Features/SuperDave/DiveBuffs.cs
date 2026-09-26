using BepInEx.Configuration;
using DR;
using HarmonyLib;

namespace DaveDiverExpansion.Features.SuperDave;

/// <summary>
/// Diving buffs ported from SuperDave:
/// infinite oxygen, invincibility, weightless items, no item popups,
/// swim speed boost and infinite bullets.
/// </summary>
public static class DiveBuffs
{
    public static ConfigEntry<bool> InfiniteOxygen;
    public static ConfigEntry<bool> Invincible;
    public static ConfigEntry<bool> WeightlessItems;
    public static ConfigEntry<bool> DisableItemPopups;
    public static ConfigEntry<float> SpeedBoost;
    public static ConfigEntry<bool> InfiniteBullets;

    public static void Init(ConfigFile config)
    {
        InfiniteOxygen = config.Bind(
            "Diving", "Diving - Infinite Oxygen", false,
            "Set to true to have infinite oxygen when diving.");
        Invincible = config.Bind(
            "Diving", "Diving - Invincible", false,
            "Set to true to take no damage when hit.");
        WeightlessItems = config.Bind(
            "Diving", "Diving - Weightless Items", false,
            "Set to true to reduce the weight of all items to 0 (infinite carry weight).");
        DisableItemPopups = config.Bind(
            "Diving", "Diving - Disable Item Info Popups", false,
            "Set to true to disable the item info popup windows (they get far behind when auto-pickup is on).");
        SpeedBoost = config.Bind(
            "Diving", "Diving - Speed Boost", 0f,
            "Permanent swim speed boost when diving (float, default 0f [set to 0 to disable]).");
        InfiniteBullets = config.Bind(
            "Diving", "Diving - Infinite Bullets", false,
            "Set to true to have infinite bullets when diving.");
    }
}

[HarmonyPatch(typeof(PlayerBreathHandler), "Update")]
static class DiveBuffsBreathPatch
{
    static bool Prefix(PlayerBreathHandler __instance)
    {
        try
        {
            if (!SuperDaveCore.Enabled.Value) return true;
            if (DiveBuffs.InfiniteOxygen.Value) __instance.SetBreathInvincible(true);
            if (DiveBuffs.Invincible.Value) __instance.SetDamageInvicible(true);
        }
        catch { }
        return true;
    }
}

[HarmonyPatch(typeof(PlayerCharacter), "SetHPDamage")]
static class DiveBuffsHpDamagePatch
{
    static bool Prefix()
    {
        try
        {
            return !(SuperDaveCore.Enabled.Value && DiveBuffs.Invincible.Value);
        }
        catch { }
        return true;
    }
}

/// <summary>
/// Weightless items (infinite carry weight).
///
/// IMPORTANT: this feature must never write to shared/stored data (e.g. Items.ItemWeight or
/// IntegratedItem.ItemWeight), because those writes persist for the whole game session and
/// cannot be undone by toggling the option off. Instead every layer intercepts at read/compute
/// time and simply *returns* 0 while the option is on, so turning it off restores normal weights
/// immediately:
///   - DR.Items.get_ItemWeight            (item definition weight reads)
///   - IntegratedItem.get_ItemWeight      (inventory entry weight reads)
///   - DataManager.CalcModifiedWeight     (per-catch weight used when adding to the dive bag)
///   - LootBox weight / overweight state  (the actual dive-bag capacity)
///   - LootsInfoPanel display
/// </summary>
[HarmonyPatch(typeof(Items), "get_ItemWeight")]
static class DiveBuffsWeightlessItemsDefPatch
{
    static void Postfix(ref float __result)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && DiveBuffs.WeightlessItems.Value)
                __result = 0f;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(IntegratedItem), "get_ItemWeight")]
static class DiveBuffsWeightlessItemWeightPatch
{
    static void Postfix(ref float __result)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && DiveBuffs.WeightlessItems.Value)
                __result = 0f;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(DataManager), "CalcModifiedWeight")]
static class DiveBuffsWeightlessCalcPatch
{
    static void Postfix(ref float __result)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && DiveBuffs.WeightlessItems.Value)
                __result = 0f;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(LootBox), "RefreshWeight")]
static class DiveBuffsWeightlessLootRefreshPatch
{
    static void Postfix(LootBox __instance)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && DiveBuffs.WeightlessItems.Value && __instance != null)
                Helpers.Il2CppReflection.SetFieldValue(__instance, "_weight_k__BackingField", 0f);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(LootBox), "get_isOverweightState")]
static class DiveBuffsWeightlessOverweightPatch
{
    static void Postfix(ref bool __result)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && DiveBuffs.WeightlessItems.Value)
                __result = false;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(LootBox), "CheckOverloadedState")]
static class DiveBuffsWeightlessCheckPatch
{
    static bool Prefix(ref bool __result)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && DiveBuffs.WeightlessItems.Value)
            {
                __result = false;
                return false;
            }
        }
        catch { }
        return true;
    }
}

[HarmonyPatch(typeof(LootsInfoPanel), "UpdateWeight")]
static class DiveBuffsWeightlessUIPatch
{
    static void Prefix(ref float now, ref float max)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && DiveBuffs.WeightlessItems.Value)
            {
                now = 0f;
                max = 999999f;
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(GetInfoPanelUI), "WaitOnPopup")]
static class DiveBuffsItemPopupPatch
{
    static void Postfix(GetInfoPanelUI __instance, GetInfoPanelUI.GetItemInfo info)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && DiveBuffs.DisableItemPopups.Value)
                __instance.gameObject.SetActive(false);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(BuffHandler), "Start")]
static class DiveBuffsSpeedPatch
{
    static void Postfix(BuffHandler __instance)
    {
        try
        {
            if (SuperDaveCore.Enabled.Value && DiveBuffs.SpeedBoost.Value > 0f
                && __instance.gameObject.name == "DaveCharacter")
            {
                __instance.GetBuffComponents.AddMoveSpeedParam(1234567, DiveBuffs.SpeedBoost.Value);
            }
        }
        catch { }
    }
}
