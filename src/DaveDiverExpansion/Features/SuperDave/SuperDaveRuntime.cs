using HarmonyLib;
using UnityEngine;

namespace DaveDiverExpansion.Features.SuperDave;

/// <summary>
/// Per-frame driver for the SuperDave features that need a tick during a dive
/// (infinite drones/bullets, harpoon head, toxic aura).
/// </summary>
public static class SuperDaveRuntime
{
    private static float _generalTimer;
    private static float _auraTimer;

    public static void Tick(PlayerCharacter player)
    {
        if (!SuperDaveCore.Enabled.Value) return;

        HarpoonHead.Tick(player);

        _generalTimer += Time.deltaTime;
        if (_generalTimer >= 1f)
        {
            _generalTimer = 0f;
            try
            {
                if (DroneTrap.InfiniteDrones.Value)
                    player.AvailableLiftDroneCount = 999;
                if (DiveBuffs.InfiniteBullets.Value)
                    player.CurrentInstanceItemInventory?.gunHandler.ForceSetBulletCount(999);
            }
            catch { }
        }

        float freq = ToxicAura.UpdateFrequency.Value;
        if (freq < 0.05f) freq = 0.05f;
        _auraTimer += Time.deltaTime;
        if (_auraTimer >= freq)
        {
            _auraTimer = 0f;
            ToxicAura.Tick();
        }
    }
}

[HarmonyPatch(typeof(PlayerCharacter), "Update")]
static class SuperDaveRuntimePatch
{
    static void Postfix(PlayerCharacter __instance) => SuperDaveRuntime.Tick(__instance);
}
