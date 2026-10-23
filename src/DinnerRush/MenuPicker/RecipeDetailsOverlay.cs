using System;
using CS.GUI.TV;
using DinnerRush.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DinnerRush.MenuPicker;

/// <summary>
/// Recipe details on top of the menu picker: a clone of the kitchen computer's recipe view (components and steps),
/// with Back and Pick/Unpick buttons.
/// </summary>
internal sealed class RecipeDetailsOverlay
{
    private static readonly Vector2 ViewArea = new Vector2(1240f, 760f);
    // Largest size the view's content is scaled to (keeps the approved ~0.58x look), centred within ViewArea.
    private static readonly Vector2 FitArea = new Vector2(1000f, 620f);

    // The cloned view is kept for the whole session and only reparented: TVUIRecipeInfo.SetData starts a coroutine
    // (on the game's runner) that touches the view a second of game time later, after the picker is gone.
    private static TVUIRecipeInfo cachedView;
    private static GameObject cachedHolder;

    private readonly GameObject root;
    private readonly RectTransform viewSlot;
    private readonly TextMeshProUGUI fallbackText;
    private readonly Button pickButton;
    private readonly TextMeshProUGUI pickLabel;
    private readonly Func<Recipe, bool> isSelected;
    private readonly Action<Recipe> toggle;
    private Recipe shown;

    internal RecipeDetailsOverlay(Transform canvas, Func<Recipe, bool> isSelected, Action<Recipe> toggle)
    {
        this.isSelected = isSelected;
        this.toggle = toggle;

        root = UiKit.CreateRect("RecipeDetails", canvas).gameObject;
        UiKit.Stretch((RectTransform)root.transform);
        Image dim = UiKit.CreatePanel("Dim", root.transform, MenuPickerScreen.DimColor);
        dim.raycastTarget = true;
        UiKit.Stretch(dim.rectTransform);

        RectTransform window = UiKit.CreatePanel("Window", root.transform, MenuPickerScreen.WindowColor).rectTransform;
        UiKit.Place(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320f, 960f));

        viewSlot = UiKit.CreateRect("View", window);
        UiKit.Place(viewSlot, new Vector2(0.5f, 1f), new Vector2(0f, -30f), ViewArea);

        fallbackText = UiKit.CreateText("Fallback", window, 28f, TextAlignmentOptions.Center, MenuPickerScreen.MutedText);
        fallbackText.enableWordWrapping = true;
        UiKit.Place(fallbackText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1000f, 300f));

        TextMeshProUGUI hint = UiKit.CreateText("Hint", window, 22f, TextAlignmentOptions.Left, MenuPickerScreen.MutedText);
        hint.text = "Right-click or Esc to go back";
        UiKit.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(50f, 50f), new Vector2(500f, 40f));

        Button back = UiKit.CreateButton("Back", window, "Back", MenuPickerScreen.SecondaryColor, Hide, new Vector2(240f, 72f));
        UiKit.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(-340f, 36f), new Vector2(240f, 72f));

        pickButton = UiKit.CreateButton("Pick", window, "Pick", MenuPickerScreen.ConfirmColor, () =>
        {
            if (shown != null)
            {
                toggle(shown);
            }
        }, new Vector2(260f, 72f));
        UiKit.Place((RectTransform)pickButton.transform, new Vector2(1f, 0f), new Vector2(-60f, 36f), new Vector2(260f, 72f));
        pickLabel = pickButton.GetComponentInChildren<TextMeshProUGUI>();

        root.SetActive(false);
    }

    internal bool IsOpen => root.activeSelf;

    internal void Show(Recipe recipe)
    {
        shown = recipe;
        root.SetActive(true);
        TVUIRecipeInfo view = AcquireView();
        if (view != null)
        {
            try
            {
                view.SetData(recipe);
                GameUiTemplates.HideRecipeInfoActions(view);
                FitToSlot(view);
                fallbackText.text = string.Empty;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Recipe details failed for {Localization.Get(recipe)}: {e}");
                view.gameObject.SetActive(false);
                fallbackText.text = "Recipe details could not be shown.";
            }
        }
        else
        {
            fallbackText.text = $"{Localization.Get(recipe)}\n\nRecipe details are unavailable: the kitchen computer's recipe view was not found.";
        }
        RefreshPickState();
    }

    internal void Hide()
    {
        root.SetActive(false);
        shown = null;
    }

    internal void RefreshPickState()
    {
        if (shown == null)
        {
            return;
        }
        pickLabel.text = isSelected(shown) ? "Unpick" : "Pick";
    }

    /// <summary>Parks the cached view outside the picker before the picker is destroyed.</summary>
    internal void Release()
    {
        if (cachedView != null && cachedView.transform.IsChildOf(root.transform))
        {
            cachedView.transform.SetParent(Holder(), false);
        }
    }

    private TVUIRecipeInfo AcquireView()
    {
        if (cachedView == null)
        {
            TVUIRecipeInfo template = GameUiTemplates.FindRecipeInfoTemplate();
            if (template == null)
            {
                Plugin.Log.LogWarning("Computer recipe details view not found.");
                return null;
            }
            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, Holder().transform, false);
            clone.name = "DinnerRushRecipeView";
            GameUiTemplates.DisableExternalButtons(clone);
            GameUiTemplates.DumpOnce("Computer recipe details clone", clone.transform);
            cachedView = clone.GetComponent<TVUIRecipeInfo>();
        }

        var rect = (RectTransform)cachedView.transform;
        rect.SetParent(viewSlot, false);
        cachedView.gameObject.SetActive(true);
        Vector2 size = GameUiTemplates.SizeOf(rect, ViewArea);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return cachedView;
    }

    /// <summary>
    /// Scales and centres the view on its visible content. The view's own rect (sized for the computer screen)
    /// is smaller than its sidebar + main panel, which overflow it to the top-left, so centre on their bounds instead.
    /// </summary>
    private void FitToSlot(TVUIRecipeInfo view)
    {
        var rect = (RectTransform)view.transform;
        // Only the visible top-level panels: deeper descendants include scroll content that runs past its viewport.
        bool any = false;
        var bounds = new Bounds();
        var corners = new Vector3[4];
        foreach (Transform child in rect)
        {
            if (!child.gameObject.activeSelf || !(child is RectTransform childRect))
            {
                continue;
            }
            childRect.GetLocalCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 point = rect.InverseTransformPoint(childRect.TransformPoint(corner));
                if (!any)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    any = true;
                }
                bounds.Encapsulate(point);
            }
        }
        Vector2 contentSize = bounds.size;
        if (!any || contentSize.x < 1f || contentSize.y < 1f)
        {
            contentSize = rect.sizeDelta;
            bounds.center = Vector3.zero;
        }
        float scale = Mathf.Min(FitArea.x / contentSize.x, FitArea.y / contentSize.y);
        rect.localScale = new Vector3(scale, scale, 1f);
        rect.anchoredPosition = -(Vector2)bounds.center * scale;
    }

    private static Transform Holder()
    {
        if (cachedHolder == null)
        {
            cachedHolder = new GameObject("DinnerRushRecipeViewHolder");
            UnityEngine.Object.DontDestroyOnLoad(cachedHolder);
            cachedHolder.SetActive(false);
        }
        return cachedHolder.transform;
    }
}
