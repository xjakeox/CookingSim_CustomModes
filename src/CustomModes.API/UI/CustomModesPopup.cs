using System.Collections.Generic;
using CS.GUI;
using CS.GUI.GameModes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CustomModes.API.UI;

/// <summary>
/// Runtime-built uGUI popup listing every registered mode. Rebuilt on each open so late registrations appear.
/// </summary>
internal static class CustomModesPopup
{
    private const int SortingOrder = 5000;
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.75f);
    private static readonly Color WindowColor = new Color(0.11f, 0.12f, 0.14f, 0.98f);
    private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color MutedText = new Color(0.78f, 0.80f, 0.84f, 1f);

    private static GameObject root;
    private static GUISandboxGameMode sandbox;

    internal static bool IsOpen => root != null;

    internal static void Open(GUISandboxGameMode owner)
    {
        Close();
        sandbox = owner;
        var context = new KitchenContext(CustomModesButton.GetSelectedKitchenId(owner));
        TextButton template = CustomModesButton.GetPlayButton(owner);
        Font font = template != null && template.Text != null ? template.Text.font : null;
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        root = new GameObject("CustomModesPopup", typeof(RectTransform));
        Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        // Full-screen dimmer also blocks clicks on the menu underneath.
        Stretch(CreateImage("Dim", root.transform, DimColor).rectTransform);

        Image window = CreateImage("Window", root.transform, WindowColor);
        RectTransform windowRect = window.rectTransform;
        windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1100f, 760f);
        var windowLayout = window.gameObject.AddComponent<VerticalLayoutGroup>();
        windowLayout.padding = new RectOffset(40, 40, 32, 32);
        windowLayout.spacing = 20f;
        windowLayout.childControlWidth = windowLayout.childControlHeight = true;
        windowLayout.childForceExpandWidth = true;
        windowLayout.childForceExpandHeight = false;

        CreateText("Title", window.transform, CustomModesButton.Label, font, 44, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, 56f);

        RectTransform list = CreateScrollList(window.transform);
        var selectables = new List<Selectable>();
        IReadOnlyList<CustomModeDefinition> modes = CustomModesApi.Modes;
        if (modes.Count == 0)
        {
            CreateText("Empty", list, "No custom game modes are installed.", font, 28, FontStyle.Italic, TextAnchor.MiddleCenter, MutedText, 120f);
        }
        foreach (CustomModeDefinition mode in modes)
        {
            Selectable play = CreateModeRow(list, mode, context, template, font);
            if (play.interactable)
            {
                selectables.Add(play);
            }
        }

        var footer = new GameObject("Footer", typeof(RectTransform));
        footer.transform.SetParent(window.transform, false);
        var footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
        footerLayout.childAlignment = TextAnchor.MiddleRight;
        footerLayout.childControlWidth = footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = footerLayout.childForceExpandHeight = false;
        Selectable close = CreateButton(footer.transform, "Close", template, font, Close, 260f);
        selectables.Add(close);
        SetVerticalNavigation(selectables);

        EventSystem.current?.SetSelectedGameObject(null);
        EventSystem.current?.SetSelectedGameObject(selectables[0].gameObject);
    }

    internal static void Close()
    {
        if (root == null)
        {
            return;
        }
        Object.Destroy(root);
        root = null;
        if (sandbox != null)
        {
            CustomModesButton.Focus(sandbox);
        }
        sandbox = null;
    }

    /// <summary>Runs instead of GUIGameModesWindow.Update while the popup is open.</summary>
    internal static void HandleInput()
    {
        if (Game.Input.UICancel.GetButtonDown())
        {
            Close();
        }
    }

    private static void Select(CustomModeDefinition mode, KitchenContext context)
    {
        Log.Info($"Mode '{mode.Id}' selected for kitchen '{context.KitchenId}'.");
        sandbox = null; // nothing to refocus; we are leaving the menu
        Close();
        // Same sequence as GUIGameModesWindow.BaseSandboxGame, but through the API so the mode is marked active first.
        GUIMenu.Me.GameModesUI.Hide();
        EventSystem.current?.SetSelectedGameObject(null);
        CustomModesApi.StartMode(mode, context);
    }

    private static Selectable CreateModeRow(Transform parent, CustomModeDefinition mode, KitchenContext context, TextButton template, Font font)
    {
        bool available = mode.CheckAvailable(context);

        Image row = CreateImage("Mode_" + mode.Id, parent, RowColor);
        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(20, 20, 16, 16);
        rowLayout.spacing = 20f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
        row.gameObject.AddComponent<LayoutElement>().minHeight = 128f;

        if (mode.Icon != null)
        {
            var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            icon.transform.SetParent(row.transform, false);
            icon.sprite = mode.Icon;
            icon.preserveAspect = true;
            var iconSize = icon.gameObject.AddComponent<LayoutElement>();
            iconSize.preferredWidth = iconSize.preferredHeight = 96f;
        }

        var textColumn = new GameObject("Text", typeof(RectTransform));
        textColumn.transform.SetParent(row.transform, false);
        var columnLayout = textColumn.AddComponent<VerticalLayoutGroup>();
        columnLayout.spacing = 6f;
        columnLayout.childControlWidth = columnLayout.childControlHeight = true;
        columnLayout.childForceExpandWidth = true;
        columnLayout.childForceExpandHeight = false;
        textColumn.AddComponent<LayoutElement>().flexibleWidth = 1f;

        CreateText("Name", textColumn.transform, mode.Name, font, 32, FontStyle.Bold, TextAnchor.UpperLeft, Color.white, -1f);
        string description = mode.Description ?? string.Empty;
        if (!available)
        {
            description = (description + "\nNot available for this kitchen.").Trim();
        }
        CreateText("Description", textColumn.transform, description, font, 24, FontStyle.Normal, TextAnchor.UpperLeft, MutedText, -1f);

        Selectable play = CreateButton(row.transform, "Play", template, font, () => Select(mode, context), 220f);
        play.interactable = available;
        return play;
    }

    private static RectTransform CreateScrollList(Transform parent)
    {
        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(parent, false);
        scrollGo.AddComponent<LayoutElement>().flexibleHeight = 1f;
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollGo.transform, false);
        Stretch((RectTransform)viewport.transform);
        viewport.AddComponent<RectMask2D>();

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = contentRect;
        return contentRect;
    }

    /// <summary>Clones the game's Play button for a native look; falls back to a plain uGUI button.</summary>
    private static Selectable CreateButton(Transform parent, string label, TextButton template, Font font, UnityEngine.Events.UnityAction onClick, float width)
    {
        Button button = null;
        if (template != null)
        {
            GameObject clone = Object.Instantiate(template.gameObject, parent, false);
            clone.name = "Button_" + label;
            clone.SetActive(true);
            foreach (GUIStaticInputTutorial glyph in clone.GetComponentsInChildren<GUIStaticInputTutorial>(true))
            {
                glyph.gameObject.SetActive(false);
            }
            var textButton = clone.GetComponent<TextButton>();
            textButton.onClick = new Button.ButtonClickedEvent();
            textButton.interactable = true;
            CustomModesButton.SetLabel(textButton, label);
            button = textButton;
        }
        else
        {
            Image bg = CreateImage("Button_" + label, parent, new Color(0.85f, 0.45f, 0.15f, 1f));
            button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            Stretch(CreateText("Label", bg.transform, label, font, 28, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, -1f).rectTransform);
        }

        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        var size = button.gameObject.GetComponent<LayoutElement>();
        if (size == null)
        {
            size = button.gameObject.AddComponent<LayoutElement>();
        }
        size.ignoreLayout = false;
        size.minWidth = size.preferredWidth = width;
        size.minHeight = size.preferredHeight = 72f;
        size.flexibleWidth = size.flexibleHeight = 0f;
        button.onClick.AddListener(onClick);
        return button;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value, Font font, int size, FontStyle style, TextAnchor anchor, Color color, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        if (height > 0f)
        {
            go.AddComponent<LayoutElement>().preferredHeight = height;
        }
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetVerticalNavigation(List<Selectable> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Navigation nav = items[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = i > 0 ? items[i - 1] : null;
            nav.selectOnDown = i < items.Count - 1 ? items[i + 1] : null;
            nav.selectOnLeft = nav.selectOnRight = null;
            items[i].navigation = nav;
        }
    }
}
