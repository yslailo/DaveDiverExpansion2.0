## Description

**DaveDiverExpansion 2.0** is an all-in-one quality-of-life mod for *Dave the Diver*, built on BepInEx 6 (IL2CPP) + HarmonyX.

It keeps the original **DaveDiverExpansion** framework — including the in-game **F1 settings panel**, dive map, auto pickup and iDiver upgrades — and folds in the full **SuperDave** feature set (infinite oxygen, toxic aura, infinite drones/traps, speed boosts, weapon hotkeys and more). Everything lives in a single plugin, with one config file and one settings panel.

**Highlights:**

- One plugin, one config — no conflicting mods to juggle
- Full in-game settings panel: press **F1**, every option with a description, rebinding and "Reset All Settings"
- Panel grouped into 6 areas: **Diving / Farming / Sushi / Map / Automation / System** — hotkeys sit next to the feature they drive
- On-screen **status HUD**: Toxic Aura state/mode + common toggles, flashes when the aura is toggled, corner configurable
- Auto pickup fish/items/chests while diving, with a configurable radius and smart filtering
- Full-screen dive map with marker toggles — press **M**
- Instant capture: Toxic Aura (kill or sleep), infinite drones & crab traps
- Diving buffs: infinite oxygen, invincible, infinite bullets, weightless items
- Sushi bar: infinite patience, infinite wasabi, money boost
- Weapon hotkeys: heal, give guns, level weapons up/down
- English + 简体中文 interface

All options are off/on individually — install it even if you only want the map or auto pickup.

## Installation

1. Install **[BepInEx 6 Bleeding Edge](https://builds.bepinex.dev/projects/bepinex_be)** — download the **Unity.IL2CPP-win-x64** build.
2. Extract BepInEx into your game folder:
`Steam\steamapps\common\Dave the Diver\`
3. **Launch the game once** so BepInEx generates `BepInEx\interop`, then close it.
4. Download `DaveDiverExpansion-2.0.3.zip` from this page and extract it.
5. Copy `dave-diver-expansion2.0` folder into:
`Dave the Diver\BepInEx\plugins\`
so you end up with `BepInEx\plugins\dave-diver-expansion2.0\dave-diver-expansion2.0.dll`
6. Launch the game and press **F1** to open the settings panel.

**Updating:** just replace the old `dave-diver-expansion2.0` folder with the new one. Your settings are kept (`BepInEx\config\com.davediver.expansion2.0.cfg`).

**Uninstall:** delete `BepInEx\plugins\dave-diver-expansion2.0\` (and optionally the config file).

## Main features

**DaveDiverExpansion framework**

- **Auto Pickup** — automatically collects nearby fish, items and chests while diving
  - Configurable pickup radius
  - Independent toggles for fish / items / chests / ammo boxes / oxygen boxes
  - Smart filtering: skips weapons to prevent swap loops, respects grab-level limits
  - Pauses during cutscenes so quests don't break
- **Dive Map** — minimap HUD + full map overlay
  - Press **M** to toggle the enlarged map (scroll to zoom, drag to pan)
  - Marker toggles for aggressive fish / ores / chests / escape pods / fish
  - Auto-disables in Merfolk Village (it has its own map)
- **iDiver Extension** — extra upgrades in the iDiver panel: harpoon damage, movement/booster speed, booster duration, crab trap count/efficiency, drone count, fish density ×2/×3/×4
- **Auto Seahorse Race** — auto-controls your seahorse during races
- **Betting Expansion** — casino bets up to 5000
- **Quick Scene Switch** — press **F2** to jump between scenes
- **In-Game Config Panel** — press **F1**; live-edit every setting, rebind hotkeys by clicking a row and pressing a key
- **6-area panel** — Diving / Farming / Sushi / Map / Automation / System; related hotkeys are listed inside their feature area
- **Status HUD** — always-on corner overlay showing Toxic Aura state/mode and Infinite Oxygen / Invincible / Infinite Bullets; flashes when the aura is toggled; corner and per-line toggles configurable

**SuperDave features**

- **Diving buffs** — Infinite Oxygen, Invincible, Weightless Items, Infinite Bullets, hide item popups
- **Infinite Drones** & **Infinite Crab Traps** (+ auto-drop traps on trap zones)
- **Toxic Aura** — insta-kill or sleep fish around Dave, with hotkeys to toggle/switch mode
- **Speed boosts** — swim, boat walk, farm, fish farm, sushi bar & staff
- **Sushi bar** — infinite customer patience, infinite wasabi, money boost, faster cooking
- **Default Harpoon Head** — auto-equip a chosen harpoon type + level on every dive
- **Weapon hotkeys** — full heal, give Tranq/Net/Snipe gun, weapon level up/down
- **Auto Call Drone** — automatically call the salvage drone for a sleeping large fish when Dave is close

**Default hotkeys** (hold the modifier key, default `LeftControl`):

| Hotkey | Default | Action |
|--------|---------|--------|
| Modifier | `LeftControl` | Hold for the hotkeys below |
| Toggle Toxic Aura | `Backspace` | Aura on/off |
| Aura Mode | `Backslash` | Sleep / Kill |
| Heal | `Keypad0` | Fully heal Dave |
| Net Gun | `Keypad1` | Give a Net Gun |
| Tranq Gun | `Keypad2` | Give a Tranq Gun |
| Sniper | `Keypad3` | Give a Sniper |
| Weapon Up | `KeypadPlus` | Weapon level +1 |
| Weapon Down | `KeypadMinus` | Weapon level -1 |

Other: **F1** settings, **F2** scene switch, **M** dive map.

## Requirements

- **[Dave the Diver](https://store.steampowered.com/app/1868140/Dave_the_Diver/)** (Steam, current version)
- **[BepInEx 6 Bleeding Edge](https://builds.bepinex.dev/projects/bepinex_be)** — *Unity.IL2CPP-win-x64* build
  - Download link above in the Installation section; required, this mod does not bundle it
- Windows 10/11, x64
- No other mod dependencies — this is a single standalone plugin

**Notes / compatibility**

- This mod bundles the features of the older *DaveDiverExpansion* and *SuperDave* mods. **Do not install those at the same time** — remove them first to avoid duplicate configs and patch conflicts.
- Save your game before enabling the more powerful options (infinite oxygen, toxic aura, etc.).
- Multiplayer/co-op is not applicable; single-player only.
- If the game updates and a feature breaks, check the GitHub page for a new release.

## Shout outs

- **[WhiteMinds/dave-diver-expansion](https://github.com/WhiteMinds/dave-diver-expansion)** — the original DaveDiverExpansion framework, auto pickup, dive map and F1 config panel this mod is built on.
- **[devopsdinosaur/dave-the-diver-mods](https://github.com/devopsdinosaur/dave-the-diver-mods)** (SuperDave) — diving buffs, drones/traps, toxic aura, sushi bar, speed boosts and weapon hotkeys.
- **[Arutsuyo/SuperDave2.0](https://github.com/Arutsuyo/SuperDave2.0)** — SuperDave 2.0 refinements.
- The **BepInEx** team for BepInEx 6 + HarmonyX, without which none of this exists.
- The *Dave the Diver* modding community for interop references and testing.

Source code: [github.com/yslailo/DaveDiverExpansion2.0](https://github.com/yslailo/DaveDiverExpansion2.0) — MIT License. Issues and PRs welcome.
