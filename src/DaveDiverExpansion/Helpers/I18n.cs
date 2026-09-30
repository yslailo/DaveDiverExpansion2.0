using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace DaveDiverExpansion.Helpers;

/// <summary>
/// Mod UI language override.
/// Auto: detect from game language (SaveSystem API) or OS language.
/// Chinese/English: force specific language.
/// </summary>
public enum ModLanguage { Auto, Chinese, English }

/// <summary>
/// Lightweight i18n helper. English is the default; Chinese translations
/// are looked up from an internal dictionary. Call T("key") to translate.
/// </summary>
public static class I18n
{
    private static readonly Dictionary<string, string> ZhCn = new()
    {
        // Panel
        ["dave-diver-expansion2.0"] = "dave-diver-expansion2.0",
        ["DaveDiverExpansion Settings"] = "DaveDiverExpansion 设置",

        // Sections
        ["AutoPickup"] = "自动拾取",
        ["ConfigUI"] = "配置界面",
        ["SuperDave"] = "SuperDave 总开关",
        ["Boat"] = "船上",
        ["Farm"] = "农场",
        ["FishFarm"] = "鱼场",
        ["Diving"] = "潜水",
        ["Sushi"] = "寿司店",
        ["Harpoon"] = "鱼叉",
        ["Hotkeys"] = "快捷键",

        // F1 display groups (merged sections)
        ["Farming"] = "农场",
        ["Map"] = "地图",
        ["Automation"] = "自动化",
        ["System"] = "系统",
        ["AuraHud"] = "状态HUD",

        // F1 feature tree nodes (second level; hotkeys live under their feature)
        ["Survival"] = "生存",
        ["Speed"] = "速度",
        ["Ammo"] = "弹药",
        ["Drones & Traps"] = "无人机与渔笼",
        ["Toxic Aura"] = "剧毒光环",
        ["Auto Call Drone"] = "自动呼叫无人机",
        ["Weapon Control"] = "武器控制",
        ["iDiver Upgrades"] = "iDiver 升级",
        ["Status HUD"] = "状态 HUD",
        ["Master Switch"] = "总开关",
        ["Settings Panel"] = "设置面板",
        ["Hotkey Modifier"] = "热键修饰键",
        ["Auto Pickup"] = "自动拾取",
        ["Seahorse Race"] = "海马赛",
        ["Casino Betting"] = "娱乐场下注",
        ["Quick Scene Switch"] = "快速切换场景",
        ["Dive Map"] = "潜水地图",
        ["Mini Map"] = "小地图",
        ["Map Display"] = "地图显示",
        ["Markers"] = "标记",
        ["Fish Farm"] = "鱼场",
        ["Sushi Bar"] = "寿司吧",

        // Status HUD
        ["HUD - Enabled"] = "显示状态HUD",
        ["HUD - Corner"] = "HUD 位置",
        ["HUD - Show Aura"] = "显示光环状态",
        ["HUD - Show Common Buffs"] = "显示常用状态",
        ["Aura"] = "光环",
        ["ON"] = "开",
        ["OFF"] = "关",
        ["Sleep"] = "睡眠",
        ["Kill"] = "击杀",
        ["Show the on-screen status HUD (aura state + common toggles)."] = "显示屏幕上的状态 HUD（光环状态 + 常用开关）。",
        ["Screen corner for the status HUD."] = "状态 HUD 显示在屏幕的哪个角。",
        ["Show the toxic aura state and mode in the HUD."] = "在 HUD 中显示剧毒光环的开关与模式。",
        ["Show infinite oxygen / invincible / infinite bullets in the HUD."] = "在 HUD 中显示无限氧气 / 无敌 / 无限子弹。",
        // SuperDave feature labels
        ["Modifier"] = "修饰键",
        ["Toggle Toxic Aura"] = "切换剧毒光环",
        ["Change Toxic Aura Mode"] = "切换光环模式",
        ["Heal"] = "治疗",
        ["Net Gun"] = "渔网枪",
        ["Tranq Gun"] = "麻醉枪",
        ["Sniper"] = "狙击枪",
        ["Weapon Up"] = "武器升级",
        ["Weapon Down"] = "武器降级",
        ["AutoDropCrabTraps"] = "自动放置渔笼",
        ["AutoPickupDebugMode"] = "自动拾取调试日志",
        // Diving
        ["Diving - Infinite Oxygen"] = "无限氧气",
        ["Diving - Invincible"] = "无敌",
        ["Diving - Weightless Items"] = "物品无重量",
        ["Diving - Disable Item Info Popups"] = "关闭物品信息弹窗",
        ["Diving - Speed Boost"] = "游泳速度加成",
        ["Diving - Infinite Bullets"] = "无限子弹",
        ["Diving - Infinite Drones"] = "无限无人机",
        ["Diving - Infinite Crab Traps"] = "无限渔笼",
        ["Diving - Toxic Aura: Enabled"] = "剧毒光环：启用",
        ["Diving - Toxic Aura: Sleep Effect"] = "剧毒光环：睡眠效果",
        ["Diving - Toxic Aura: Radius"] = "剧毒光环：半径",
        ["Diving - Toxic Aura: Update Frequency"] = "剧毒光环：更新频率",
        ["Diving - Auto Call Drone"] = "潜水 - 自动呼叫无人机",
        ["Automatically call the salvage drone to lift a downed large fish (sleeping or dead) when Dave is close (no need to hold the interact key)."] =
            "大鱼被睡眠或击杀后，Dave 靠近即自动呼叫打捞无人机抓取，无需长按交互键。",
        ["Diving - Auto Call Drone: Radius"] = "潜水 - 自动呼叫无人机：半径",
        ["Radius (meters) around Dave in which a downed large fish triggers an automatic drone call (float, default 6f)."] =
            "Dave 周围多少米内的睡眠/死亡大鱼会触发自动呼叫无人机（浮点数，默认 6）。",
        ["Diving - Auto Call Drone: Cooldown"] = "潜水 - 自动呼叫无人机：冷却",
        ["Minimum seconds between drone calls on the same fish (float, default 3f)."] =
            "对同一条鱼两次呼叫无人机之间的最小间隔秒数（浮点数，默认 3）。",
        // Boat / Farm / FishFarm
        ["Boat - Walk Speed Boost"] = "船上行走速度加成",
        ["Farm - Walk Multiplier"] = "农场行走倍率",
        ["FishFarm - Walk Multiplier"] = "鱼场行走倍率",
        // Sushi
        ["Sushi - Speed Boost"] = "寿司店速度加成",
        ["Sushi - Staff Walk Multiplier"] = "寿司店员工行走倍率",
        ["Sushi - Infinite Customer Patience"] = "顾客无限耐心",
        ["Sushi - Money Boost"] = "寿司店金钱加成",
        ["Sushi - Infinite Wasabi"] = "无限芥末",
        ["Sushi - Cook Multiplier"] = "员工烹饪倍率",
        // Harpoon
        ["Harpoon - Head Type"] = "鱼叉头类型",
        ["Harpoon - Head Level"] = "鱼叉头等级",
        // SuperDave descriptions (hover text)
        ["Master switch for all SuperDave features (speed boosts, infinite oxygen, toxic aura, etc.)."] = "所有 SuperDave 功能的总开关（速度加成、无限氧气、剧毒光环等）。",
        ["Set to true to have infinite oxygen when diving."] = "潜水时无限氧气。",
        ["Set to true to take no damage when hit."] = "受到攻击时不掉血。",
        ["Set to true to reduce the weight of all items to 0 (infinite carry weight)."] = "把所有物品重量降为 0（无限负重）。",
        ["Set to true to disable the item info popup windows (they get far behind when auto-pickup is on)."] = "关闭物品信息弹窗（开自动拾取时弹窗会严重滞后）。",
        ["Permanent swim speed boost when diving (float, default 0f [set to 0 to disable])."] = "潜水时的常驻游泳速度加成（浮点数，默认 0f，设为 0 关闭）。",
        ["Set to true to have infinite bullets when diving."] = "潜水时无限子弹。",
        ["Set to true to enable infinite salvage drones."] = "启用无限打捞无人机。",
        ["Set to true to enable infinite crab traps (the popup still shows 0 but you can keep dropping them)."] = "启用无限渔笼（弹窗仍显示 0，但可以一直放置）。",
        ["Set to true to enable the fish-killing (or sleeping) aura around Dave."] = "启用 Dave 周围的杀鱼/催眠光环。",
        ["Set to false to switch from sleep aura to instant-kill aura."] = "设为 false 从睡眠光环切换为秒杀光环。",
        ["Radius (in meters) around Dave in which fish are affected (float, default 6f)."] = "光环影响半径（米，浮点数，默认 6f）。",
        ["Time (in seconds) between aura pulses (float, default 0.5f)."] = "光环脉冲间隔（秒，浮点数，默认 0.5f）。",
        ["Set to true to let large fish (Calldrone) be picked up without drones."] = "允许大型鱼（Calldrone）无需无人机即可拾取。",
        ["Speed boost applied to Dave when walking on the boat (float, default 0f [set to 0 to disable])."] = "Dave 在船上行走的速度加成（浮点数，默认 0f，设为 0 关闭）。",
        ["Multiplier applied to Dave when walking/sprinting on the farm (float, default 0f [< 1 == faster, > 1 == slower, set to 0 to disable]). [NOTE: values above ~5 can let Dave walk off screen and get stuck]."] = "Dave 在农场行走/奔跑的倍率（浮点数，默认 0f；< 1 更快，> 1 更慢，设为 0 关闭）。[注意：超过约 5 会让 Dave 走出屏幕卡住]",
        ["Multiplier applied to Dave when walking/sprinting on the fish farm (float, default 0f [set to 0 to disable])."] = "Dave 在鱼场行走/奔跑的倍率（浮点数，默认 0f，设为 0 关闭）。",
        ["Permanent speed boost when working in the sushi bar (float, default 0f [set to 0 to disable])."] = "在寿司店工作时的常驻速度加成（浮点数，默认 0f，设为 0 关闭）。",
        ["Multiplier applied to sushi bar staff walking speed [also affects Dave on top of 'Sushi - Speed Boost'] (float, default 0f [set to 0 to disable])."] = "寿司店员工行走速度倍率（也会叠加在 Dave 的 'Sushi - Speed Boost' 之上；浮点数，默认 0f，设为 0 关闭）。",
        ["Set to true to make customers never storm off if food/drinks are too slow."] = "顾客不会因为上菜/上酒太慢而离开。",
        ["Money boost applied to all customer purchases at the sushi bar (float, default 0f [set to 0 to disable])."] = "寿司店所有顾客消费的金钱加成（浮点数，默认 0f，设为 0 关闭）。",
        ["Set to true to never need to refill wasabi."] = "无需补充芥末。",
        ["Multiplier applied to sushi bar staff cooking speed (float, default 0f [> 1 == faster, set to 0 to disable])."] = "寿司店员工烹饪速度倍率（浮点数，默认 0f；> 1 更快，设为 0 关闭）。",
        ["Harpoon head type to equip when diving (one of: Normal, Electric, Poison, Chain, Sleep, Paralysis, Strong, Fire, Ice) [case sensitive, blank = disable]."] = "潜水时装备的鱼叉头类型（Normal / Electric / Poison / Chain / Sleep / Paralysis / Strong / Fire / Ice，区分大小写，留空关闭）。",
        ["Harpoon head level [ignored if Head Type is blank] (int, 0 = weakest .. 4 = strongest)."] = "鱼叉头等级（Head Type 留空时忽略；整数，0 最弱 .. 4 最强）。",
        ["Modifier key that must be held for the hotkeys below to fire. Set to None to not require one."] = "触发下方热键时必须按住的修饰键。设为 None 则不需要。",
        ["Toggle the toxic aura on/off (if enabled)."] = "开关剧毒光环（需已启用）。",
        ["Switch the toxic aura between Sleep/Kill."] = "在睡眠/秒杀之间切换剧毒光环。",
        ["Fully heal Dave."] = "将 Dave 治疗至满血。",
        ["Give Dave a Net Gun."] = "给 Dave 渔网枪。",
        ["Give Dave a Tranq Gun."] = "给 Dave 麻醉枪。",
        ["Give Dave a Sniper."] = "给 Dave 狙击枪。",
        ["Increase current weapon level."] = "提升当前武器等级。",
        ["Decrease current weapon level."] = "降低当前武器等级。",
        ["Automatically set up crab traps on nearby trap zones (uses max bait)."] = "自动在附近渔笼点放置渔笼（使用最高级鱼饵）。",
        ["Log each auto-pickup action for debugging."] = "记录每次自动拾取动作（调试用）。",

        // AutoPickup entries
        ["Enabled"] = "启用",
        ["AutoPickupFish"] = "自动拾取鱼",
        ["AutoPickupItems"] = "自动拾取物品",
        ["AutoOpenChests"] = "自动开启宝箱",
        ["AutoPickupAmmoBox"] = "自动拾取弹药箱",
        ["AutoPickupOxygenBox"] = "自动拾取氧气箱",
        ["PickupRadius"] = "拾取半径",

        // ConfigUI entries
        ["ToggleKey"] = "切换按键",
        ["Language"] = "语言",

        // DiveMap entries
        ["DiveMap"] = "潜水地图",
        ["ShowEscapePods"] = "显示逃生点",
        ["ShowFish"] = "显示普通鱼",
        ["ShowAggressiveFish"] = "显示攻击性鱼",
        ["ShowCatchableFish"] = "显示可捕捉鱼",
        ["ShowItems"] = "显示物品",
        ["ShowChests"] = "显示宝箱",
        ["MapSize"] = "小地图大小",
        ["MapOpacity"] = "小地图透明度",
        ["MiniMapZoom"] = "小地图缩放",
        ["MiniMapEnabled"] = "显示小地图",
        ["MiniMapPosition"] = "小地图位置",
        ["MiniMapOffsetX"] = "小地图水平偏移",
        ["MiniMapOffsetY"] = "小地图垂直偏移",
        ["ShowOres"] = "显示矿石",
        ["ShowDistantFish"] = "显示远处鱼",
        ["MarkerScale"] = "标记大小",
        // Legend labels
        ["Player"] = "玩家",
        ["Escape Point"] = "逃生点",
        ["Aggressive Fish"] = "攻击性鱼",
        ["Normal Fish"] = "普通鱼",
        ["Catchable Fish"] = "可捕捉鱼",
        ["Item"] = "物品",
        ["Ammo Box"] = "弹药箱",
        ["Chest"] = "宝箱",
        ["O2 Chest"] = "氧气箱",
        ["Material Chest"] = "材料箱",
        ["Ore"] = "矿石",
        ["ShowCrabTraps"] = "显示渔笼位置",
        ["Show fish trap spot markers on the map"] = "在地图上显示可放置渔笼的岩石缝隙标记",
        ["Trap Spot"] = "渔笼位置",
        // Big map help panel
        ["Scroll to Zoom"] = "滚轮缩放",
        ["Drag to Pan"] = "拖拽平移",
        ["Close"] = "关闭",
        ["TopRight"] = "右上",
        ["TopLeft"] = "左上",
        ["BottomRight"] = "右下",
        ["BottomLeft"] = "左下",

        // AutoSeahorseRace
        ["AutoSeahorseRace"] = "海马赛自动操作",
        ["Automatically control seahorse during racing"] = "在海马赛中自动控制海马",

        // iDiver extension
        ["iDiverExtension"] = "更多 iDiver 升级选项",
        ["Harpoon Damage Enhancement"] = "鱼叉伤害强化",
        ["Movement Speed Enhancement"] = "移动速度强化",
        ["Booster Speed Enhancement"] = "推进器速度强化",
        ["Booster Duration Enhancement"] = "推进器持续时间强化",
        ["Crab Trap Count Enhancement"] = "渔笼数量强化",
        ["Crab Trap Efficiency Enhancement"] = "渔笼效率强化",
        ["Drone Count Enhancement"] = "无人机次数强化",
        ["Ecology Protection"] = "生态保护",
        ["Enable extra iDiver upgrade options (harpoon damage, move speed, booster speed & duration). Disabling hides the UI and removes effects, but preserves your upgrade levels."] = "启用额外的 iDiver 升级选项（鱼叉伤害、移动速度、推进器速度和持续时间）。关闭后将隐藏 UI 并移除效果，但不会重置已升级的等级。",
        // iDiver status labels
        ["Damage"] = "伤害",
        ["Move Speed"] = "移动速度",
        ["Booster Speed"] = "推进速度",
        ["Duration"] = "持续时间",
        ["Trap Count"] = "渔笼数量",
        ["Catch Time"] = "捕获时间",
        ["Drone"] = "无人机",
        ["Population"] = "鱼群数量",

        // BettingExpansion
        ["BettingExpansion"] = "提升娱乐场下注上限",
        ["Expand casino mini-game betting from 10/50/100 to 10/50/100/500/1000/5000"] = "将娱乐场小游戏下注选项从 10/50/100 扩展为 10/50/100/500/1000/5000",

        // FishDensity (sub-config under iDiverExtension)
        ["FishDensityEnabled"] = "启用生态保护升级",
        ["Enable fish density enhancement (multiplier from Ecology Protection upgrade)"] = "启用生态保护升级（通过 iDiver 升级增加鱼群密度）",

        // Debug entries
        ["Debug"] = "调试",
        ["DebugLog"] = "调试日志",
        ["DiveMapDebugLog"] = "地图调试日志",
        ["AutoContinue"] = "自动继续游戏",
        ["Auto-continue to last save when reaching the title screen"] = "到达主菜单时自动继续上次存档",

        // QuickSceneSwitch entries
        ["QuickSceneSwitch"] = "快速切换场景",

        // Config descriptions
        ["Key to open/close the in-game settings panel"] = "打开/关闭游戏内设置面板的按键",
        ["UI language (Auto detects from game/system)"] = "界面语言（Auto 自动检测游戏/系统语言）",
        ["Open the scene-switch menu with a hotkey (no need to walk to the exit). WARNING: Using during cutscenes/story events may cause missions to be skipped or unexpected behavior."] = "按快捷键打开场景切换菜单（无需走到出口）。⚠️注意：在过场动画/剧情事件期间使用可能导致任务被跳过或出现意外情况。",
        ["Key to open/close the scene-switch menu"] = "打开/关闭场景切换菜单的按键",
        ["Enable automatic item pickup while diving"] = "潜水时启用自动拾取物品",
        ["Auto-pickup dead fish"] = "自动拾取死鱼",
        ["Auto-pickup dropped items"] = "自动拾取掉落物品",
        ["Auto-open treasure chests"] = "自动开启宝箱",
        ["Auto-pickup ammo boxes"] = "自动拾取弹药箱",
        ["Auto-pickup oxygen boxes (chests)"] = "自动拾取氧气箱",
        ["Radius around the player to auto-pick items (in game units). Oxygen boxes always use a fixed 1.0 radius regardless of this setting."] = "自动拾取物品的范围半径（游戏单位）。氧气箱固定使用 1.0 半径，不受此设置影响。",
        ["Enable the dive map HUD"] = "启用潜水地图 HUD",
        ["Key to toggle the enlarged map view"] = "切换大地图视图的按键",
        ["Show the minimap overlay during diving"] = "潜水时显示小地图",
        ["Screen corner for the minimap"] = "小地图在屏幕上的位置",
        ["Minimap horizontal offset from screen edge"] = "小地图距屏幕边缘的水平偏移",
        ["Minimap vertical offset from screen edge"] = "小地图距屏幕边缘的垂直偏移",
        ["Minimap size as fraction of screen height"] = "小地图大小（占屏幕高度的比例）",
        ["Minimap zoom level (higher = more zoomed in)"] = "小地图缩放级别（越大越放大）",
        ["Minimap opacity"] = "小地图透明度",
        ["Show escape pod/mirror markers on the map"] = "在地图上显示逃生点标记",
        ["Show ore/mineral markers on the map"] = "在地图上显示矿石标记",
        ["Show normal fish markers on the map (non-aggressive, non-catchable). Note: some large fish like tuna patrol fixed routes and can deal contact damage, but are classified as normal since they don't actively chase the player."] = "在地图上显示普通鱼标记（非攻击性、非可捕捉）。注意：部分大型鱼（如金枪鱼）沿固定路线巡游，接触会造成伤害，但因不会主动追击玩家而归为普通鱼。",
        ["Show aggressive fish markers on the map. These fish actively attack or chase the player (e.g. sharks, jellyfish, lionfish, triggerfish)."] = "在地图上显示攻击性鱼标记。这些鱼会主动攻击或追击玩家（如鲨鱼、水母、狮子鱼、炮弹鱼）。",
        ["Show catchable fish markers on the map. These fish flee from the player and can be caught with special tools (e.g. shrimp, seahorse)."] = "在地图上显示可捕捉鱼标记。这些鱼会逃离玩家，可用特殊工具捕捉（如虾、海马）。",
        ["Show item markers on the map"] = "在地图上显示物品标记",
        ["Show chest markers on the map"] = "在地图上显示宝箱标记",
        ["Scale multiplier for all map markers"] = "所有地图标记的缩放倍率",
        ["Show markers for distant fish that are streamed out by the game (frozen at last known position)"] = "显示被游戏流式卸载的远处鱼标记（冻结在最后已知位置）",
        ["Enable verbose debug logging for DiveMap diagnostics"] = "启用详细的 DiveMap 调试日志",

        // Reset button
        ["Reset All Settings"] = "重置所有设置",
        ["Confirm Reset?"] = "确认重置？",

        // Tree toolbar
        ["Expand All"] = "全部展开",
        ["Collapse All"] = "全部折叠",

        // KeyCode binding
        ["Press a key..."] = "请按键...",
    };

