using System.Collections.Generic;
using BepInEx.Configuration;
using DR;
using DaveDiverExpansion.Helpers;
using UnityEngine;

namespace DaveDiverExpansion.Features.SuperDave;

/// <summary>
/// Toxic aura ported from SuperDave: insta-kill (or sleep) fish around Dave.
/// Uses EntityRegistry.AllFish instead of FindObjectsOfTypeAll for performance.
/// Toggled at runtime by SuperDaveHotkeys.
/// </summary>
public static class ToxicAura
{
    private const int SLEEP_BUFF_ID = 14080415;

    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<bool> SleepEffect;
    public static ConfigEntry<float> Radius;
    public static ConfigEntry<float> UpdateFrequency;

    // Runtime toggles (initialized from config, flipped by hotkeys)
    public static bool ActiveByHotkey = true;
    public static bool SleepByHotkey = true;

    private static bool _initialized;

    // Long sleep value applied to the SHARED sleep buff data (original SuperDave behaviour).
    // This is what lets big fish fall asleep (they resist the default short sleep) and keeps
    // them asleep.  Applied once per process.
    // NOTE: this also lengthens every other sleep effect in the game (e.g. the tranquilizer
    // gun) — that is the trade-off for the aura actually working.  (A per-fish clone of the
    // data was attempted, but the interop AddBuff(BuffDebuffEffectData,...) overload throws.)
    private const float LONG_SLEEP_VALUE = 9999999999f;
    private static bool _didSetSleepBuffValue;
    // Safety net: never re-apply to the same fish more often than this.
    private const float SleepReapplyCooldown = 8f;
    private static readonly Dictionary<long, float> _lastSleep = new();

    public static void Init(ConfigFile config)
    {
        Enabled = config.Bind(
            "Diving", "Diving - Toxic Aura: Enabled", false,
            "Set to true to enable the fish-killing (or sleeping) aura around Dave.");
        SleepEffect = config.Bind(
            "Diving", "Diving - Toxic Aura: Sleep Effect", true,
            "Set to false to switch from sleep aura to instant-kill aura.");
        Radius = config.Bind(
            "Diving", "Diving - Toxic Aura: Radius", 6f,
            "Radius (in meters) around Dave in which fish are affected (float, default 6f).");
        UpdateFrequency = config.Bind(
            "Diving", "Diving - Toxic Aura: Update Frequency", 0.5f,
            "Time (in seconds) between aura pulses (float, default 0.5f).");
    }

    public static void EnsureInit()
    {
        if (_initialized) return;
        ActiveByHotkey = Enabled.Value;
        SleepByHotkey = SleepEffect.Value;
        _initialized = true;
    }

    public static void ToggleActive()
    {
        EnsureInit();
        ActiveByHotkey = !ActiveByHotkey;
        Plugin.Log.LogInfo($"[ToxicAura] Active = {ActiveByHotkey}");
    }

    public static void ToggleSleep()
    {
        EnsureInit();
        SleepByHotkey = !SleepByHotkey;
        Plugin.Log.LogInfo($"[ToxicAura] Sleep mode = {SleepByHotkey}");
    }

    public static void Tick()
    {
        try
        {
            if (!SuperDaveCore.Enabled.Value) return;
            EnsureInit();
            if (!ActiveByHotkey) return;

            var character = SuperDaveCore.Character;
            if (character == null || !character.isActiveAndEnabled) return;

            if (SleepByHotkey && !_didSetSleepBuffValue)
                EnsureLongSleepValue();

            var origin = character.transform.position;
            float radius = Radius.Value;

            foreach (var fish in EntityRegistry.AllFish)
            {
                if (fish == null || fish.gameObject == null) continue;

                // NOTE: the old "droneless large pickup" conversion (Calldrone -> Pickup) has
                // been removed entirely.  Flipping a big fish to Pickup without a valid
                // pickupCommand made the game dereference a null command in its per-frame input
                // callback ("NullReferenceException while executing 'InputSystem.onAfterUpdate'
                // callbacks") and froze the player.  Large fish keep their native Calldrone
                // interaction; "Diving - Auto Call Drone" calls the salvage drone automatically.

                if (Vector3.Distance(origin, fish.transform.position) > radius) continue;

                if (SleepByHotkey)
                {
                    var buffHandler = FindBuffHandler(fish.gameObject);
                    if (buffHandler == null) continue;
                    if (!IsBuffAsleep(buffHandler))
                    {
                        long key = fish.Pointer.ToInt64();
                        if (!_lastSleep.TryGetValue(key, out float last)
                                || Time.time - last >= SleepReapplyCooldown)
                        {
                            buffHandler.AddBuff(SLEEP_BUFF_ID);
                            _lastSleep[key] = Time.time;
                        }
                    }
                }
                else
                {
                    var damageable = fish.gameObject.GetComponent<Damageable>();
                    if (damageable != null)
                        damageable.OnDie();
                }
            }
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogError("** ToxicAura.Tick ERROR - " + e);
        }
    }

