using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using DaveDiverExpansion.Helpers;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DaveDiverExpansion.Features;

/// <summary>
/// In-game configuration panel built with uGUI.
/// Press F1 (configurable) to toggle the settings panel overlay.
/// Auto-discovers all ConfigEntry items registered by other features.
/// Uses an Il2CppInterop-registered MonoBehaviour for global Update (works in all scenes).
/// </summary>
public static class ConfigUI
{
    private static ConfigEntry<KeyCode> _toggleKey;
    private static ConfigEntry<bool> _autoContinue;
    private static ConfigFile _configFile;

    private static GameObject _canvasGO;
    private static GameObject _panelGO;
    private static GameObject _overlayGO;
    private static Text _titleText;
    private static Text _descText;
    private static bool _isVisible;
    private static bool _lastLangChinese;

    // "Press any key" listening state
    private static ConfigEntryBase _listeningEntry;
    private static Text _listeningText;

    // Track UI controls for cleanup on rebuild
    private static readonly List<GameObject> _sectionObjects = new();

    // Tree view: collapsed node keys ("G:<group>" / "G:<group>/<section>"), all keys seen
    // during the last rebuild (for "Collapse All"), and a deferred-rebuild flag (never
    // destroy the clicked node inside its own onClick).
    private static readonly HashSet<string> _collapsed = new();
    private static readonly List<string> _allNodeKeys = new();
    private static bool _needsRebuild;

    // Hover description: row RectTransform → raw description key (translated on display)
    private static readonly List<(RectTransform rt, string desc)> _rowDescs = new();
    private static RectTransform _viewportRT;
    private static ScrollRect _scrollRect;
    private static RectTransform _hoveredRow;
    private static float _hoverTimer;
    private const float HoverDelay = 0.3f;

    // Auto-continue state (only triggers once per game launch)
    private static bool _autoContinueDone;
    private static float _autoContinueTimer;

    // Reset button confirmation state
    private static bool _resetConfirming;
    private static float _resetConfirmTimer;
    private static Text _resetBtnText;
    private static Image _resetBtnImg;
    private const float ResetConfirmTimeout = 3f;

    public static void Init(ConfigFile config)
    {
        _configFile = config;

        _toggleKey = config.Bind(
            "ConfigUI", "ToggleKey", KeyCode.F1,
            "Key to open/close the in-game settings panel");

        I18n.LanguageSetting = config.Bind(
            "ConfigUI", "Language", ModLanguage.Auto,
            "UI language (Auto detects from game/system)");

        _autoContinue = config.Bind(
            "Debug", "AutoContinue", false,
            "Auto-continue to last save when reaching the title screen");

        // Register our MonoBehaviour in IL2CPP type system and spawn it
        ClassInjector.RegisterTypeInIl2Cpp<ConfigUIBehaviour>();
        var go = new GameObject("DDE_ConfigUIUpdater");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<ConfigUIBehaviour>();

        Plugin.Log.LogInfo("ConfigUI initialized (toggle key: " + _toggleKey.Value + ")");
    }

    /// <summary>
    /// Called every frame by ConfigUIBehaviour.Update to check hotkey.
    /// Builds UI lazily on first toggle.
    /// </summary>
    internal static void CheckToggle()
    {
        // Key-listen mode takes priority over all hotkeys
        if (_listeningEntry != null)
        {
            ProcessKeyListen();
            return;
        }

        // Detect language change while panel is visible and rebuild immediately
        if (_isVisible && _canvasGO != null)
        {
            bool isChinese = I18n.IsChinese();
            if (isChinese != _lastLangChinese)
            {
                _lastLangChinese = isChinese;
                if (_titleText != null)
                    _titleText.text = I18n.T("dave-diver-expansion2.0");
                RebuildEntries();
            }

            if (_needsRebuild)
            {
                _needsRebuild = false;
                RebuildEntries();
            }
            UpdateHoverDesc();
            HandleScrollWheel();
            TickResetConfirm();
        }

        try
        {
            // ESC closes panel if visible
            if (_isVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
                return;
            }

            if (!Input.GetKeyDown(_toggleKey.Value)) return;
        }
        catch
        {
            // Input may not be ready yet during early frames
            return;
        }

        if (_canvasGO == null)
            BuildUI();

        _isVisible = !_isVisible;
        _panelGO.SetActive(_isVisible);
        _overlayGO.SetActive(_isVisible);

        if (_isVisible)
        {
            _lastLangChinese = I18n.IsChinese();
            if (_titleText != null)
                _titleText.text = I18n.T("dave-diver-expansion2.0");
            RebuildEntries();
        }
    }

    private static void BuildUI()
    {
        // Canvas
        _canvasGO = new GameObject("DDE_ConfigCanvas");
        UnityEngine.Object.DontDestroyOnLoad(_canvasGO);

        var canvas = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        var scaler = _canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _canvasGO.AddComponent<GraphicRaycaster>();

        // EventSystem (only if none exists)
        if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("DDE_EventSystem");
            UnityEngine.Object.DontDestroyOnLoad(esGO);
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Dimmed background overlay (catches clicks outside panel to close)
        _overlayGO = CreateUIObject("Overlay", _canvasGO);
        var overlayRT = _overlayGO.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.sizeDelta = Vector2.zero;
        var overlayImg = _overlayGO.AddComponent<Image>();
        overlayImg.color = new Color(0, 0, 0, 0.4f);
        var overlayBtn = _overlayGO.AddComponent<Button>();
        overlayBtn.onClick.AddListener((UnityAction)delegate { Hide(); });

        // Panel
        _panelGO = CreateUIObject("Panel", _canvasGO);
        var panelRT = _panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(520, 620);
        panelRT.anchoredPosition = Vector2.zero;
        var panelImg = _panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.12f, 0.16f, 0.96f);

        // Block clicks from falling through the panel to the overlay
        _panelGO.AddComponent<Button>().onClick.AddListener((UnityAction)delegate { });

