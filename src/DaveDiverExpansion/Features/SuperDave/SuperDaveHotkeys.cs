using BepInEx.Configuration;
using UnityEngine;

namespace DaveDiverExpansion.Features.SuperDave;

/// <summary>
/// SuperDave hotkeys, unified to ConfigEntry&lt;KeyCode&gt; so the in-game
/// ConfigUI can capture keys directly. Checked every frame from ConfigUIBehaviour.
/// </summary>
public static class SuperDaveHotkeys
{
    public static ConfigEntry<KeyCode> Modifier;
    public static ConfigEntry<KeyCode> ToggleToxicAura;
    public static ConfigEntry<KeyCode> ChangeAuraMode;
    public static ConfigEntry<KeyCode> Heal;
    public static ConfigEntry<KeyCode> NetGun;
    public static ConfigEntry<KeyCode> TranqGun;
    public static ConfigEntry<KeyCode> Sniper;
    public static ConfigEntry<KeyCode> WeaponUp;
    public static ConfigEntry<KeyCode> WeaponDown;

    public static void Init(ConfigFile config)
    {
        Modifier = config.Bind(
            "Hotkeys", "Modifier", KeyCode.LeftControl,
            "Modifier key that must be held for the hotkeys below to fire. Set to None to not require one.");
        ToggleToxicAura = config.Bind(
            "Hotkeys", "Toggle Toxic Aura", KeyCode.Backspace,
            "Toggle the toxic aura on/off (if enabled).");
        ChangeAuraMode = config.Bind(
            "Hotkeys", "Change Toxic Aura Mode", KeyCode.Backslash,
            "Switch the toxic aura between Sleep/Kill.");
        Heal = config.Bind(
            "Hotkeys", "Heal", KeyCode.Keypad0,
            "Fully heal Dave.");
        NetGun = config.Bind(
            "Hotkeys", "Net Gun", KeyCode.Keypad1,
            "Give Dave a Net Gun.");
        TranqGun = config.Bind(
            "Hotkeys", "Tranq Gun", KeyCode.Keypad2,
            "Give Dave a Tranq Gun.");
        Sniper = config.Bind(
            "Hotkeys", "Sniper", KeyCode.Keypad3,
            "Give Dave a Sniper.");
        WeaponUp = config.Bind(
            "Hotkeys", "Weapon Up", KeyCode.KeypadPlus,
            "Increase current weapon level.");
        WeaponDown = config.Bind(
            "Hotkeys", "Weapon Down", KeyCode.KeypadMinus,
            "Decrease current weapon level.");
    }

    private static bool ModifierDown()
    {
        if (Modifier.Value == KeyCode.None) return true;
        return Input.GetKey(Modifier.Value);
    }

    /// <summary>Called every frame from ConfigUIBehaviour.Update.</summary>
    internal static void Check()
    {
        if (!SuperDaveCore.Enabled.Value) return;
        try
        {
            if (!ModifierDown()) return;

            if (Down(ToggleToxicAura)) ToxicAura.ToggleActive();
            if (Down(ChangeAuraMode)) ToxicAura.ToggleSleep();
            if (Down(Heal)) WeaponControl.Heal();
            if (Down(NetGun)) WeaponControl.GiveNet();
            if (Down(TranqGun)) WeaponControl.GiveTranq();
            if (Down(Sniper)) WeaponControl.GiveSnipe();
            if (Down(WeaponUp)) WeaponControl.IncreaseLevel();
            if (Down(WeaponDown)) WeaponControl.DecreaseLevel();
        }
        catch { }
    }

    private static bool Down(ConfigEntry<KeyCode> key)
    {
        return key.Value != KeyCode.None && Input.GetKeyDown(key.Value);
    }
}