    /// <summary>True when the fish currently has the sleep buff (shared with AutoCallDrone).</summary>
    public static bool IsFishAsleep(FishInteractionBody fish)
    {
        if (fish == null || fish.gameObject == null) return false;
        try
        {
            var buffHandler = FindBuffHandler(fish.gameObject);
            return buffHandler != null && IsBuffAsleep(buffHandler);
        }
        catch { return false; }
    }

    /// <summary>
    /// Locate the BuffHandler.  Small fish keep it on the same GameObject as the
    /// FishInteractionBody, but large fish frequently keep it on a child (body model) or
    /// parent — a plain GetComponent then returns null and the fish never looked "asleep".
    /// </summary>
    public static BuffHandler FindBuffHandler(GameObject go)
    {
        if (go == null) return null;
        var bh = go.GetComponent<BuffHandler>();
        if (bh != null) return bh;
        bh = go.GetComponentInChildren<BuffHandler>(true);
        if (bh != null) return bh;
        return go.GetComponentInParent<BuffHandler>();
    }

    private static bool IsBuffAsleep(BuffHandler buffHandler)
    {
        // 1) typed check
        try
        {
            if (buffHandler.HasBuffType(BuffType.Sleep) || buffHandler.HasBuffType(BuffType.InstantSleep))
                return true;
        }
        catch { }

        // 2) direct scan of the real buff dictionary.  NOTE: the real field is `m_Buffs`; the
        //    previous code read a non-existent obfuscated name ("CJCBPPIBGLB"), so this branch
        //    always returned false and every large fish looked awake.
        try
        {
            var buffs = buffHandler.m_Buffs;
            if (buffs != null)
            {
                if (buffs.ContainsKey(SLEEP_BUFF_ID)) return true;
                foreach (var kv in buffs)
                {
                    var buff = kv.Value;
                    if (buff == null) continue;
                    var t = buff.BuffType;
                    if (t == BuffType.Sleep || t == BuffType.InstantSleep) return true;
                    if (buff.BuffID == SLEEP_BUFF_ID || buff.m_BuffDataID == SLEEP_BUFF_ID) return true;
                }
            }
        }
        catch { }

        return false;
    }

    /// <summary>Compact buff-state summary for the AutoCallDrone diag line.</summary>
    public static string DescribeBuffState(FishInteractionBody fish)
    {
        if (fish == null || fish.gameObject == null) return "bh=nullfish";
        try
        {
            var bh = FindBuffHandler(fish.gameObject);
            if (bh == null) return "bh=none";
            int n = 0;
            bool hasSleep = false, hasInstant = false, hasId = false;
            try { n = bh.m_Buffs?.Count ?? 0; } catch { }
            try { hasSleep = bh.HasBuffType(BuffType.Sleep); } catch { }
            try { hasInstant = bh.HasBuffType(BuffType.InstantSleep); } catch { }
            try { hasId = bh.m_Buffs != null && bh.m_Buffs.ContainsKey(SLEEP_BUFF_ID); } catch { }
            return $"bh=1 n={n} sleep={hasSleep} instant={hasInstant} id={hasId}";
        }
        catch { return "bh=err"; }
    }

    // Applies the long sleep value to the shared sleep buff data once (original behaviour).
    private static void EnsureLongSleepValue()
    {
        try
        {
            var dic = DataManager.Instance?.BuffEffectDataDic;
            if (dic != null && dic.ContainsKey(SLEEP_BUFF_ID))
            {
                dic[SLEEP_BUFF_ID].buffvalue1 = LONG_SLEEP_VALUE;
                dic[SLEEP_BUFF_ID].buffvalue2 = LONG_SLEEP_VALUE;
                _didSetSleepBuffValue = true;
                Plugin.Log.LogInfo("[ToxicAura] applied shared long sleep value");
            }
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning("[ToxicAura] set long sleep value failed: " + e.Message);
        }
    }
}
