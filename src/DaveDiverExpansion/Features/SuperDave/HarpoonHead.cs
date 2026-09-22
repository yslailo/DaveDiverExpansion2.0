using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using DR;
using DaveDiverExpansion.Helpers;
using UnityEngine;

namespace DaveDiverExpansion.Features.SuperDave;

/// <summary>
/// Default harpoon head selection (ported from SuperDave 2.0).
/// Applies the configured harpoon head once per inventory instance.
/// </summary>
public static class HarpoonHead
{
    public static ConfigEntry<string> HeadType;
    public static ConfigEntry<int> HeadLevel;

    private static Dictionary<HarpoonHeadItemType, List<HarpoonHeadSpecData>> _heads;
    private static int _appliedInventoryHash;

    public static void Init(ConfigFile config)
    {
        HeadType = config.Bind(
            "Harpoon", "Harpoon - Head Type", "",
            "Harpoon head type to equip when diving (one of: Normal, Electric, Poison, Chain, Sleep, Paralysis, Strong, Fire, Ice) [case sensitive, blank = disable].");
        HeadLevel = config.Bind(
            "Harpoon", "Harpoon - Head Level", 0,
            "Harpoon head level [ignored if Head Type is blank] (int, 0 = weakest .. 4 = strongest).");
    }

    public static void Tick(PlayerCharacter player)
    {
        try
        {
            if (!SuperDaveCore.Enabled.Value || string.IsNullOrEmpty(HeadType.Value)) return;
            var inventory = player?.CurrentInstanceItemInventory;
            if (inventory == null) return;

            int hash = inventory.GetHashCode();
            if (hash == _appliedInventoryHash) return;

            if (!Enum.TryParse<HarpoonHeadItemType>(HeadType.Value + "Head", out var type)) return;
            BuildHeads();
            if (_heads == null || !_heads.TryGetValue(type, out var list) || list.Count == 0) return;

            int level = Mathf.Clamp(HeadLevel.Value, 0, list.Count - 1);
            inventory.harpoonHandler.EquipItem(list[level], true);
            _appliedInventoryHash = hash;
            Plugin.Log.LogInfo($"[HarpoonHead] Equipped {type} level {level} ({list[level].Name})");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("[HarpoonHead] Tick failed: " + e.Message);
        }
    }

    private static void BuildHeads()
    {
        if (_heads != null) return;
        _heads = new Dictionary<HarpoonHeadItemType, List<HarpoonHeadSpecData>>();
        foreach (var spec in Resources.FindObjectsOfTypeAll<HarpoonHeadSpecData>())
        {
            HarpoonHeadItemType type;
            try { type = (HarpoonHeadItemType)Il2CppReflection.GetFieldValue<int>(spec, "m_HarpoonHeadType"); }
            catch { continue; }
            if (!_heads.ContainsKey(type)) _heads[type] = new List<HarpoonHeadSpecData>();
            _heads[type].Add(spec);
        }
        foreach (var list in _heads.Values)
            list.Sort((x, y) => x.Damage.CompareTo(y.Damage));
    }
}
