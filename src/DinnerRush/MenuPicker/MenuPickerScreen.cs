using System;
using System.Collections.Generic;
using System.Linq;
using CS.GUI;
using CS.GUI.ScreenUI;
using DinnerRush.Core;
using DinnerRush.Ui;
using Recipes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DinnerRush.MenuPicker;

/// <summary>
/// Full-screen modal: a grid of drawn dishes, pick exactly N, then confirm. The run holds the game paused while it is open.
/// Cards are clones of the kitchen computer's own recipe cards when available; right-click opens the recipe details.
/// </summary>
internal sealed class MenuPickerScreen : MonoBehaviour
{
    private const int SortingOrder = 300;
    private const int Columns = 5;
    private static readonly Vector2 CellSize = new Vector2(300f, 330f);
    private static readonly Vector2 CardArea = new Vector2(290f, 236f);

    // Palette taken from the kitchen computer's order screen.
    internal static readonly Color DimColor = new Color(0f, 0f, 0f, 0.8f);
    internal static readonly Color WindowColor = new Color(0.12f, 0.16f, 0.22f, 0.98f);
    internal static readonly Color HeaderColor = new Color(0.17f, 0.23f, 0.31f, 1f);
    internal static readonly Color CardColor = new Color(0.20f, 0.26f, 0.34f, 1f);
    internal static readonly Color AccentColor = new Color(0.91f, 0.42f, 0.17f, 1f);
    internal static readonly Color ConfirmColor = new Color(0.27f, 0.52f, 0.24f, 1f);
    internal static readonly Color SecondaryColor = new Color(0.27f, 0.33f, 0.42f, 1f);
    internal static readonly Color MutedText = new Color(0.74f, 0.79f, 0.86f, 1f);

    private sealed class Card
    {
        public Recipe Recipe;
        public RectTransform Root;
        public Image Highlight;
        public GameObject Badge;
        public GameObject PickButton; // game card's "Order" tab, relabelled
        public bool Selected;
    }

    private static MenuPickerScreen current;

    private readonly List<Card> cards = new List<Card>();
    private List<float> poolWorkloads;
    private int menuSize;
    private bool useWorkload;
    private Action<List<Recipe>, MenuRating> onConfirm;
    private Action onQuit;
    private TextMeshProUGUI counterText;
    private TextMeshProUGUI difficultyText;
    private Button confirmButton;
    private RecipeDetailsOverlay details;

    internal static bool IsOpen => current != null;

    internal static MenuPickerScreen Open(Transform parent, List<Recipe> choices, List<Recipe> pool, int menuSize, bool useWorkload,
        Action<List<Recipe>, MenuRating> onConfirm, Action onQuit)
    {
        Close();
        Canvas canvas = UiKit.CreateOverlayCanvas("DinnerRushMenuPicker", parent, SortingOrder);
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        current = canvas.gameObject.AddComponent<MenuPickerScreen>();
        current.menuSize = menuSize;
        current.useWorkload = useWorkload;
        current.poolWorkloads = pool.Select(RecipePool.SafeWorkload).ToList();
        current.onConfirm = onConfirm;
        current.onQuit = onQuit;
        current.Build(choices);
        return current;
    }

    internal static void Close()
    {
        if (current == null)
        {
            return;
        }
        current.details?.Release();
        Destroy(current.gameObject);
        current = null;
        GUIGenericWindow.CheckCursor();
    }

    private void Build(List<Recipe> choices)
    {
        Transform root = transform;
        Image dim = UiKit.CreatePanel("Dim", root, DimColor);
        dim.raycastTarget = true; // blocks clicks to anything underneath
        UiKit.Stretch(dim.rectTransform);

        RectTransform window = UiKit.CreatePanel("Window", root, WindowColor).rectTransform;
        UiKit.Place(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1720f, 980f));

