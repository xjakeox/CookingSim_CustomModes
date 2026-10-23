using System.Text;
using DinnerRush.Core;
using DinnerRush.Ui;
using TMPro;
using UnityEngine;

namespace DinnerRush;

/// <summary>
/// Screen overlay for messages only: the game-over notice and the optional debug line.
/// The run clock, score and strike marks are on the kitchen TVs (TvPanel / TvScoreBar).
/// </summary>
internal sealed class Hud : MonoBehaviour
{
    // Above gameplay HUD, below the game's popups (and we hide while paused anyway).
    private const int SortingOrder = 50;

    private static readonly Color GameOverColor = new Color(1f, 0.45f, 0.35f, 1f);

    private DinnerRushRun run;
    private Canvas canvas;
    private TextMeshProUGUI messageText;
    private TextMeshProUGUI debugText;
    private readonly StringBuilder builder = new StringBuilder();

    internal static Hud Create(Transform parent, DinnerRushRun run)
    {
        Canvas canvas = UiKit.CreateOverlayCanvas("DinnerRushHud", parent, SortingOrder);
        var hud = canvas.gameObject.AddComponent<Hud>();
        hud.run = run;
        hud.canvas = canvas;
        hud.Build();
        return hud;
    }

    private void Build()
    {
        Transform root = canvas.transform;

        messageText = UiKit.CreateText("Message", root, 40f, TextAlignmentOptions.Center, GameOverColor);
        UiKit.Place(messageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1200f, 150f));
        messageText.enableWordWrapping = true;

        debugText = UiKit.CreateText("Debug", root, 18f, TextAlignmentOptions.TopLeft, new Color(1f, 1f, 0.6f, 1f));
        UiKit.Place(debugText.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(700f, 200f));
        debugText.enableWordWrapping = true;
    }

    private void LateUpdate()
    {
        RunPhase phase = run.Phase;
        bool playing = phase == RunPhase.Prep || phase == RunPhase.Service;
        // Our own GameOver pause keeps the HUD up; anyone else's pause (ESC menu, dish summary) hides it.
        canvas.enabled = phase == RunPhase.GameOver || (playing && !GameManager.Paused);
        if (!canvas.enabled)
        {
            return;
        }

        ScoreTracker score = run.Score;

        // Fallback only: normally the results popup covers game over.
        messageText.text = phase == RunPhase.GameOver && !ResultsScreen.IsOpen
            ? $"GAME OVER  {UiKit.FormatElapsed(run.ServiceElapsed)}\n{run.EndReason}\n<size=70%>Stars {ScoreTracker.FormatStars(score.TotalHalfStars)}   Dishes {score.DishesServed}   Strikes {score.Strikes}/{score.StrikesToLose}</size>\n<size=60%>Quit to the menu from the pause menu.</size>"
            : string.Empty;

        debugText.gameObject.SetActive(DinnerRushConfig.DebugHudActive);
        if (DinnerRushConfig.DebugHudActive)
        {
            builder.Length = 0;
            builder.Append("phase=").Append(phase)
                .Append("  timeScale=").Append(Time.timeScale.ToString("0.##"))
                .Append("  paused=").Append(GameManager.Paused)
                .Append("\nprep=").Append(run.PrepRemaining.ToString("0.0"))
                .Append("  service=").Append(run.ServiceElapsed.ToString("0.0"))
                .Append("  dishes=").Append(score.DishesServed)
                .Append("\norders=").Append(run.OrdersSpawned)
                .Append("  active=").Append(ActiveOrderCount())
                .Append("  next in ").Append(Mathf.Max(0f, run.NextOrderAt - run.ServiceElapsed).ToString("0.0")).Append('s');
            debugText.text = builder.ToString();
        }
    }

    private static int ActiveOrderCount()
    {
        RecipesManager manager = WIUtils.TemporalSingleton<RecipesManager>.Me;
        return manager != null ? manager.currentRecipes.Count : 0;
    }
}
