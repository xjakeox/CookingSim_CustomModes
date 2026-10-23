using System;
using System.Collections.Generic;
using CS.GUI;
using DinnerRush.Core;
using DinnerRush.MenuPicker;
using DinnerRush.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DinnerRush;

/// <summary>Everything the results popup shows about a finished run.</summary>
internal sealed class RunResults
{
    public GameOverReason Reason;
    public float ServiceSeconds;
    public int OrdersReceived;
    public ScoreTracker Score;
    public List<Recipe> Menu;
    public MenuRating Rating;
    public float Multiplier;
    public float FinalScore;

    /// <summary>Null when the run was not saved (debug keys used).</summary>
    public HighScoreResult HighScore;
}

/// <summary>
/// End-of-run popup in the style of the game's "dish served" window: stats on the left, the menu on the right,
/// best scores along the bottom, and Play again / Main menu buttons. The run holds the game paused while it is open.
/// </summary>
internal sealed class ResultsScreen : MonoBehaviour
{
    private const int SortingOrder = 310;
    private static readonly Vector2 WindowSize = new Vector2(1240f, 820f);
    private static readonly Color ValueColor = Color.white;
    private static readonly Color BadColor = new Color(0.95f, 0.35f, 0.28f, 1f);
    private static readonly Color StrikeColor = new Color(0.95f, 0.22f, 0.18f, 1f);
    private static readonly Color StrikeEmptyColor = new Color(0.55f, 0.55f, 0.55f, 0.9f);

    private static ResultsScreen current;

    internal static bool IsOpen => current != null;

    internal static void Open(Transform parent, RunResults results, Action onPlayAgain, Action onMainMenu)
    {
        Close();
        Canvas canvas = UiKit.CreateOverlayCanvas("DinnerRushResults", parent, SortingOrder);
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        current = canvas.gameObject.AddComponent<ResultsScreen>();
        current.Build(results, onPlayAgain, onMainMenu);
    }

    internal static void Close()
    {
        if (current == null)
        {
            return;
        }
        Destroy(current.gameObject);
        current = null;
        GUIGenericWindow.CheckCursor();
    }

    private void Build(RunResults results, Action onPlayAgain, Action onMainMenu)
    {
        FinalWindowStyle style = FinalWindowStyle.Find();
        Transform root = transform;

        Image dim = UiKit.CreatePanel("Dim", root, MenuPickerScreen.DimColor);
        dim.raycastTarget = true;
        UiKit.Stretch(dim.rectTransform);

        Image window = UiKit.CreatePanel("Window", root, MenuPickerScreen.WindowColor);
        window.raycastTarget = true;
        style.ApplyBackground(window);
        RectTransform w = window.rectTransform;
        UiKit.Place(w, new Vector2(0.5f, 0.5f), Vector2.zero, WindowSize);

        // Title strip, like the dish-served window's header.
        Image header = UiKit.CreatePanel("Header", w, MenuPickerScreen.HeaderColor);
        style.ApplyHeader(header);
        RectTransform h = header.rectTransform;
        h.anchorMin = new Vector2(0f, 1f);
        h.anchorMax = new Vector2(1f, 1f);
        h.pivot = new Vector2(0.5f, 1f);
        h.sizeDelta = new Vector2(0f, 160f);
        h.anchoredPosition = Vector2.zero;

        // Header
        TextMeshProUGUI title = UiKit.CreateText("Title", w, 54f, TextAlignmentOptions.Center, Color.white);
        title.fontStyle = FontStyles.Bold;
        title.text = "Dinner Rush Results";
        UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(1100f, 66f));
        TextMeshProUGUI reason = UiKit.CreateText("Reason", w, 30f, TextAlignmentOptions.Center, BadColor);
        reason.text = results.Reason?.ToString() ?? "Run ended";
        // Black outline so the red stays readable on the yellow header banner.
        reason.fontStyle = FontStyles.Bold;
        reason.outlineWidth = 0.25f;
        reason.outlineColor = Color.black;
        // Kept inside the banner's visible area; long dish names shrink to fit rather than spill out.
        reason.enableAutoSizing = true;
        reason.fontSizeMin = 18f;
        reason.fontSizeMax = 30f;
        UiKit.Place(reason.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(1060f, 38f));