        var panelLayout = _panelGO.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(16, 16, 12, 12);
        panelLayout.spacing = 4;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        // Header row
        var headerGO = CreateUIObject("Header", _panelGO);
        var headerLayout = headerGO.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = false;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.spacing = 8;
        var headerLE = headerGO.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 40;
        headerLE.flexibleHeight = 0;

        // Title
        var titleGO = CreateUIObject("Title", headerGO);
        _titleText = titleGO.AddComponent<Text>();
        _titleText.text = I18n.T("dave-diver-expansion2.0");
        _titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _titleText.fontSize = 22;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.color = new Color(0.9f, 0.9f, 0.9f);
        _titleText.alignment = TextAnchor.MiddleLeft;
        var titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.flexibleWidth = 1;

        // Close button
        var closeBtnGO = CreateUIObject("CloseBtn", headerGO);
        var closeBtnImg = closeBtnGO.AddComponent<Image>();
        closeBtnImg.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        var closeBtn = closeBtnGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeBtnImg;
        closeBtn.onClick.AddListener((UnityAction)delegate { Hide(); });
        var closeBtnLE = closeBtnGO.AddComponent<LayoutElement>();
        closeBtnLE.preferredWidth = 36;
        closeBtnLE.preferredHeight = 36;

        var closeTxtGO = CreateUIObject("X", closeBtnGO);
        var closeTxt = closeTxtGO.AddComponent<Text>();
        closeTxt.text = "X";
        closeTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        closeTxt.fontSize = 20;
        closeTxt.fontStyle = FontStyle.Bold;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAnchor.MiddleCenter;
        var closeTxtRT = closeTxtGO.GetComponent<RectTransform>();
        closeTxtRT.anchorMin = Vector2.zero;
        closeTxtRT.anchorMax = Vector2.one;
        closeTxtRT.sizeDelta = Vector2.zero;

        // Divider
        var divGO = CreateUIObject("Divider", _panelGO);
        var divImg = divGO.AddComponent<Image>();
        divImg.color = new Color(0.4f, 0.4f, 0.5f, 0.6f);
        var divLE = divGO.AddComponent<LayoutElement>();
        divLE.preferredHeight = 2;
        divLE.flexibleHeight = 0;

        // ScrollRect
        var scrollGO = CreateUIObject("Scroll", _panelGO);
        var scrollLE = scrollGO.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1;
        scrollLE.flexibleWidth = 1;

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30;

        // Viewport (with mask)
        var viewportGO = CreateUIObject("Viewport", scrollGO);
        var viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.sizeDelta = Vector2.zero;
        viewportGO.AddComponent<RectMask2D>();
        var viewportImg = viewportGO.AddComponent<Image>();
        viewportImg.color = new Color(0, 0, 0, 0);

        // Content
        var contentGO = CreateUIObject("Content", viewportGO);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.sizeDelta = new Vector2(0, 0);
        var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(4, 4, 4, 4);
        contentLayout.spacing = 6;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;
        _viewportRT = viewportRT;
        _scrollRect = scrollRect;

        // Bottom divider
        var div2GO = CreateUIObject("Divider2", _panelGO);
        var div2Img = div2GO.AddComponent<Image>();
        div2Img.color = new Color(0.4f, 0.4f, 0.5f, 0.6f);
        var div2LE = div2GO.AddComponent<LayoutElement>();
        div2LE.preferredHeight = 2;
        div2LE.flexibleHeight = 0;

