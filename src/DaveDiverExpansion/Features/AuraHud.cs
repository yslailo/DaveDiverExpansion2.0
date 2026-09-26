using BepInEx.Configuration;
using DaveDiverExpansion.Features.SuperDave;
using DaveDiverExpansion.Helpers;
using UnityEngine;
using UnityEngine.UI;

namespace DaveDiverExpansion.Features;

public enum HudCorner { TopRight, TopLeft, BottomRight, BottomLeft }

/// <summary>
/// Small always-on status HUD (screen corner) showing the toxic aura state/mode and a few
/// common toggles (infinite oxygen / invincible / infinite bullets).
///
/// Read-only: it never changes game state. It flashes the aura line whenever the aura is
/// toggled at runtime, so the hotkey gives visible feedback without opening the F1 panel.
/// </summary>
public static class AuraHud
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<HudCorner> Corner;
    public static ConfigEntry<bool> ShowAura;
    public static ConfigEntry<bool> ShowCommonBuffs;

    private static GameObject _canvasGO;
    private static GameObject _panelGO;
    private static Text _auraText;
    private static Text _oxygenText;
    private static Text _invincibleText;
    private static Text _bulletsText;

    private static bool _built;
    private static float _flashTimer;
    private const float FlashTime = 1.2f;

    // Last observed aura state (used to detect a toggle and flash)
    private static bool _hasLast;
    private static bool _lastAuraOn;
    private static bool _lastSleep;

    // Cached strings so we only touch Text.text when the value actually changes
    private static string _lastAuraStr;
    private static string _lastOxyStr;
    private static string _lastInvStr;
    private static string _lastBulStr;

    private static HudCorner _lastCorner;
    private static bool _cornerApplied;

    private static readonly Color AuraColor = new(0.95f, 0.95f, 0.95f);
    private static readonly Color BuffColor = new(0.78f, 0.78f, 0.84f);
    private static readonly Color FlashColor = new(1f, 0.85f, 0.2f);

    public static void Init(ConfigFile config)
    {
        Enabled = config.Bind(
            "AuraHud", "HUD - Enabled", true,
            "Show the on-screen status HUD (aura state + common toggles).");
        Corner = config.Bind(
            "AuraHud", "HUD - Corner", HudCorner.BottomLeft,
            "Screen corner for the status HUD.");
        ShowAura = config.Bind(
            "AuraHud", "HUD - Show Aura", true,
            "Show the toxic aura state and mode in the HUD.");
        ShowCommonBuffs = config.Bind(
            "AuraHud", "HUD - Show Common Buffs", true,
            "Show infinite oxygen / invincible / infinite bullets in the HUD.");
    }

    /// <summary>Called every frame from ConfigUIBehaviour.Update.</summary>
    internal static void Update()
    {
        try
        {
            if (Enabled == null) return;

            if (!Enabled.Value)
            {
                if (_panelGO != null && _panelGO.activeSelf)
                    _panelGO.SetActive(false);
                return;
            }

            if (!_built)
                BuildUI();

            if (!_panelGO.activeSelf)
                _panelGO.SetActive(true);

            ApplyCorner();
            Refresh();
        }
        catch { }
    }

    private static void Refresh()
    {
        ToxicAura.EnsureInit();

        bool auraOn = SuperDaveCore.Enabled.Value && ToxicAura.Enabled.Value && ToxicAura.ActiveByHotkey;
        bool sleep = ToxicAura.SleepByHotkey;

        // Detect a runtime toggle (hotkey) and flash the aura line
        if (_hasLast && (auraOn != _lastAuraOn || sleep != _lastSleep))
            _flashTimer = FlashTime;
        _lastAuraOn = auraOn;
        _lastSleep = sleep;
        _hasLast = true;

        // Aura row
        if (ShowAura.Value)
        {
            if (!_auraText.gameObject.activeSelf) _auraText.gameObject.SetActive(true);
            string s = I18n.T("Aura") + ": " + I18n.T(auraOn ? "ON" : "OFF")
                       + "  [" + I18n.T(sleep ? "Sleep" : "Kill") + "]";
            if (s != _lastAuraStr) { _auraText.text = s; _lastAuraStr = s; }
        }
        else if (_auraText.gameObject.activeSelf)
        {
            _auraText.gameObject.SetActive(false);
        }

        // Common buffs
        if (ShowCommonBuffs.Value)
        {
            if (!_oxygenText.gameObject.activeSelf) _oxygenText.gameObject.SetActive(true);
            if (!_invincibleText.gameObject.activeSelf) _invincibleText.gameObject.SetActive(true);
            if (!_bulletsText.gameObject.activeSelf) _bulletsText.gameObject.SetActive(true);

            string o = I18n.T("Diving - Infinite Oxygen") + ": " + I18n.T(DiveBuffs.InfiniteOxygen.Value ? "ON" : "OFF");
            string i = I18n.T("Diving - Invincible") + ": " + I18n.T(DiveBuffs.Invincible.Value ? "ON" : "OFF");
            string b = I18n.T("Diving - Infinite Bullets") + ": " + I18n.T(DiveBuffs.InfiniteBullets.Value ? "ON" : "OFF");

            if (o != _lastOxyStr) { _oxygenText.text = o; _lastOxyStr = o; }
            if (i != _lastInvStr) { _invincibleText.text = i; _lastInvStr = i; }
            if (b != _lastBulStr) { _bulletsText.text = b; _lastBulStr = b; }
        }
        else
        {
            if (_oxygenText.gameObject.activeSelf) _oxygenText.gameObject.SetActive(false);
            if (_invincibleText.gameObject.activeSelf) _invincibleText.gameObject.SetActive(false);
            if (_bulletsText.gameObject.activeSelf) _bulletsText.gameObject.SetActive(false);
        }

        // Flash decay
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.unscaledDeltaTime;
            if (_auraText.color != FlashColor) _auraText.color = FlashColor;
        }
        else if (_auraText.color != AuraColor)
        {
            _auraText.color = AuraColor;
        }
    }

    private static void ApplyCorner()
    {
        if (_panelGO == null) return;
        if (_cornerApplied && _lastCorner == Corner.Value) return;

        var rt = _panelGO.GetComponent<RectTransform>();
        const float pad = 16f;
        switch (Corner.Value)
        {
            case HudCorner.TopLeft:
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(pad, -pad);
                break;
            case HudCorner.BottomRight:
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-pad, pad);
                break;
            case HudCorner.BottomLeft:
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(pad, pad);
                break;
            default: // TopRight
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-pad, -pad);
                break;
        }

        _lastCorner = Corner.Value;
        _cornerApplied = true;
    }

    private static void BuildUI()
    {
        _canvasGO = new GameObject("DDE_HudCanvas");
        UnityEngine.Object.DontDestroyOnLoad(_canvasGO);
        var canvas = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000; // below the F1 panel (10000)
        var scaler = _canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _panelGO = CreateUIObject("Panel", _canvasGO);
        var rt = _panelGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(240f, 0f);

        var img = _panelGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.45f);
        img.raycastTarget = false;

        var layout = _panelGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 6, 6);
        layout.spacing = 2;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childAlignment = TextAnchor.UpperLeft;

        var fitter = _panelGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _auraText = CreateText(_panelGO, 16, FontStyle.Bold, AuraColor);
        _oxygenText = CreateText(_panelGO, 14, FontStyle.Normal, BuffColor);
        _invincibleText = CreateText(_panelGO, 14, FontStyle.Normal, BuffColor);
        _bulletsText = CreateText(_panelGO, 14, FontStyle.Normal, BuffColor);

        _built = true;
    }

    private static Text CreateText(GameObject parent, int size, FontStyle style, Color color)
    {
        var go = CreateUIObject("Text", parent);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = size + 6;
        le.flexibleWidth = 1;
        return t;
    }

    private static GameObject CreateUIObject(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent.transform, false);
        return go;
    }
}
