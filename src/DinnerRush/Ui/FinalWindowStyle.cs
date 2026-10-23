using CS.GUI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DinnerRush.Ui;

/// <summary>
/// Visual pieces borrowed from the game's "dish served" window (GUIKitchen.finalWindow) so the results popup
/// looks like part of the game: its panel and header backgrounds, its OK button sprite, and its star-rating bar.
/// Every piece may be missing; callers fall back to flat colours.
/// </summary>
internal sealed class FinalWindowStyle
{
    private static readonly AccessTools.FieldRef<GUIFinalWindow, Button> OkButtonRef =
        AccessTools.FieldRefAccess<GUIFinalWindow, Button>("okButton");
    private static readonly AccessTools.FieldRef<GUIFinalWindow, Image> OverallMaskRef =
        AccessTools.FieldRefAccess<GUIFinalWindow, Image>("overallMask");

    public Image Background { get; private set; }
    public Image HeaderBackground { get; private set; }
    public Image ButtonImage { get; private set; }
    public Image StarMask { get; private set; }

    internal static FinalWindowStyle Find()
    {
        var style = new FinalWindowStyle();
        GUIFinalWindow window = GUIKitchen.Me != null ? GUIKitchen.Me.finalWindow : null;
        if (window == null)
        {
            Plugin.Log.LogWarning("Dish-served window not found; results use plain styling.");
            return style;
        }
        GameUiTemplates.DumpOnce("Dish served window", window.transform);
        // By name: the window also has a huge, almost transparent "Mouse Blocker" image, so size alone misleads.
        style.Background = FindImage(window.transform, "Background");
        style.HeaderBackground = FindImage(window.transform, "Header/Background");
        Button ok = OkButtonRef(window);
        style.ButtonImage = ok != null ? ok.targetGraphic as Image : null;
        style.StarMask = OverallMaskRef(window);
        return style;
    }

    /// <summary>Applies the borrowed panel sprite and tint to <paramref name="target"/>, keeping its slicing.</summary>
    internal bool ApplyBackground(Image target) => CopySprite(Background, target, keepTint: true);

    internal bool ApplyHeader(Image target) => CopySprite(HeaderBackground, target, keepTint: true);

    internal bool ApplyButton(Image target) => CopySprite(ButtonImage, target, keepTint: false);

    /// <summary>
    /// Clones the window's overall star bar (the mask's parent: empty stars + fill mask) into <paramref name="slot"/>,
    /// filled to <paramref name="halfStars"/> the same way the game does (0.1 per half star). Returns false if unavailable.
    /// </summary>
    internal bool CreateStarBar(RectTransform slot, float halfStars)
    {
        if (StarMask == null || StarMask.transform.parent == null)
        {
            return false;
        }
        Transform source = StarMask.transform.parent;
        string maskPath = PathFrom(source, StarMask.transform);
        GameObject clone = Object.Instantiate(source.gameObject, slot, false);
        clone.SetActive(true);
        Transform mask = clone.transform.Find(maskPath);
        Image fill = mask != null ? mask.GetComponent<Image>() : null;
        if (fill == null)
        {
            Object.Destroy(clone);
            return false;
        }
        fill.fillAmount = Mathf.Clamp01(halfStars * 0.1f);
        foreach (Graphic graphic in clone.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }

        var rect = (RectTransform)clone.transform;
        Vector2 size = GameUiTemplates.SizeOf(rect, slot.rect.size);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        float scale = Mathf.Min(slot.rect.width / size.x, slot.rect.height / size.y);
        rect.localScale = new Vector3(scale, scale, 1f);
        return true;
    }

    /// <param name="keepTint">Copy the source's colour too: the game tints white sprites through Image.color.</param>
    private static bool CopySprite(Image source, Image target, bool keepTint)
    {
        if (source == null || source.sprite == null)
        {
            return false;
        }
        target.sprite = source.sprite;
        target.type = source.type;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.color = keepTint ? source.color : Color.white;
        return true;
    }

    private static Image FindImage(Transform root, string path)
    {
        Transform t = root.Find(path);
        Image image = t != null ? t.GetComponent<Image>() : null;
        if (image == null)
        {
            Plugin.Log.LogWarning($"Dish-served window part '{path}' not found.");
        }
        return image;
    }

    private static string PathFrom(Transform root, Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null && p != root; p = p.parent)
        {
            path = p.name + "/" + path;
        }
        return path;
    }
}