        BuildStats(w, results, style);
        BuildMenu(w, results);
        BuildBest(w, results);

        // Buttons
        Button playAgain = UiKit.CreateButton("PlayAgain", w, "Play again", MenuPickerScreen.ConfirmColor, () => onPlayAgain?.Invoke(), new Vector2(300f, 76f), 30f);
        UiKit.Place((RectTransform)playAgain.transform, new Vector2(1f, 0f), new Vector2(-60f, 44f), new Vector2(300f, 76f));
        Button mainMenu = UiKit.CreateButton("MainMenu", w, "Main menu", MenuPickerScreen.SecondaryColor, () => onMainMenu?.Invoke(), new Vector2(300f, 76f), 30f);
        UiKit.Place((RectTransform)mainMenu.transform, new Vector2(1f, 0f), new Vector2(-390f, 44f), new Vector2(300f, 76f));
        if (style.ApplyButton(playAgain.image))
        {
            playAgain.image.color = new Color(0.75f, 1f, 0.75f, 1f);
            style.ApplyButton(mainMenu.image);
        }

        Navigation nav = mainMenu.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnRight = playAgain;
        mainMenu.navigation = nav;
        nav = playAgain.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnLeft = mainMenu;
        playAgain.navigation = nav;
        if (Game.Input.IsUsingGamepad)
        {
            EventSystem.current?.SetSelectedGameObject(playAgain.gameObject);
        }
    }

    private static void BuildStats(RectTransform window, RunResults results, FinalWindowStyle style)
    {
        ScoreTracker score = results.Score;
        float top = -190f;
        const float rowHeight = 56f;
        int row = 0;

        StatRow(window, top - rowHeight * row++, "Time survived", UiKit.FormatElapsed(results.ServiceSeconds));
        StatRow(window, top - rowHeight * row++, "Orders received", results.OrdersReceived.ToString());
        StatRow(window, top - rowHeight * row++, "Dishes served", score.DishesServed.ToString());
        StatRow(window, top - rowHeight * row++, "Total stars", ScoreTracker.FormatStars(score.TotalHalfStars));
        StatRow(window, top - rowHeight * row++, "Difficulty bonus", $"x{results.Multiplier:0.##}");
        TextMeshProUGUI final = Value(StatRow(window, top - rowHeight * row++, "Final score", null), $"{results.FinalScore:0.#}");
        final.color = new Color(0.91f, 0.42f, 0.17f, 1f);
        final.fontSize = 36f;

        // Average rating, drawn with the game's own star bar when available.
        float averageHalfStars = score.DishesServed > 0 ? (float)score.TotalHalfStars / score.DishesServed : 0f;
        RectTransform slot = StatRow(window, top - rowHeight * row++, "Average rating", null);
        if (!style.CreateStarBar(slot, averageHalfStars))
        {
            TextMeshProUGUI average = Value(slot, $"{averageHalfStars / 2f:0.0} / 5");
            average.alignment = TextAlignmentOptions.Left;
        }

        RectTransform strikes = StatRow(window, top - rowHeight * row, "Strikes", null);
        for (int i = 0; i < score.StrikesToLose; i++)
        {
            TextMeshProUGUI mark = UiKit.CreateText("Strike" + i, strikes, 40f, TextAlignmentOptions.Center, i < score.Strikes ? StrikeColor : StrikeEmptyColor);
            mark.fontStyle = FontStyles.Bold;
            mark.text = "X";
            UiKit.Place(mark.rectTransform, new Vector2(0f, 0.5f), new Vector2(i * 48f, 0f), new Vector2(48f, 54f));
        }
    }

    /// <summary>A label at the left and a 260-wide value slot to its right; returns the slot.</summary>
    private static RectTransform StatRow(RectTransform window, float y, string label, string value)
    {
        TextMeshProUGUI text = UiKit.CreateText("Label_" + label, window, 28f, TextAlignmentOptions.Left, MenuPickerScreen.MutedText);
        text.text = label;
        UiKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(70f, y), new Vector2(280f, 54f));

        RectTransform slot = UiKit.CreateRect("Value_" + label, window);
        UiKit.Place(slot, new Vector2(0f, 1f), new Vector2(360f, y), new Vector2(260f, 54f));
        if (value != null)
        {
            Value(slot, value);
        }
        return slot;
    }

    private static TextMeshProUGUI Value(RectTransform slot, string value)
    {
        TextMeshProUGUI text = UiKit.CreateText("Text", slot, 30f, TextAlignmentOptions.Left, ValueColor);
        text.fontStyle = FontStyles.Bold;
        text.text = value;
        UiKit.Stretch(text.rectTransform);
        return text;
    }

    private static void BuildMenu(RectTransform window, RunResults results)
    {
        const float left = 680f;
        TextMeshProUGUI header = UiKit.CreateText("MenuHeader", window, 28f, TextAlignmentOptions.Left, MenuPickerScreen.MutedText);
        header.text = "Tonight's menu";
        UiKit.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(left, -190f), new Vector2(500f, 54f));

        for (int i = 0; i < results.Menu.Count; i++)
        {
            Recipe recipe = results.Menu[i];
            float y = -250f - i * 92f;
            Image card = UiKit.CreatePanel("Dish" + i, window, MenuPickerScreen.CardColor);
            UiKit.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(left, y), new Vector2(500f, 82f));

            var render = new GameObject("Render", typeof(RectTransform)).AddComponent<Image>();
            render.transform.SetParent(card.transform, false);
            render.sprite = recipe.image;
            render.preserveAspect = true;
            render.raycastTarget = false;
            render.enabled = recipe.image != null;
            UiKit.Place(render.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(100f, 74f));

            TextMeshProUGUI name = UiKit.CreateText("Name", card.transform, 24f, TextAlignmentOptions.Left, Color.white);
            name.fontStyle = FontStyles.Bold;
            name.enableWordWrapping = true;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.text = Localization.Get(recipe);
            UiKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(370f, 74f));
        }

        TextMeshProUGUI difficulty = UiKit.CreateText("Difficulty", window, 26f, TextAlignmentOptions.Left, MenuPickerScreen.MutedText);
        difficulty.text = results.Rating != null ? "Menu difficulty: <b><color=#FFFFFF>" + results.Rating.Display + "</color></b>" : string.Empty;
        UiKit.Place(difficulty.rectTransform, new Vector2(0f, 1f), new Vector2(left, -250f - results.Menu.Count * 92f), new Vector2(500f, 48f));
    }

    private static void BuildBest(RectTransform window, RunResults results)
    {
        // Right column, under the menu difficulty: clear of the buttons along the bottom.
        TextMeshProUGUI best = UiKit.CreateText("Best", window, 24f, TextAlignmentOptions.TopLeft, MenuPickerScreen.MutedText);
        best.enableWordWrapping = true;
        float top = -250f - results.Menu.Count * 92f - 64f;
        UiKit.Place(best.rectTransform, new Vector2(0f, 1f), new Vector2(680f, top), new Vector2(500f, 110f));

        HighScoreResult hs = results.HighScore;
        if (hs == null)
        {
            best.text = "Debug keys were used, so this run was not saved.";
            return;
        }
        string newBest = "<color=#E86A2B><b>NEW BEST!</b></color>  ";
        string menuLine = hs.IsNewMenuBest
            ? newBest + "Best for this menu"
            : $"Best for this menu: <color=#FFFFFF><b>{HighScores.ScoreOf(hs.PreviousMenuBest):0.#}</b></color> points ({UiKit.FormatElapsed(hs.PreviousMenuBest.BestTimeSeconds)})";
        string kitchenLine = hs.KitchenBest == null
            ? string.Empty
            : (hs.IsNewKitchenBest ? "<color=#E86A2B><b>New kitchen record!</b></color>" : $"Kitchen best: <color=#FFFFFF><b>{HighScores.ScoreOf(hs.KitchenBest):0.#}</b></color> points ({UiKit.FormatElapsed(hs.KitchenBest.BestTimeSeconds)})");
        best.text = menuLine + "\n" + kitchenLine;
    }

    private void LateUpdate()
    {
        // Keep the cursor free while the popup is up, whatever the kitchen input does underneath.
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
}