        // Description bar — auto-sizes height to fit long descriptions
        var descGO = CreateUIObject("DescBar", _panelGO);
        var descLE = descGO.AddComponent<LayoutElement>();
        descLE.minHeight = 20;
        descLE.flexibleHeight = 0;
        _descText = descGO.AddComponent<Text>();
        _descText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _descText.fontSize = 13;
        _descText.color = new Color(0.65f, 0.65f, 0.7f);
        _descText.alignment = TextAnchor.MiddleLeft;
        _descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _descText.verticalOverflow = VerticalWrapMode.Overflow;
        _descText.text = "";
        var descFitter = descGO.AddComponent<ContentSizeFitter>();
        descFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Start hidden
        _panelGO.SetActive(false);
        _overlayGO.SetActive(false);
    }

    // ---- Display grouping: merge raw config sections into a feature tree ----
    // Raw config sections stay unchanged (so existing .cfg files keep working); only the
    // panel rendering is regrouped.  Hotkeys live with the feature they drive.

    private static readonly string[] GroupOrder =
    {
        "Diving", "Farming", "Sushi", "Map", "Automation", "System"
    };

    // Fallback: config section -> display group (used only for entries no FeatureDef matches)
    private static readonly Dictionary<string, string> SectionGroup = new()
    {
        ["Diving"] = "Diving",
        ["Harpoon"] = "Diving",
        ["iDiverExtension"] = "Diving",
        ["Boat"] = "Diving",
        ["AuraHud"] = "Diving",

        ["Farm"] = "Farming",
        ["FishFarm"] = "Farming",

        ["Sushi"] = "Sushi",

        ["DiveMap"] = "Map",

        ["AutoPickup"] = "Automation",
        ["AutoSeahorseRace"] = "Automation",
        ["BettingExpansion"] = "Automation",
        ["QuickSceneSwitch"] = "Automation",

        ["ConfigUI"] = "System",
        ["SuperDave"] = "System",
        ["Hotkeys"] = "System",
        ["Debug"] = "System",
    };

    // Config entries that no longer exist (removed features) but may still linger in the .cfg
    // file on disk.  BepInEx keeps unbound lines, so hide them explicitly.
    private static readonly HashSet<string> IgnoredEntries = new()
    {
        "Diving/Enable Large Pickups",   // removed, replaced by Auto Call Drone
    };

    private static bool In(string key, params string[] keys) => Array.IndexOf(keys, key) >= 0;

    /// <summary>
    /// A feature node in the tree: which group it belongs to, its label, and how to recognise
    /// its config entries.  Matched in declaration order; first match wins.
    /// </summary>
    private sealed class FeatureDef
    {
        public readonly string Group;
        public readonly string Name;
        public readonly Func<string, string, bool> Match;
        public FeatureDef(string group, string name, Func<string, string, bool> match)
        {
            Group = group; Name = name; Match = match;
        }
    }

    private static readonly FeatureDef[] FeatureDefs =
    {
        // ---- System ----
        new("System", "Master Switch", (s, k) => s == "SuperDave"),
        new("System", "Settings Panel", (s, k) => s == "ConfigUI"),
        new("System", "Hotkey Modifier", (s, k) => s == "Hotkeys" && k == "Modifier"),
        new("System", "Debug", (s, k) => s == "Debug"),

        // ---- Diving ----
        new("Diving", "Survival", (s, k) => s == "Diving" && In(k,
            "Diving - Infinite Oxygen", "Diving - Invincible",
            "Diving - Weightless Items", "Diving - Disable Item Info Popups")),
        new("Diving", "Speed", (s, k) => (s == "Diving" && k == "Diving - Speed Boost") || s == "Boat"),
        new("Diving", "Ammo", (s, k) => s == "Diving" && k == "Diving - Infinite Bullets"),
        new("Diving", "Drones & Traps", (s, k) => s == "Diving" && In(k,
            "Diving - Infinite Drones", "Diving - Infinite Crab Traps")),
        new("Diving", "Toxic Aura", (s, k) =>
            (s == "Diving" && k.StartsWith("Diving - Toxic Aura")) ||
            (s == "Hotkeys" && In(k, "Toggle Toxic Aura", "Change Toxic Aura Mode"))),
        new("Diving", "Auto Call Drone", (s, k) => s == "Diving" && k.StartsWith("Diving - Auto Call Drone")),
        new("Diving", "Weapon Control", (s, k) => s == "Hotkeys" && In(k,
            "Heal", "Net Gun", "Tranq Gun", "Sniper", "Weapon Up", "Weapon Down")),
        new("Diving", "Harpoon", (s, k) => s == "Harpoon"),
        new("Diving", "iDiver Upgrades", (s, k) => s == "iDiverExtension"),
        new("Diving", "Status HUD", (s, k) => s == "AuraHud"),

        // ---- Automation ----
        new("Automation", "Auto Pickup", (s, k) => s == "AutoPickup"),
        new("Automation", "Seahorse Race", (s, k) => s == "AutoSeahorseRace"),
        new("Automation", "Casino Betting", (s, k) => s == "BettingExpansion"),
        new("Automation", "Quick Scene Switch", (s, k) => s == "QuickSceneSwitch"),

        // ---- Map ----
        new("Map", "Dive Map", (s, k) => s == "DiveMap" && In(k, "Enabled", "ToggleKey")),
        new("Map", "Mini Map", (s, k) => s == "DiveMap" && k.StartsWith("MiniMap")),
        new("Map", "Map Display", (s, k) => s == "DiveMap" && In(k, "MapSize", "MapOpacity", "MarkerScale")),
        new("Map", "Markers", (s, k) => s == "DiveMap" && k.StartsWith("Show")),

        // ---- Farming ----
        new("Farming", "Farm", (s, k) => s == "Farm"),
        new("Farming", "Fish Farm", (s, k) => s == "FishFarm"),

        // ---- Sushi ----
        new("Sushi", "Sushi Bar", (s, k) => s == "Sushi"),
    };

    // Per-section entry ordering (key name -> display position); applied inside a feature
    // when all of its entries come from the same config section.
    private static readonly Dictionary<string, string[]> EntryOrder = new()
    {
        ["DiveMap"] = new[] {
            "Enabled", "ToggleKey",
            "MiniMapEnabled", "MiniMapPosition", "MiniMapOffsetX", "MiniMapOffsetY",
            "MiniMapZoom", "MapSize", "MapOpacity", "MarkerScale",
            "ShowEscapePods", "ShowOres", "ShowFish", "ShowAggressiveFish", "ShowCatchableFish", "ShowDistantFish", "ShowItems", "ShowChests", "ShowCrabTraps"
        },
        ["Debug"] = new[] { "DebugLog", "DiveMapDebugLog", "AutoContinue" }
    };

    private static void RebuildEntries()
    {
        if (_descText != null) _descText.text = "";
        _rowDescs.Clear();
        _allNodeKeys.Clear();

        // Clear old section objects
        foreach (var go in _sectionObjects)
        {
            if (go != null)
                UnityEngine.Object.Destroy(go);
        }
        _sectionObjects.Clear();

        // Find the Content transform
        var contentRT = _panelGO.transform.Find("Scroll/Viewport/Content");
        if (contentRT == null)
        {
            Plugin.Log.LogError("ConfigUI: Content transform not found");
            return;
        }

        // Bucket every entry into: group -> feature -> entries (insertion order preserved)
        var groups = new Dictionary<string, Dictionary<string, List<ConfigEntryBase>>>();
        var featureOrderByGroup = new Dictionary<string, List<string>>();
        foreach (var kv in _configFile)
        {
            string section = kv.Key.Section;
            string key = kv.Key.Key;
            if (IgnoredEntries.Contains(section + "/" + key)) continue;

            ResolveFeature(section, key, out string group, out string feature);

            if (!groups.TryGetValue(group, out var feats))
            {
                groups[group] = feats = new Dictionary<string, List<ConfigEntryBase>>();
                featureOrderByGroup[group] = new List<string>();
            }
            if (!feats.TryGetValue(feature, out var list))
            {
                feats[feature] = list = new List<ConfigEntryBase>();
                featureOrderByGroup[group].Add(feature);
            }
            list.Add(kv.Value);
        }

        // Reorder each group's features to follow the FeatureDefs declaration order
        foreach (var group in new List<string>(featureOrderByGroup.Keys))
        {
            var declared = new List<string>();
            foreach (var f in FeatureDefs)
                if (f.Group == group && groups[group].ContainsKey(f.Name) && !declared.Contains(f.Name))
                    declared.Add(f.Name);
            foreach (var name in featureOrderByGroup[group])
                if (!declared.Contains(name)) declared.Add(name); // fallback / unknown
            featureOrderByGroup[group] = declared;
        }

        // Ordered list of groups (unknown groups appended at the end)
        var orderedGroups = new List<string>();
        foreach (var g in GroupOrder)
            if (groups.ContainsKey(g)) orderedGroups.Add(g);
        foreach (var g in groups.Keys)
            if (!orderedGroups.Contains(g)) orderedGroups.Add(g);

        // Tree toolbar (Expand / Collapse all)
        CreateTreeToolbar(contentRT.gameObject);

        // ---- Tree: Group (depth 0) -> Feature (depth 1) -> Entries (depth 2) ----
        foreach (var group in orderedGroups)
        {
            string groupKey = "G:" + group;

            AddTreeNode(contentRT.gameObject, groupKey, I18n.T(group), 0, 18,
                new Color(0.55f, 0.78f, 1f), new Color(0.16f, 0.19f, 0.27f, 0.95f));
            if (_collapsed.Contains(groupKey)) continue;

            var feats = groups[group];
            foreach (var feature in featureOrderByGroup[group])
            {
                var entries = feats[feature];
                string featKey = groupKey + "/" + feature;

                AddTreeNode(contentRT.gameObject, featKey, I18n.T(feature), 1, 15,
                    new Color(0.68f, 0.74f, 0.86f), new Color(0.13f, 0.14f, 0.19f, 0.9f));
                if (_collapsed.Contains(featKey)) continue;

                // Apply the section order only when the feature is single-section
                string onlySection = entries[0].Definition.Section;
                bool singleSection = true;
                foreach (var e in entries)
                {
                    if (e.Definition.Section != onlySection) { singleSection = false; break; }
                }
                if (singleSection && EntryOrder.TryGetValue(onlySection, out var keyOrder))
                    SortEntries(entries, keyOrder);

                foreach (var entry in entries)
                    _sectionObjects.Add(CreateEntryRow(contentRT.gameObject, entry, 2));
            }
        }

        // Reset button at the bottom
        CreateResetButton(contentRT.gameObject);
    }

    private static string ResolveGroup(string section)
    {
        if (SectionGroup.TryGetValue(section, out var g)) return g;
        return "System";
    }

    // Returns the (group, feature) that owns a config entry.  Falls back to the raw section
    // name (grouped via SectionGroup) for entries no FeatureDef matches.
    private static void ResolveFeature(string section, string key, out string group, out string feature)
    {
        foreach (var f in FeatureDefs)
        {
            if (f.Match(section, key)) { group = f.Group; feature = f.Name; return; }
        }
        group = ResolveGroup(section);
        feature = section;
    }

    /// <summary>Expand / Collapse-all toolbar shown above the tree.</summary>
    private static void CreateTreeToolbar(GameObject parent)
    {
        var rowGO = CreateUIObject("TreeToolbar", parent);
        _sectionObjects.Add(rowGO);
        var hl = rowGO.AddComponent<HorizontalLayoutGroup>();
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        hl.childAlignment = TextAnchor.MiddleRight;
        hl.spacing = 6;
        var le = rowGO.AddComponent<LayoutElement>();
        le.preferredHeight = 26;
        le.flexibleHeight = 0;

        AddToolbarButton(rowGO, I18n.T("Expand All"), (UnityAction)delegate
        {
            _collapsed.Clear();
            _needsRebuild = true;
        });
        AddToolbarButton(rowGO, I18n.T("Collapse All"), (UnityAction)delegate
        {
            foreach (var k in _allNodeKeys) _collapsed.Add(k);
            _needsRebuild = true;
        });
    }

    private static void AddToolbarButton(GameObject parent, string text, UnityAction onClick)
    {
        var btnGO = CreateUIObject("ToolbarBtn", parent);
        var le = btnGO.AddComponent<LayoutElement>();
        le.preferredWidth = 100;
        le.preferredHeight = 24;
        le.flexibleWidth = 0;
        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0.25f, 0.28f, 0.36f, 0.95f);
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var txtGO = CreateUIObject("Text", btnGO);
        var rt = txtGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        var t = txtGO.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 13;
        t.color = new Color(0.9f, 0.9f, 0.9f);
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
    }

    /// <summary>
    /// A collapsible tree node header.  Clicking anywhere on the row toggles collapse;
    /// the actual rebuild is deferred to the next Update (never destroy the clicked
    /// button inside its own onClick).
    /// </summary>
    private static void AddTreeNode(GameObject parent, string nodeKey, string text, int depth, int fontSize, Color textColor, Color bgColor)
    {
        _allNodeKeys.Add(nodeKey);

        var rowGO = CreateUIObject("Node_" + nodeKey, parent);
        _sectionObjects.Add(rowGO);
        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = fontSize + 14;
        rowLE.flexibleHeight = 0;

        var bg = rowGO.AddComponent<Image>();
        bg.color = bgColor;
        var btn = rowGO.AddComponent<Button>();
        btn.targetGraphic = bg;

        var hl = rowGO.AddComponent<HorizontalLayoutGroup>();
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.spacing = 4;
        hl.padding = new RectOffset(depth * 18, 4, 0, 0);

        bool collapsed = _collapsed.Contains(nodeKey);

        var arrowGO = CreateUIObject("Arrow", rowGO);
        var arrowText = arrowGO.AddComponent<Text>();
        arrowText.text = collapsed ? ">" : "v";
        arrowText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        arrowText.fontSize = fontSize - 2;
        arrowText.color = textColor;
        arrowText.alignment = TextAnchor.MiddleCenter;
        arrowText.raycastTarget = false;
        var arrowLE = arrowGO.AddComponent<LayoutElement>();
        arrowLE.preferredWidth = 16;
        arrowLE.flexibleWidth = 0;

        var labelGO = CreateUIObject("Label", rowGO);
        var labelText = labelGO.AddComponent<Text>();
        labelText.text = text;
        labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        labelText.fontSize = fontSize;
        labelText.fontStyle = FontStyle.Bold;
        labelText.color = textColor;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.raycastTarget = false;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1;

        string key = nodeKey;
        btn.onClick.AddListener((UnityAction)delegate
        {
            if (!_collapsed.Add(key))
                _collapsed.Remove(key);
            _needsRebuild = true;
        });
    }

    private static void SortEntries(List<ConfigEntryBase> entries, string[] keyOrder)
    {
        var orderMap = new Dictionary<string, int>();
        for (int i = 0; i < keyOrder.Length; i++)
            orderMap[keyOrder[i]] = i;
        entries.Sort((a, b) =>
        {
            bool aHas = orderMap.TryGetValue(a.Definition.Key, out int aIdx);
            bool bHas = orderMap.TryGetValue(b.Definition.Key, out int bIdx);
            if (aHas && bHas) return aIdx.CompareTo(bIdx);
            if (aHas) return -1;
            if (bHas) return 1;
            return 0;
        });
    }

    private static void CreateResetButton(GameObject parent)
    {
        _resetConfirming = false;
        _resetConfirmTimer = 0;

        // Spacer
        var spacerGO = CreateUIObject("ResetSpacer", parent);
        _sectionObjects.Add(spacerGO);
        var spacerLE = spacerGO.AddComponent<LayoutElement>();
        spacerLE.preferredHeight = 8;
        spacerLE.flexibleHeight = 0;

        // Button row — centered
        var rowGO = CreateUIObject("ResetRow", parent);
        _sectionObjects.Add(rowGO);
        var rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 36;
        rowLE.flexibleHeight = 0;

        var btnGO = CreateUIObject("ResetBtn", rowGO);
        _resetBtnImg = btnGO.AddComponent<Image>();
        _resetBtnImg.color = new Color(0.5f, 0.2f, 0.2f, 0.9f);
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredWidth = 200;
        btnLE.preferredHeight = 32;

        var txtGO = CreateUIObject("Text", btnGO);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;
        _resetBtnText = txtGO.AddComponent<Text>();
        _resetBtnText.text = I18n.T("Reset All Settings");
        _resetBtnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _resetBtnText.fontSize = 15;
        _resetBtnText.color = Color.white;
        _resetBtnText.alignment = TextAnchor.MiddleCenter;

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = _resetBtnImg;
        btn.onClick.AddListener((UnityAction)delegate { OnResetClicked(); });
    }

    private static void OnResetClicked()
    {
        if (!_resetConfirming)
        {
            _resetConfirming = true;
            _resetConfirmTimer = ResetConfirmTimeout;
            if (_resetBtnText != null) _resetBtnText.text = I18n.T("Confirm Reset?");
            if (_resetBtnImg != null) _resetBtnImg.color = new Color(0.7f, 0.15f, 0.15f, 0.95f);
            return;
        }

        // Confirmed — reset all entries to defaults
        foreach (var kv in _configFile)
            kv.Value.BoxedValue = kv.Value.DefaultValue;

        _resetConfirming = false;
        Plugin.Log.LogInfo("ConfigUI: all settings reset to defaults");
        RebuildEntries();
    }

    /// <summary>
    /// Ticks the reset button confirmation timeout. Called from CheckToggle when visible.
    /// </summary>
    private static void TickResetConfirm()
    {
        if (!_resetConfirming) return;
        _resetConfirmTimer -= Time.unscaledDeltaTime;
        if (_resetConfirmTimer <= 0)
        {
            _resetConfirming = false;
            if (_resetBtnText != null) _resetBtnText.text = I18n.T("Reset All Settings");
            if (_resetBtnImg != null) _resetBtnImg.color = new Color(0.5f, 0.2f, 0.2f, 0.9f);
        }
    }

    private static GameObject CreateEntryRow(GameObject parent, ConfigEntryBase entry, int depth)
    {
        var rowGO = CreateUIObject("Row_" + entry.Definition.Key, parent);
        var rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 36;
        rowLE.flexibleHeight = 0;

        // Tree indentation (aligns entries under their node header)
        if (depth > 0)
        {
            var indentGO = CreateUIObject("Indent", rowGO);
            var indentLE = indentGO.AddComponent<LayoutElement>();
            indentLE.preferredWidth = depth * 18;
            indentLE.flexibleWidth = 0;
        }

        // Label
        var labelGO = CreateUIObject("Label", rowGO);
        var labelText = labelGO.AddComponent<Text>();
        labelText.text = I18n.T(entry.Definition.Key);
        labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        labelText.fontSize = 16;
        labelText.color = new Color(0.85f, 0.85f, 0.85f);
        labelText.alignment = TextAnchor.MiddleLeft;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 200;
        labelLE.flexibleWidth = 0;

        // Control based on type
        if (entry.SettingType == typeof(bool))
            CreateBoolControl(rowGO, entry);
        else if (entry.SettingType == typeof(float))
            CreateFloatControl(rowGO, entry);
        else if (entry.SettingType == typeof(int))
            CreateIntControl(rowGO, entry);
        else if (entry.SettingType == typeof(KeyCode))
            CreateKeyCodeControl(rowGO, entry);
        else if (entry.SettingType == typeof(string))
            CreateStringControl(rowGO, entry);
        else if (entry.SettingType.IsEnum)
            CreateEnumControl(rowGO, entry);
        else
            CreateStringControl(rowGO, entry);

        // Register row for hover description (checked via RectTransform hit test in Update)
        string desc = entry.Description?.Description;
        if (!string.IsNullOrEmpty(desc))
            _rowDescs.Add((rowGO.GetComponent<RectTransform>(), desc));

        return rowGO;
    }

    private static void CreateBoolControl(GameObject parent, ConfigEntryBase entry)
    {
        var toggleGO = CreateUIObject("Toggle", parent);
        var toggleLE = toggleGO.AddComponent<LayoutElement>();
        toggleLE.preferredWidth = 30;
        toggleLE.preferredHeight = 30;
        toggleLE.flexibleHeight = 0;
        toggleLE.flexibleWidth = 0;

        var bgImg = toggleGO.AddComponent<Image>();
        bgImg.color = new Color(0.35f, 0.35f, 0.4f); // brighter so OFF state is visible

        var checkGO = CreateUIObject("Checkmark", toggleGO);
        var checkRT = checkGO.GetComponent<RectTransform>();
        checkRT.anchorMin = new Vector2(0.15f, 0.15f);
        checkRT.anchorMax = new Vector2(0.85f, 0.85f);
        checkRT.sizeDelta = Vector2.zero;
        var checkImg = checkGO.AddComponent<Image>();
        checkImg.color = new Color(0.3f, 0.8f, 0.4f);

        var toggle = toggleGO.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        toggle.isOn = (bool)entry.BoxedValue;
        toggle.onValueChanged.AddListener((UnityAction<bool>)delegate(bool val)
        {
            entry.BoxedValue = val;
        });
    }

    private static void CreateFloatControl(GameObject parent, ConfigEntryBase entry)
    {
        float min = 0f, max = 100f;
        if (entry.Description?.AcceptableValues is AcceptableValueRange<float> range)
        {
            min = range.MinValue;
            max = range.MaxValue;
        }
        else
        {
            var def = (float)entry.DefaultValue;
            min = 0f;
            max = Mathf.Max(def * 4f, 10f);
        }

        var slider = CreateSlider(parent, min, max, (float)entry.BoxedValue, false);

        var valueLabelGO = CreateUIObject("Value", parent);
        var valueText = valueLabelGO.AddComponent<Text>();
        valueText.text = ((float)entry.BoxedValue).ToString("F1");
        valueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        valueText.fontSize = 15;
        valueText.color = new Color(0.9f, 0.9f, 0.9f);
        valueText.alignment = TextAnchor.MiddleRight;
        var valueLE = valueLabelGO.AddComponent<LayoutElement>();
        valueLE.preferredWidth = 50;
        valueLE.flexibleWidth = 0;
        valueLE.flexibleHeight = 0;

        slider.onValueChanged.AddListener((UnityAction<float>)delegate(float val)
        {
            entry.BoxedValue = val;
            valueText.text = val.ToString("F1");
        });
    }

    private static void CreateIntControl(GameObject parent, ConfigEntryBase entry)
    {
        float min = 0, max = 100;
        if (entry.Description?.AcceptableValues is AcceptableValueRange<int> range)
        {
            min = range.MinValue;
            max = range.MaxValue;
        }
        else
        {
            var def = (int)entry.DefaultValue;
            min = 0;
            max = Mathf.Max(def * 4, 100);
        }

        var slider = CreateSlider(parent, min, max, (int)entry.BoxedValue, true);

        var valueLabelGO = CreateUIObject("Value", parent);
        var valueText = valueLabelGO.AddComponent<Text>();
        valueText.text = entry.BoxedValue.ToString();
        valueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        valueText.fontSize = 15;
        valueText.color = new Color(0.9f, 0.9f, 0.9f);
        valueText.alignment = TextAnchor.MiddleRight;
        var valueLE = valueLabelGO.AddComponent<LayoutElement>();
        valueLE.preferredWidth = 50;
        valueLE.flexibleWidth = 0;
        valueLE.flexibleHeight = 0;

        slider.onValueChanged.AddListener((UnityAction<float>)delegate(float val)
        {
            entry.BoxedValue = (int)val;
            valueText.text = ((int)val).ToString();
        });
    }

    /// <summary>
    /// Creates a Slider inside a fixed-height wrapper container.
    /// The wrapper's LayoutElement controls size; the Slider fills the wrapper via anchors
    /// so the Slider component cannot overflow its bounds.
    /// </summary>
    private static Slider CreateSlider(GameObject parent, float min, float max, float value, bool wholeNumbers)
    {
        // Wrapper container controlled by layout
        var wrapperGO = CreateUIObject("SliderWrapper", parent);
        var wrapperLE = wrapperGO.AddComponent<LayoutElement>();
        wrapperLE.flexibleWidth = 1;
        wrapperLE.preferredHeight = 24;
        wrapperLE.flexibleHeight = 0;

        // Slider fills wrapper via anchors (not layout)
        var sliderGO = CreateUIObject("Slider", wrapperGO);
        var sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = Vector2.zero;
        sliderRT.anchorMax = Vector2.one;
        sliderRT.sizeDelta = Vector2.zero;
        var sliderBG = sliderGO.AddComponent<Image>();
        sliderBG.color = new Color(0.25f, 0.25f, 0.3f);

        // Fill area (middle 50% height for a thin track look)
        var fillAreaGO = CreateUIObject("FillArea", sliderGO);
        var fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1, 0.75f);
        fillAreaRT.offsetMin = new Vector2(5, 0);
        fillAreaRT.offsetMax = new Vector2(-5, 0);

        var fillGO = CreateUIObject("Fill", fillAreaGO);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.6f, 0.9f);

        // Handle slide area
        var handleAreaGO = CreateUIObject("HandleArea", sliderGO);
        var handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(5, 0);
        handleAreaRT.offsetMax = new Vector2(-5, 0);

        var handleGO = CreateUIObject("Handle", handleAreaGO);
        var handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(14, 0);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = new Color(0.85f, 0.85f, 0.9f);

        var slider = sliderGO.AddComponent<Slider>();
        slider.targetGraphic = handleImg;
        slider.fillRect = fillGO.GetComponent<RectTransform>();
        slider.handleRect = handleRT;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.value = value;

        return slider;
    }

    private static void CreateStringControl(GameObject parent, ConfigEntryBase entry)
    {
        var inputGO = CreateUIObject("InputField", parent);
        var inputLE = inputGO.AddComponent<LayoutElement>();
        inputLE.flexibleWidth = 1;
        inputLE.preferredHeight = 30;
        var inputBG = inputGO.AddComponent<Image>();
        inputBG.color = new Color(0.2f, 0.2f, 0.25f);

        var textGO = CreateUIObject("Text", inputGO);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(6, 2);
        textRT.offsetMax = new Vector2(-6, -2);
        var text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 15;
        text.color = new Color(0.9f, 0.9f, 0.9f);
        text.supportRichText = false;

        var inputField = inputGO.AddComponent<InputField>();
        inputField.textComponent = text;
        inputField.text = entry.BoxedValue?.ToString() ?? "";
        inputField.onEndEdit.AddListener((UnityAction<string>)delegate(string val)
        {
            try
            {
                if (entry.SettingType == typeof(string))
                    entry.BoxedValue = val;
                else
                    entry.BoxedValue = System.ComponentModel.TypeDescriptor
                        .GetConverter(entry.SettingType).ConvertFromString(val);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"ConfigUI: Failed to parse '{val}' for {entry.Definition.Key}: {e.Message}");
            }
        });
    }

    private static void CreateEnumControl(GameObject parent, ConfigEntryBase entry)
    {
        var dropdownGO = CreateUIObject("Dropdown", parent);
        var dropdownLE = dropdownGO.AddComponent<LayoutElement>();
        dropdownLE.flexibleWidth = 1;
        dropdownLE.preferredHeight = 30;
        var dropdownBG = dropdownGO.AddComponent<Image>();
        dropdownBG.color = new Color(0.2f, 0.2f, 0.25f);

        var captionGO = CreateUIObject("Label", dropdownGO);
        var captionRT = captionGO.GetComponent<RectTransform>();
        captionRT.anchorMin = Vector2.zero;
        captionRT.anchorMax = Vector2.one;
        captionRT.offsetMin = new Vector2(8, 2);
        captionRT.offsetMax = new Vector2(-28, -2);
        var captionText = captionGO.AddComponent<Text>();
        captionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        captionText.fontSize = 15;
        captionText.color = new Color(0.9f, 0.9f, 0.9f);
        captionText.alignment = TextAnchor.MiddleLeft;

        var arrowGO = CreateUIObject("Arrow", dropdownGO);
        var arrowRT = arrowGO.GetComponent<RectTransform>();
        arrowRT.anchorMin = new Vector2(1, 0);
        arrowRT.anchorMax = new Vector2(1, 1);
        arrowRT.pivot = new Vector2(1, 0.5f);
        arrowRT.sizeDelta = new Vector2(24, 0);
        var arrowText = arrowGO.AddComponent<Text>();
        arrowText.text = "v";
        arrowText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        arrowText.fontSize = 14;
        arrowText.color = new Color(0.7f, 0.7f, 0.7f);
        arrowText.alignment = TextAnchor.MiddleCenter;

        // Template (hidden, used by Dropdown to create popup)
        var templateGO = CreateUIObject("Template", dropdownGO);
        templateGO.SetActive(false);
        var templateRT = templateGO.GetComponent<RectTransform>();
        templateRT.anchorMin = new Vector2(0, 0);
        templateRT.anchorMax = new Vector2(1, 0);
        templateRT.pivot = new Vector2(0.5f, 1);
        templateRT.sizeDelta = new Vector2(0, 150);
        var templateImg = templateGO.AddComponent<Image>();
        templateImg.color = new Color(0.18f, 0.18f, 0.22f);
        var templateScroll = templateGO.AddComponent<ScrollRect>();
        templateScroll.horizontal = false;
        templateScroll.movementType = ScrollRect.MovementType.Clamped;

        var viewportGO = CreateUIObject("Viewport", templateGO);
        var viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.pivot = new Vector2(0, 1);
        viewportRT.sizeDelta = Vector2.zero;
        viewportGO.AddComponent<RectMask2D>();
        var viewportImg = viewportGO.AddComponent<Image>();
        viewportImg.color = new Color(0, 0, 0, 0);

        // Content — NO VerticalLayoutGroup / ContentSizeFitter here.
        // Dropdown.Show() manually positions items and sets content height.
        var contentGO = CreateUIObject("Content", viewportGO);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.sizeDelta = new Vector2(0, 28);

        // Item template — full width, height = 28.
        // Dropdown.Show() clones this, preserves anchorMin/Max.x, overrides y.
        var itemGO = CreateUIObject("Item", contentGO);
        var itemRT = itemGO.GetComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0, 0.5f);
        itemRT.anchorMax = new Vector2(1, 0.5f);
        itemRT.sizeDelta = new Vector2(0, 28);
        var itemToggle = itemGO.AddComponent<Toggle>();

        var itemBG = CreateUIObject("ItemBG", itemGO);
        var itemBGRT = itemBG.GetComponent<RectTransform>();
        itemBGRT.anchorMin = Vector2.zero;
        itemBGRT.anchorMax = Vector2.one;
        itemBGRT.sizeDelta = Vector2.zero;
        var itemBGImg = itemBG.AddComponent<Image>();
        itemBGImg.color = new Color(0.25f, 0.25f, 0.3f);

        var itemLabelGO = CreateUIObject("ItemLabel", itemGO);
        var itemLabelRT = itemLabelGO.GetComponent<RectTransform>();
        itemLabelRT.anchorMin = Vector2.zero;
        itemLabelRT.anchorMax = Vector2.one;
        itemLabelRT.offsetMin = new Vector2(8, 2);
        itemLabelRT.offsetMax = new Vector2(-8, -2);
        var itemLabel = itemLabelGO.AddComponent<Text>();
        itemLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        itemLabel.fontSize = 15;
        itemLabel.color = new Color(0.9f, 0.9f, 0.9f);
        itemLabel.alignment = TextAnchor.MiddleLeft;

        itemToggle.targetGraphic = itemBGImg;

        templateScroll.viewport = viewportRT;
        templateScroll.content = contentRT;

        var dropdown = dropdownGO.AddComponent<Dropdown>();
        dropdown.targetGraphic = dropdownBG;
        dropdown.template = templateRT;
        dropdown.captionText = captionText;
        dropdown.itemText = itemLabel;

        var enumNames = Enum.GetNames(entry.SettingType);
        dropdown.options = new Il2CppSystem.Collections.Generic.List<Dropdown.OptionData>();
        foreach (var name in enumNames)
            dropdown.options.Add(new Dropdown.OptionData(I18n.T(name)));

        var currentVal = entry.BoxedValue.ToString();
        dropdown.value = Array.IndexOf(enumNames, currentVal);
        dropdown.RefreshShownValue();

        dropdown.onValueChanged.AddListener((UnityAction<int>)delegate(int idx)
        {
            entry.BoxedValue = Enum.Parse(entry.SettingType, enumNames[idx]);
        });
    }

    private static void CreateKeyCodeControl(GameObject parent, ConfigEntryBase entry)
    {
        var btnGO = CreateUIObject("KeyButton", parent);
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.flexibleWidth = 1;
        btnLE.preferredHeight = 30;
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.2f, 0.25f);

        var textGO = CreateUIObject("Text", btnGO);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8, 2);
        textRT.offsetMax = new Vector2(-8, -2);
        var text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 15;
        text.color = new Color(0.9f, 0.9f, 0.9f);
        text.alignment = TextAnchor.MiddleCenter;
        text.text = entry.BoxedValue.ToString();

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener((UnityAction)delegate
        {
            // Cancel any previous listening
            if (_listeningText != null)
                _listeningText.text = _listeningEntry?.BoxedValue.ToString() ?? "";

            _listeningEntry = entry;
            _listeningText = text;
            text.text = I18n.T("Press a key...");
            btnImg.color = new Color(0.3f, 0.4f, 0.6f);
        });
    }

    private static void ProcessKeyListen()
    {
        try
        {
            if (!Input.anyKeyDown) return;
        }
        catch
        {
            return;
        }

        // ESC cancels
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _listeningText.text = _listeningEntry.BoxedValue.ToString();
            _listeningEntry = null;
            _listeningText = null;
            return;
        }

        // Find which key was pressed
        foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
        {
            // Skip mouse buttons and None
            if (kc == KeyCode.None || (int)kc >= 323 && (int)kc <= 329)
                continue;

            try
            {
                if (!Input.GetKeyDown(kc)) continue;
            }
            catch
            {
                continue;
            }

            _listeningEntry.BoxedValue = kc;
            _listeningText.text = kc.ToString();

            // Reset the button background color
            var btnImg = _listeningText.transform.parent.GetComponent<Image>();
            if (btnImg != null)
                btnImg.color = new Color(0.2f, 0.2f, 0.25f);

            _listeningEntry = null;
            _listeningText = null;
            return;
        }
    }

    /// <summary>
    /// Per-frame hover check with debounce: only updates description after hovering
    /// on the same row for HoverDelay seconds, so fast mouse movement doesn't cause
    /// distracting rapid text changes. Clears when mouse leaves the viewport.
    /// </summary>
    private static void UpdateHoverDesc()
    {
        if (_descText == null) return;

        Vector2 mousePos = Input.mousePosition;

        // Mouse outside the scroll viewport → clear
        if (_viewportRT != null && !RectTransformUtility.RectangleContainsScreenPoint(_viewportRT, mousePos))
        {
            _descText.text = "";
            _hoveredRow = null;
            _hoverTimer = 0;
            return;
        }

        // Find which row the mouse is over
        RectTransform hitRow = null;
        string hitDesc = null;
        foreach (var (rt, desc) in _rowDescs)
        {
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos))
            {
                hitRow = rt;
                hitDesc = desc;
                break;
            }
        }

        if (hitRow == null) return; // gap between rows — keep current text

        // Row changed → restart timer
        if (hitRow != _hoveredRow)
        {
            _hoveredRow = hitRow;
            _hoverTimer = 0;
        }

        _hoverTimer += Time.unscaledDeltaTime;
        if (_hoverTimer >= HoverDelay)
            _descText.text = I18n.T(hitDesc);
    }

    /// <summary>
    /// Manual scroll wheel handling — IL2CPP event bubbling to ScrollRect is unreliable.
    /// </summary>
    private static void HandleScrollWheel()
    {
        if (_scrollRect == null) return;

        float scroll = Input.mouseScrollDelta.y;
        if (scroll == 0) return;

        if (_viewportRT != null &&
            !RectTransformUtility.RectangleContainsScreenPoint(_viewportRT, Input.mousePosition))
            return;

        // 40px per scroll notch, normalized by scrollable range
        float contentH = _scrollRect.content.rect.height;
        float viewportH = _scrollRect.viewport.rect.height;
        float range = contentH - viewportH;
        if (range <= 0) return;

        _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
            _scrollRect.verticalNormalizedPosition + scroll * 40f / range);
    }

    /// <summary>
    /// Auto-continue: on first arrival at DR_Title scene, call TitleManager.OnContinueGame().
    /// IsSceneInitialized is set before async init (StartGame/LoadSpriteAtlas) finishes,
    /// so we wait an additional 3 seconds after it becomes true to ensure the title menu
    /// is fully interactive before triggering continue.
    /// </summary>
    internal static void AutoContinueCheck()
    {
        if (_autoContinueDone || !_autoContinue.Value) return;

        try
        {
            if (SceneManager.GetActiveScene().name != "DR_Title") return;
        }
        catch { return; }

        try
        {
            var titleMgr = UnityEngine.Object.FindObjectOfType<DR.Title.TitleManager>();
            if (titleMgr == null || !titleMgr.IsSceneInitialized) return;

            // Accumulate wait time after IsSceneInitialized becomes true
            _autoContinueTimer += Time.unscaledDeltaTime;
            if (_autoContinueTimer < 3f) return;

            _autoContinueDone = true;
            Plugin.Log.LogInfo("AutoContinue: triggering OnContinueGame on title screen");
            titleMgr.OnContinueGame();
        }
        catch (Exception e)
        {
            _autoContinueDone = true;
            Plugin.Log.LogWarning($"AutoContinue failed: {e.Message}");
        }
    }

    private static void Hide()
    {
        _isVisible = false;
        if (_panelGO != null)
            _panelGO.SetActive(false);
        if (_overlayGO != null)
            _overlayGO.SetActive(false);
    }

    private static GameObject CreateUIObject(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent.transform, false);
        return go;
    }
}

/// <summary>
/// Il2CppInterop-registered MonoBehaviour that runs Update every frame in all scenes.
/// Handles ConfigUI hotkey detection globally.
/// </summary>
public class ConfigUIBehaviour : MonoBehaviour
{
    public ConfigUIBehaviour(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        ConfigUI.CheckToggle();
        ConfigUI.AutoContinueCheck();
        AuraHud.Update();
        QuickSceneSwitch.CheckToggle();
        AutoSeahorseRace.CheckHotkey();
        SuperDave.SuperDaveHotkeys.Check();
    }
}
