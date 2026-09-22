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
    private const float SLEEP_BUFF_VALUE = 9999999999f;

    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<bool> SleepEffect;
    public static ConfigEntry<float> Radius;
    public static ConfigEntry<float> UpdateFrequency;
    public static ConfigEntry<bool> LargePickups;

    // Runtime toggles (initialized from config, flipped by hotkeys)
    public static bool ActiveByHotkey = true;
    public static bool SleepByHotkey = true;

    private static bool _initialized;
    private static bool _didSetSleepBuffValue;

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
        LargePickups = config.Bind(
            "Diving", "Diving - Enable Large Pickups", false,
            "Set to true to let large fish (Calldrone) be picked up without drones.");
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

            if (!_didSetSleepBuffValue)
            {
                var data = DataManager.Instance?.BuffEffectDataDic;
                if (data != null && data.ContainsKey(SLEEP_BUFF_ID))
                {
                    data[SLEEP_BUFF_ID].buffvalue1 = SLEEP_BUFF_VALUE;
                    data[SLEEP_BUFF_ID].buffvalue2 = SLEEP_BUFF_VALUE;
                    _didSetSleepBuffValue = true;
                }
            }

            var origin = character.transform.position;
            float radius = Radius.Value;

            foreach (var fish in EntityRegistry.AllFish)
            {
                if (fish == null || fish.gameObject == null) continue;

                if (!AutoPickup.AutoPickupFish.Value
                    && fish.InteractionType == FishInteractionBody.FishInteractionType.Calldrone
                    && LargePickups.Value)
                {
                    fish.InteractionType = FishInteractionBody.FishInteractionType.Pickup;
                }

                if (Vector3.Distance(origin, fish.transform.position) > radius) continue;

                if (SleepByHotkey)
                {
                    var buffHandler = fish.gameObject.GetComponent<BuffHandler>();
                    if (buffHandler == null) continue;
                    bool isAsleep = false;
                    try
                    {
                        var buffDict = Il2CppReflection.GetFieldValue(buffHandler, "CJCBPPIBGLB");
                        if (buffDict != null && Il2CppReflection.GetFieldValue<int>((Il2CppSystem.Object)buffDict, "count") > 0)
                            isAsleep = true;
                    }
                    catch { }
                    if (!isAsleep)
                        buffHandler.AddBuff(SLEEP_BUFF_ID);
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
}