    public static ConfigEntry<ModLanguage> LanguageSetting;

    private static bool _langDebugDone;

    /// <summary>
    /// Returns true when the UI should display Chinese text.
    /// Priority: ConfigEntry override > game SaveSystem API > OS language.
    /// </summary>
    public static bool IsChinese()
    {
        if (LanguageSetting?.Value == ModLanguage.Chinese) return true;
        if (LanguageSetting?.Value == ModLanguage.English) return false;

        // Auto mode: query game's SaveSystem directly
        try
        {
            var saveSystem = Singleton<DR.Save.SaveSystem>._instance;
            if (saveSystem != null)
            {
                var optMgr = saveSystem.UserOptionManager;
                if (optMgr != null)
                {
                    var lang = optMgr.CurrentLanguage;
                    bool fromGame = lang == DR.Save.Languages.Chinese
                        || lang == DR.Save.Languages.ChineseTraditional;
                    LogAutoDetection("SaveSystem", $"CurrentLanguage={lang}({(int)lang})", fromGame);
                    return fromGame;
                }
            }
        }
        catch { }

        // Fallback: OS language (SaveSystem not ready yet)
        var sysLang = Application.systemLanguage;
        bool fallback = sysLang == SystemLanguage.Chinese
            || sysLang == SystemLanguage.ChineseSimplified
            || sysLang == SystemLanguage.ChineseTraditional;
        LogAutoDetection("OS", $"systemLanguage={sysLang}({(int)sysLang})", fallback);
        return fallback;
    }

    private static void LogAutoDetection(string source, string detail, bool isChinese)
    {
        if (_langDebugDone) return;
        try
        {
            Plugin.Log.LogInfo($"[I18n] Auto detect via {source}: {detail} → isChinese={isChinese}");
            _langDebugDone = true;
        }
        catch { }
    }

    /// <summary>
    /// Translate a key. Returns the Chinese string if IsChinese() and a
    /// translation exists; otherwise returns the key itself (English).
    /// </summary>
    public static string T(string key)
    {
        if (IsChinese() && ZhCn.TryGetValue(key, out var zh))
            return zh;
        return key;
    }
}
