# DaveDiverExpansion 2.0

A mod for **Dave the Diver** built on BepInEx 6 (IL2CPP) + HarmonyX.

DaveDiverExpansion 2.0 keeps the original **DaveDiverExpansion** framework and its in-game
**F1 settings panel**, and folds in the full **SuperDave** quality-of-life feature set
(formerly "SuperDave 3.0").

- **Plugin name:** `dave-diver-expansion2.0`
- **GUID:** `com.davediver.expansion2.0`
- **Config file:** `BepInEx/config/com.davediver.expansion2.0.cfg`
- **Config UI:** press **F1** in-game

## Features

### DaveDiverExpansion

- **Auto Pickup** — automatically collects nearby fish, items and chests while diving
  - Configurable pickup radius
  - Independent toggles for fish / items / chests / ammo boxes / oxygen boxes
  - Smart filtering: skips weapons to prevent swap loops, respects sea-urchin grab level
  - Pauses during cutscenes/scenarios with a cooldown to avoid quest-breaking pickups
  - Uses a Harmony lifecycle registry (no per-frame `FindObjectsOfType`) for performance
- **Dive Map** — minimap HUD and full-level map overlay while diving
  - Minimap in a configurable corner, follows the player with adjustable zoom
  - Press **M** to toggle the enlarged map — scroll to zoom toward cursor, drag to pan
  - Distinct marker shapes (aggressive fish / ores / chests / escape pods / normal & catchable fish)
  - Independent toggles for each marker type, minimap opacity/size, distant-fish markers
  - Auto-disables in Merfolk Village (which has its own map)
- **iDiver Extension** — custom upgrade options in the iDiver upgrade panel (disabled by default)
  - Harpoon Damage / Movement Speed / Booster Speed / Booster Duration
  - Crab Trap Count / Efficiency, Drone Count, Ecology Protection (fish density ×2/×3/×4)
  - Upgrade levels persist even when the feature is disabled
- **Auto Seahorse Race** — automatically controls your seahorse during races (disabled by default)
- **Betting Expansion** — expands casino betting from 10/50/100 to 10/50/100/500/1000/5000
- **Quick Scene Switch** — press **F2** to open the scene-switch menu from anywhere
- **In-Game Config Panel** — press **F1**
  - Auto-discovers every config entry, renders toggles/sliders/dropdowns/text inputs
  - In-game key rebinding (click a hotkey row and press a key)
  - Hover a row to read its full description; scroll support; "Reset All Settings" button

### SuperDave (ported)

- **Diving buffs** — Infinite Oxygen, Invincible, Weightless Items, Infinite Bullets, disable item-info popups
- **Infinite Drones** and **Infinite Crab Traps** (+ auto-drop crab traps on nearby trap zones)
- **Toxic Aura** — insta-kill or sleep fish around Dave, with runtime hotkeys to toggle on/off and switch mode
- **Speed boosts** — swim speed, boat walk, farm walk, fish-farm walk, sushi-bar speed & staff walk
- **Sushi bar** — infinite customer patience, infinite wasabi, money boost, faster staff cooking
- **Default Harpoon Head** — pick a harpoon head type + level to auto-equip when diving
- **Weapon hotkeys** — full heal, give Tranq/Net/Snipe gun, weapon level up/down
- **Large Pickups** — pick up large fish without drones

## Installation (Players)

1. Download [BepInEx 6 Bleeding Edge](https://builds.bepinex.dev/projects/bepinex_be) → select **Unity.IL2CPP-win-x64**
2. Extract it into your game folder: `Steam\steamapps\common\Dave the Diver\`
3. Launch the game once so BepInEx generates the `BepInEx\interop` assemblies, then close it
4. Put `dave-diver-expansion2.0.dll` into `Dave the Diver\BepInEx\plugins\dave-diver-expansion2.0\`
5. Launch the game and press **F1** to configure

## Configuration

Press **F1** in-game to open the settings panel. All settings can be changed live and are saved
automatically. Alternatively edit `BepInEx/config/com.davediver.expansion2.0.cfg`.

### Default hotkeys

All SuperDave hotkeys require the **Modifier** key to be held (default `LeftControl`).

| Hotkey | Default | Action |
|--------|---------|--------|
| Modifier | `LeftControl` | Must be held for the hotkeys below |
| Toggle Toxic Aura | `Backspace` | Toggle the toxic aura on/off |
| Change Toxic Aura Mode | `Backslash` | Switch aura between Sleep / Kill |
| Heal | `Keypad0` | Fully heal Dave |
| Net Gun | `Keypad1` | Give Dave a Net Gun |
| Tranq Gun | `Keypad2` | Give Dave a Tranq Gun |
| Sniper | `Keypad3` | Give Dave a Sniper |
| Weapon Up | `KeypadPlus` | Increase current weapon level |
| Weapon Down | `KeypadMinus` | Decrease current weapon level |

Other hotkeys: **F1** (config panel), **F2** (quick scene switch), **M** (dive map).

## Building from source

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (6.0+)
- [Git](https://git-scm.com/) + [Git LFS](https://git-lfs.com/) (for the `lib/` reference DLLs)
- Dave the Diver (Steam)

### Steps

```bash
git lfs install
git clone https://github.com/yslailo/DaveDiverExpansion2.0.git
cd DaveDiverExpansion2.0

# Point the build at your game installation (this file is NOT committed)
cat > GamePath.user.props << 'EOF'
<Project>
  <PropertyGroup>
    <GamePath>C:\Program Files (x86)\Steam\steamapps\common\Dave the Diver</GamePath>
  </PropertyGroup>
</Project>
EOF

# Launch the game once to generate BepInEx/interop, then close it

# Build + auto-deploy to the game's BepInEx/plugins folder
dotnet build src/DaveDiverExpansion/DaveDiverExpansion.csproj -c Release
```

The build produces `src/DaveDiverExpansion/bin/Release/net480/dave-diver-expansion2.0.dll`
and (when `GamePath` is set) copies it to
`<GamePath>\BepInEx\plugins\dave-diver-expansion2.0\`.

### Project structure

```
Directory.Build.props              # Build config: references, auto-deploy (in git)
GamePath.user.props                # Your local game path (NOT in git)
lib/                               # Reference DLLs for CI builds (Git LFS)
scripts/update-lib.sh              # Copy reference DLLs from the game dir into lib/
src/DaveDiverExpansion/
  Plugin.cs                        # BepInEx entry point (BasePlugin)
  Features/                        # Feature modules (config + Harmony patches per file)
    SuperDave/                     # Ported SuperDave features
  Helpers/                         # Shared utilities (I18n, EntityRegistry, IL2CPP reflection)
.github/workflows/release.yml      # CI: build + GitHub Release on v* tags
```

## Credits & License

- Framework and original features based on
  [WhiteMinds/dave-diver-expansion](https://github.com/WhiteMinds/dave-diver-expansion)
- SuperDave features ported from
  [devopsdinosaur/dave-the-diver-mods](https://github.com/devopsdinosaur/dave-the-diver-mods)
  and [Arutsuyo/SuperDave2.0](https://github.com/Arutsuyo/SuperDave2.0)

Licensed under the **MIT License**.
