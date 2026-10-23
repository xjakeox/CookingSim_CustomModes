using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DinnerRush.Ui;

/// <summary>Small helpers for building runtime uGUI/TMP elements that match the game's font.</summary>
internal static class UiKit
{
    private static TMP_FontAsset cachedFont;

    /// <summary>
    /// The font most used by TMP texts currently in the scene (i.e. the game's HUD font),
    /// falling back to TMP's default. Logged once so we can pin a specific asset later if needed.
    /// </summary>
    internal static TMP_FontAsset GameFont
    {
        get
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }
            cachedFont = Object.FindObjectsOfType<TMP_Text>()
                .Where(t => t.font != null)
                .GroupBy(t => t.font)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? TMP_Settings.defaultFontAsset;

            string available = string.Join(", ", Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Select(f => f.name).Distinct());
            Plugin.Log.LogInfo($"HUD font: {(cachedFont != null ? cachedFont.name : "none")}. Loaded TMP fonts: {available}");
            return cachedFont;
        }
    }

    internal static Canvas CreateOverlayCanvas(string name, Transform parent, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    internal static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    internal static Image CreatePanel(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    internal static TextMeshProUGUI CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = GameFont;
        if (font != null)
        {
            text.font = font;
        }
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    internal static Button CreateButton(string name, Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick, Vector2 size, float fontSize = 28f)
    {
        Image background = CreatePanel(name, parent, color);
        background.raycastTarget = true;
        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        // Tints multiply the image colour, so "normal" is slightly dimmed to leave room for a brighter hover.
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        button.colors = colors;
        button.onClick.AddListener(onClick);
        background.rectTransform.sizeDelta = size;
        var layout = background.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;

        TextMeshProUGUI text = CreateText("Label", background.transform, fontSize, TextAlignmentOptions.Center, Color.white);
        text.fontStyle = FontStyles.Bold;
        text.text = label;
        Stretch(text.rectTransform);
        return button;
    }

    internal static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    internal static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// <summary>mm:ss, rounding up so a countdown shows 00:01 until it truly reaches zero.</summary>
    internal static string FormatCountdown(float seconds) => FormatClock(Mathf.Ceil(Mathf.Max(0f, seconds)));

    internal static string FormatElapsed(float seconds) => FormatClock(Mathf.Floor(Mathf.Max(0f, seconds)));

    private static string FormatClock(float wholeSeconds)
    {
        int total = (int)wholeSeconds;
        return $"{total / 60:00}:{total % 60:00}";
    }
}