        RectTransform header = UiKit.CreatePanel("Header", window, HeaderColor).rectTransform;
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 130f);
        header.anchoredPosition = Vector2.zero;

        Image accent = UiKit.CreatePanel("Accent", header, AccentColor);
        UiKit.Place(accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(12f, 130f));

        TextMeshProUGUI title = UiKit.CreateText("Title", header, 52f, TextAlignmentOptions.Left, Color.white);
        title.fontStyle = FontStyles.Bold;
        title.text = "Dinner Rush";
        UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -14f), new Vector2(900f, 64f));

        TextMeshProUGUI subtitle = UiKit.CreateText("Subtitle", header, 24f, TextAlignmentOptions.Left, MutedText);
        subtitle.text = $"Choose {menuSize} dishes for tonight's menu. Every order will be one of them.  <color=#E86A2B>Right-click a dish to see its recipe.</color>";
        UiKit.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -80f), new Vector2(1600f, 36f));

        RectTransform grid = UiKit.CreateRect("Grid", window);
        UiKit.Place(grid, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1620f, 690f));
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = CellSize;
        layout.spacing = new Vector2(30f, 24f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Columns;
        layout.childAlignment = TextAnchor.UpperCenter;

        SandboxRecipeButton cardPrefab = GameUiTemplates.FindCardPrefab();
        if (cardPrefab == null)
        {
            Plugin.Log.LogWarning("Computer recipe card prefab not found; using plain cards.");
        }
        foreach (Recipe recipe in choices)
        {
            cards.Add(CreateCard(grid, recipe, cardPrefab));
        }

        counterText = UiKit.CreateText("Counter", window, 30f, TextAlignmentOptions.Left, Color.white);
        counterText.fontStyle = FontStyles.Bold;
        UiKit.Place(counterText.rectTransform, new Vector2(0f, 0f), new Vector2(60f, 42f), new Vector2(360f, 60f));

        difficultyText = UiKit.CreateText("Difficulty", window, 28f, TextAlignmentOptions.Left, MutedText);
        UiKit.Place(difficultyText.rectTransform, new Vector2(0f, 0f), new Vector2(420f, 42f), new Vector2(560f, 60f));

        Button quit = UiKit.CreateButton("Quit", window, "Quit to menu", SecondaryColor, () => onQuit?.Invoke(), new Vector2(280f, 72f));
        UiKit.Place((RectTransform)quit.transform, new Vector2(1f, 0f), new Vector2(-400f, 36f), new Vector2(280f, 72f));

        confirmButton = UiKit.CreateButton("Confirm", window, "Start prep", ConfirmColor, Confirm, new Vector2(300f, 72f));
        UiKit.Place((RectTransform)confirmButton.transform, new Vector2(1f, 0f), new Vector2(-60f, 36f), new Vector2(300f, 72f));

        details = new RecipeDetailsOverlay(transform, IsSelected, Toggle);

        Refresh();
        if (Game.Input.IsUsingGamepad && cards.Count > 0)
        {
            Selectable first = cards[0].Root.GetComponentInChildren<Selectable>();
            if (first != null)
            {
                EventSystem.current?.SetSelectedGameObject(first.gameObject);
            }
        }
    }

    private Card CreateCard(Transform parent, Recipe recipe, SandboxRecipeButton prefab)
    {
        var card = new Card { Recipe = recipe };
        // Transparent cell (still a raycast target) holding: selection frame, card background, card, texts.
        Image cell = UiKit.CreatePanel("Card_" + recipe.CoreId.Id, parent, Color.clear);
        cell.raycastTarget = true;
        card.Root = cell.rectTransform;

        card.Highlight = UiKit.CreatePanel("Highlight", cell.transform, AccentColor);
        UiKit.Stretch(card.Highlight.rectTransform);
        card.Highlight.rectTransform.offsetMin = new Vector2(-6f, -6f);
        card.Highlight.rectTransform.offsetMax = new Vector2(6f, 6f);

        Image background = UiKit.CreatePanel("Background", cell.transform, CardColor);
        UiKit.Stretch(background.rectTransform);

        bool usedGameCard = prefab != null && TryCreateGameCard(card, cell.transform, prefab);
        if (!usedGameCard)
        {
            CreatePlainCard(card, background);
        }

        TextMeshProUGUI name = UiKit.CreateText("Name", cell.transform, 22f, TextAlignmentOptions.Center, Color.white);
        name.fontStyle = FontStyles.Bold;
        name.enableWordWrapping = true;
        name.overflowMode = TextOverflowModes.Ellipsis;
        name.text = Localization.Get(recipe);
        UiKit.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(286f, 56f));

        TextMeshProUGUI info = UiKit.CreateText("Info", cell.transform, 20f, TextAlignmentOptions.Center, MutedText);
        info.text = $"<color={DifficultyColor(recipe.RecipeDifficulty)}>{recipe.RecipeDifficulty}</color>  ·  {Recipe.TimeToMinuteString(RecipePool.SafeEstimatedTime(recipe))}";
        UiKit.Place(info.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(286f, 28f));

        Image badge = UiKit.CreatePanel("Badge", cell.transform, AccentColor);
        UiKit.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(110f, 32f));
        TextMeshProUGUI badgeText = UiKit.CreateText("Text", badge.transform, 19f, TextAlignmentOptions.Center, Color.white);
        badgeText.fontStyle = FontStyles.Bold;
        badgeText.text = "PICKED";
        UiKit.Stretch(badgeText.rectTransform);
        card.Badge = badge.gameObject;
        return card;
    }

    /// <summary>Clones the computer's recipe card; its picture and "Order" tab both toggle the pick.</summary>
    private bool TryCreateGameCard(Card card, Transform cell, SandboxRecipeButton prefab)
    {
        try
        {
            SandboxRecipeButton clone = Instantiate(prefab, cell, false);
            clone.gameObject.name = "GameCard";
            clone.gameObject.SetActive(true);
            GameUiTemplates.DumpOnce("Computer recipe card clone", clone.transform);

            clone.SetData(card.Recipe, () => Toggle(card.Recipe));
            // The game's name panel slides in on hover; we show the name permanently below instead.
            RectTransform namePanel = GameUiTemplates.CardNamePanel(clone);
            if (namePanel != null)
            {
                namePanel.gameObject.SetActive(false);
            }
            Button order = GameUiTemplates.CardOrderButton(clone);
            if (order != null)
            {
                order.onClick = new Button.ButtonClickedEvent(); // drop the inspector-wired StartRecipe
                order.onClick.AddListener(() => Toggle(card.Recipe));
                card.PickButton = order.gameObject;
            }
            GameUiTemplates.DisableExternalButtons(clone.gameObject);

            var rect = (RectTransform)clone.transform;
            Vector2 size = GameUiTemplates.SizeOf(rect, new Vector2(335f, 290f));
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, -4f);
            float scale = Mathf.Min(CardArea.x / size.x, CardArea.y / size.y);
            rect.localScale = new Vector3(scale, scale, 1f);
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not clone the computer's recipe card, using a plain card: {e.Message}");
            return false;
        }
    }

    private void CreatePlainCard(Card card, Image cell)
    {
        var button = cell.gameObject.AddComponent<Button>();
        button.targetGraphic = cell;
        button.onClick.AddListener(() => Toggle(card.Recipe));

        var render = new GameObject("Render", typeof(RectTransform)).AddComponent<Image>();
        render.transform.SetParent(cell.transform, false);
        render.sprite = card.Recipe.image;
        render.preserveAspect = true;
        render.raycastTarget = false;
        UiKit.Place(render.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(276f, 210f));
    }

    private bool IsSelected(Recipe recipe) => cards.Any(c => c.Recipe == recipe && c.Selected);

    private void Toggle(Recipe recipe)
    {
        Card card = cards.FirstOrDefault(c => c.Recipe == recipe);
        if (card == null || (!card.Selected && SelectedCount >= menuSize))
        {
            return; // full: deselect something first
        }
        card.Selected = !card.Selected;
        Refresh();
    }

    private int SelectedCount => cards.Count(c => c.Selected);

    private void Refresh()
    {
        foreach (Card card in cards)
        {
            card.Highlight.enabled = card.Selected;
            card.Badge.SetActive(card.Selected);
            if (card.PickButton != null)
            {
                GameUiTemplates.SetRawText(card.PickButton, card.Selected ? "Picked" : "Pick");
            }
        }
        int count = SelectedCount;
        counterText.text = $"{count} / {menuSize} selected";
        counterText.color = count == menuSize ? AccentColor : Color.white;
        MenuRating rating = count == 0 ? null : Rate();
        difficultyText.text = rating == null
            ? "Menu difficulty: -"
            : $"Menu difficulty: {rating.Display}   <color=#E86A2B>Score x{rating.Multiplier(DinnerRushConfig.MultiplierPerDifficultyPoint.Value):0.##}</color>";
        confirmButton.interactable = count == menuSize;
        details?.RefreshPickState();
    }

    private MenuRating Rate()
    {
        List<Recipe> picked = cards.Where(c => c.Selected).Select(c => c.Recipe).ToList();
        return MenuRating.Compute(
            picked.Select(r => (int)r.RecipeDifficulty).ToList(),
            picked.Select(RecipePool.SafeWorkload).ToList(),
            poolWorkloads,
            useWorkload);
    }

    private void Confirm()
    {
        if (SelectedCount != menuSize)
        {
            return;
        }
        List<Recipe> picked = cards.Where(c => c.Selected).Select(c => c.Recipe).ToList();
        MenuRating rating = Rate();
        Action<List<Recipe>, MenuRating> callback = onConfirm;
        Close();
        callback?.Invoke(picked, rating);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (details.IsOpen)
        {
            if ((mouse != null && mouse.rightButton.wasPressedThisFrame) || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
            {
                details.Hide();
            }
            return;
        }
        // Buttons inside the cards swallow pointer events, so find the card under the cursor with a UI raycast.
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            Card card = CardUnderPointer(mouse.position.ReadValue());
            if (card != null)
            {
                details.Show(card.Recipe);
            }
        }
    }

    private Card CardUnderPointer(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return null;
        }
        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, results);
        foreach (RaycastResult hit in results)
        {
            Card card = cards.FirstOrDefault(c => hit.gameObject.transform.IsChildOf(c.Root));
            if (card != null)
            {
                return card;
            }
        }
        return null;
    }

    private void LateUpdate()
    {
        // Keep the cursor free while the picker is up, whatever the kitchen input does underneath.
        Game.KitchenServices.KitchenInput.CursorLocked = false;
        Game.KitchenServices.KitchenInput.CursorVisible = !Game.Input.IsUsingGamepad;
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }

    private static string DifficultyColor(RecipeDifficulty difficulty) => difficulty switch
    {
        RecipeDifficulty.Easy => "#7CD67C",
        RecipeDifficulty.Medium => "#F2C14E",
        _ => "#F0645A",
    };
}
