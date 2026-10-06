using System;
using BepInEx.Configuration;
using DR;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

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
/// IMPORTANT #1 — this feature must never write to shared/stored data (e.g. Items.ItemWeight or
/// IntegratedItem.ItemWeight), because those writes persist for the whole game session and
/// cannot be undone by toggling the option off. Instead every layer intercepts at read/compute
/// time and simply *returns* 0 while the option is on, so turning it off restores normal weights
/// immediately:
///   - DR.Items.get_ItemWeight            (item definition weight reads)
///   - IntegratedItem.get_ItemWeight      (inventory entry weight reads)
///   - DataManager.CalcModifiedWeight     (per-catch weight used when adding to the dive bag)
///   - LootBox weight / overweight state  (the actual dive-bag capacity)
///   - LootsInfoPanel display
///
/// IMPORTANT #2 — IL2CPP IDENTICAL-CODE-FOLDING (the reason this file type-checks the instance).
/// The Unity IL2CPP toolchain merges byte-identical method bodies onto a SINGLE native address.
/// These two trivial getters collide with unrelated movement getters:
///     DR.Items.get_ItemWeight()                        ==  movss xmm0,[rcx+4Ch]; ret
///     IndependentMovableHelper.get_GetCurrentSpeed()   ==  movss xmm0,[rcx+4Ch]; ret
///     IntegratedItem.get_ItemWeight()                  ==  movss xmm0,[rcx+48h]; ret
///     IndependentMovableHelper.get_GetCurrentRotationSpeed() == movss xmm0,[rcx+48h]; ret
/// (IndependentMovableHelper is the base class of the fish-farm fish movers, and its fields
///  +0x4C / +0x48 hold the current swim / rotation speed.)
/// A naive "always zero" postfix therefore ALSO forced every fish's speed to 0 — e.g. all
/// fish-farm fish froze in place the moment "Weightless Items" was enabled.
/// Fix: the postfix reads the RUNTIME class of __instance through IL2CPP
/// (ObjectClass -> il2cpp_class_is_assignable_from) and only zeroes the result for genuine
/// item instances, leaving the folded movement getters untouched.
/// </summary>
[HarmonyPatch(typeof(Items), "get_ItemWeight")]
static class DiveBuffsWeightlessItemsDefPatch
{
    private static IntPtr _itemsClass;
    private static bool _itemsClassResolved;

    static void Postfix(Il2CppObjectBase __instance, ref float __result)
    {
        try
        {
            if (!SuperDaveCore.Enabled.Value || !DiveBuffs.WeightlessItems.Value) return;
            if (__instance == null) return;

            if (!_itemsClassResolved)
            {
                _itemsClassResolved = true;
                // DR.Items lives in namespace "DR".
                _itemsClass = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "DR", "Items");
            }
            if (_itemsClass == IntPtr.Zero) return;

            // Folded twin IndependentMovableHelper.get_GetCurrentSpeed runs through this same
            // native body for a non-Items instance — only zero real item weights.
            if (IL2CPP.il2cpp_class_is_assignable_from(_itemsClass, __instance.ObjectClass))
                __result = 0f;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(IntegratedItem), "get_ItemWeight")]
static class DiveBuffsWeightlessItemWeightPatch
{
    private static IntPtr _integratedItemClass;
    private static bool _integratedItemClassResolved;

    static void Postfix(Il2CppObjectBase __instance, ref float __result)
    {
        try
        {
            if (!SuperDaveCore.Enabled.Value || !DiveBuffs.WeightlessItems.Value) return;
            if (__instance == null) return;

            if (!_integratedItemClassResolved)
            {
                _integratedItemClassResolved = true;
                // IntegratedItem has no namespace (global).
                _integratedItemClass = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "IntegratedItem");
            }
            if (_integratedItemClass == IntPtr.Zero) return;

            // Folded twin IndependentMovableHelper.get_GetCurrentRotationSpeed runs through this
            // same native body for a non-IntegratedItem instance.
            if (IL2CPP.il2cpp_class_is_assignable_from(_integratedItemClass, __instance.ObjectClass))
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
