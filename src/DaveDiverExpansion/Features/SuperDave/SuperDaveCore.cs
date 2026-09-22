using BepInEx.Configuration;
using HarmonyLib;

namespace DaveDiverExpansion.Features.SuperDave;

/// <summary>
/// Shared state for the ported SuperDave features:
/// master enable switch plus cached player/character singletons.
/// </summary>
public static class SuperDaveCore
{
    public static ConfigEntry<bool> Enabled;

    public static void Init(ConfigFile config)
    {
        Enabled = config.Bind(
            "SuperDave", "Enabled", true,
            "Master switch for all SuperDave features (speed boosts, infinite oxygen, toxic aura, etc.).");
    }

    private static PlayerCharacter _player;
    public static PlayerCharacter Player
    {
        get
        {
            try { if (_player != null) { _ = _player.enabled; return _player; } }
            catch { }
            return _player = null;
        }
        internal set => _player = value;
    }

    private static CharacterController2D _character;
    public static CharacterController2D Character
    {
        get
        {
            try { if (_character != null) { _ = _character.enabled; return _character; } }
            catch { }
            return _character = null;
        }
        internal set => _character = value;
    }
}

[HarmonyPatch(typeof(PlayerCharacter), "Awake")]
static class SuperDavePlayerAwakePatch
{
    static void Postfix(PlayerCharacter __instance) => SuperDaveCore.Player = __instance;
}

[HarmonyPatch(typeof(CharacterController2D), "Awake")]
static class SuperDaveCharacterAwakePatch
{
    static void Postfix(CharacterController2D __instance) => SuperDaveCore.Character = __instance;
}

