using System.Collections.Generic;
using BepInEx.Configuration;
using DR;
using EvilFactory;
using UnityEngine;

namespace DaveDiverExpansion.Features.SuperDave;

/// <summary>
/// Weapon granting / level control (ported from SuperDave 2.0).
/// Builds a per-type sorted list of GunSpecData and equips via the game's own
/// gunHandler.EquipItem path. Also provides a full-heal action.
/// </summary>
public static class WeaponControl
{
    private static Dictionary<GunItemType, List<GunSpecData>> _gunSpecs;
    private static readonly Dictionary<GunItemType, int> _levelByType = new();
    private static bool _built;

    public static void Init(ConfigFile config)
    {
        // Weapon actions are hotkey-driven; no config entries here.
    }

    private static void Build()
    {
        if (_built) return;
        _built = true;
        _gunSpecs = new Dictionary<GunItemType, List<GunSpecData>>();
        foreach (var spec in Resources.FindObjectsOfTypeAll<GunSpecData>())
        {
            if (!_gunSpecs.ContainsKey(spec.GunType)) _gunSpecs[spec.GunType] = new List<GunSpecData>();
            _gunSpecs[spec.GunType].Add(spec);
        }
        foreach (var type in _gunSpecs.Keys)
            _gunSpecs[type].Sort(CompareGuns);
    }

    private static int CompareGuns(GunSpecData x, GunSpecData y)
    {
        int res = x.Damage.CompareTo(y.Damage);
        if (res != 0) return res;

        if (x.buffDatas != null && x.buffDatas.Count > 0 && y.buffDatas != null && y.buffDatas.Count > 0)
            res = x.buffDatas[0].Level.CompareTo(y.buffDatas[0].Level);
        if (res != 0) return res;

        switch (x.GunRootType)
        {
            case GunRootType.BasicRifle:
                res = string.CompareOrdinal(x.Name, y.Name);
                if (res == 0 && x.GunType == GunItemType.GrenadeLauncher)
                    res = x.ExplosionSplashDamage.CompareTo(y.ExplosionSplashDamage);
                break;
            case GunRootType.SleepGun:
                res = string.CompareOrdinal(x.Name, y.Name);
                break;
            case GunRootType.NetGun:
                res = x.CaptureCount.CompareTo(y.CaptureCount);
                break;
            case GunRootType.StickyBombGun:
                if (x.GunType == GunItemType.MineBombGun || x.GunType == GunItemType.MineBombGun02 || x.GunType == GunItemType.SleepBombGun)
                {
                    res = string.CompareOrdinal(x.Name, y.Name);
                    break;
                }
                res = x.ExplosionSplashDamage.CompareTo(y.ExplosionSplashDamage);
                break;
            case GunRootType.GrenadeLauncher:
                res = x.ExplosionSplashDamage.CompareTo(y.ExplosionSplashDamage);
                break;
            case GunRootType.IceGun:
                if (x.buffEffects != null && x.buffEffects.Count > 0 && y.buffEffects != null && y.buffEffects.Count > 0)
                    res = x.buffEffects[0].Level.CompareTo(y.buffEffects[0].Level);
                break;
        }
        return res;
    }

    public static void Heal()
    {
        var player = SuperDaveCore.Player;
        if (!SuperDaveCore.Enabled.Value || player == null) return;
        try
        {
            var breath = player.BreathHandler;
            breath.HealHP(breath.MaxHP - breath.HP);
            Plugin.Log.LogInfo("[WeaponControl] Healed player");
        }
        catch (System.Exception e) { Plugin.Log.LogWarning("[WeaponControl] Heal failed: " + e.Message); }
    }

    public static void IncreaseLevel() => StepLevel(+1);
    public static void DecreaseLevel() => StepLevel(-1);

    private static void StepLevel(int delta)
    {
        var player = SuperDaveCore.Player;
        if (!SuperDaveCore.Enabled.Value || player == null) return;
        try
        {
            Build();
            var current = player.CurrentInstanceItemInventory?.gunHandler?.m_GunSpec;
            if (current == null || _gunSpecs == null || !_gunSpecs.TryGetValue(current.GunType, out var list) || list.Count == 0) return;

            _levelByType.TryGetValue(current.GunType, out int level);
            level = Mathf.Clamp(level + delta, 0, list.Count - 1);
            _levelByType[current.GunType] = level;

            player.CurrentInstanceItemInventory.gunHandler.EquipItem(list[level], true);
            Plugin.Log.LogInfo($"[WeaponControl] {current.GunType} -> level {level} ({list[level].Name})");
        }
        catch (System.Exception e) { Plugin.Log.LogWarning("[WeaponControl] StepLevel failed: " + e.Message); }
    }

    public static void Give(GunItemType gunType)
    {
        var player = SuperDaveCore.Player;
        if (!SuperDaveCore.Enabled.Value || player == null) return;
        try
        {
            Build();
            if (_gunSpecs == null || !_gunSpecs.TryGetValue(gunType, out var list) || list.Count == 0)
            {
                Plugin.Log.LogWarning($"[WeaponControl] No GunSpecData for {gunType}");
                return;
            }
            _levelByType.TryGetValue(gunType, out int level);
            level = Mathf.Clamp(level, 0, list.Count - 1);
            player.CurrentInstanceItemInventory.gunHandler.EquipItem(list[level], true);
            Plugin.Log.LogInfo($"[WeaponControl] Gave {gunType} level {level} ({list[level].Name})");
        }
        catch (System.Exception e) { Plugin.Log.LogWarning("[WeaponControl] Give failed: " + e.Message); }
    }

    public static void GiveTranq() => Give(GunItemType.SleepGun02);
    public static void GiveNet() => Give(GunItemType.L_NetGun);
    public static void GiveSnipe() => Give(GunItemType.Poison_SniperGun02);
}
