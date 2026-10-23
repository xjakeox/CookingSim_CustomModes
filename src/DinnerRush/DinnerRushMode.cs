using System;
using System.Collections.Generic;
using System.Linq;
using CS.GUI;
using CustomModes.API;
using DinnerRush.Core;
using DinnerRush.MenuPicker;
using DinnerRush.Tv;
using DinnerRush.Ui;
using Recipes;
using UnityEngine;
using UnityEngine.InputSystem;
using WIUtils;

namespace DinnerRush;

/// <summary>
/// Drives one Dinner Rush run: ticks the state machine with scaled time, owns the HUD and the TV panels,
/// and holds the game paused in phases that need it. One instance per run.
/// </summary>
internal sealed class DinnerRushMode : MonoBehaviour
{
    private const float TvSyncInterval = 0.5f;
    private const float DebugRushSeconds = 10f;

    private Hud hud;
    private System.Random rng;
    private OrderDirector director;
    private OrderTimeoutWatcher watcher;
    private bool holdingPause;
    private bool debugKeysUsed; // runs that used debug keys are not saved as high scores
    private float nextTvSync;

    internal static DinnerRushMode Current { get; private set; }

    internal DinnerRushRun Run { get; private set; }
    internal KitchenContext Kitchen { get; private set; }

    /// <summary>The chosen dishes; orders are drawn from these.</summary>
    internal List<Recipe> MenuRecipes { get; private set; } = new List<Recipe>();

    /// <summary>Paused while choosing the menu, at game over, and while a confirmation is up.</summary>
    private bool ShouldHoldPause => Run.HoldsPause || ConfirmPopup.IsOpen;

    internal static void Begin(KitchenContext context)
    {
        End();
        var root = new GameObject("DinnerRush");
        DontDestroyOnLoad(root);
        Current = root.AddComponent<DinnerRushMode>();
        Current.Init(context);
    }

    /// <summary>Tears down the current run (mode ended, back to menu). Safe to call repeatedly.</summary>
    internal static void End()
    {
        if (Current == null)
        {
            return;
        }
        DinnerRushMode mode = Current;
        Current = null;
        MenuPickerScreen.Close();
        ResultsScreen.Close();
        ConfirmPopup.Dismiss();
        TvPanel.HideAll();
        mode.ReleasePause();
        // The game also re-enables saving whenever it leaves a kitchen; this covers ending without leaving.
        Game.SaveLoadManager?.EnableSaving();
        Destroy(mode.gameObject);
    }

    /// <summary>TV skip button: confirm, then end prep early.</summary>
    internal void RequestSkipPrep()
    {
        if (Run.Phase != RunPhase.Prep || ConfirmPopup.IsOpen)
        {
            return;
        }
        bool shown = ConfirmPopup.Show("skip_prep", "Skip prep and start service now?", Run.SkipPrep, null);
        if (shown)
        {
            HoldPause();
        }
        else
        {
            Plugin.Log.LogWarning("Game popup unavailable; skipping prep without confirmation.");
            Run.SkipPrep();
        }
    }

    private void Init(KitchenContext context)
    {
        Kitchen = context;
        // System.Random, not UnityEngine.Random: other mods seeding Unity's RNG must not make runs repeat.
        rng = new System.Random(Environment.TickCount);
        Run = new DinnerRushRun(DinnerRushConfig.PrepSeconds.Value, DinnerRushConfig.StrikesToLose.Value, DinnerRushConfig.StrikeAtOrBelowStars.Value,
            DinnerRushConfig.MultiplierPerDifficultyPoint.Value);
        Run.PhaseChanged += OnPhaseChanged;
        // Static event raised only for real serves at the checkout (not analyzer/discard paths).
        CheckoutStand.OnBringOutOrder += OnServed;
        hud = Hud.Create(transform, Run);

        // Runs are not save-compatible: no autosave, quicksave or pause-menu save may capture one.
        Game.SaveLoadManager.DisableSaving();
        Plugin.Log.LogInfo($"Dinner Rush run started in kitchen '{context.KitchenId}' (saving disabled).");
        Run.BeginMenuSelect();
        OpenMenuPicker();
    }

    private void OpenMenuPicker()
    {
        List<Recipe> pool = RecipePool.Build();
        List<Recipe> choices = RecipePool.Draw(pool, DinnerRushConfig.MenuChoices.Value, rng);
        int menuSize = Math.Min(DinnerRushConfig.MenuSize.Value, choices.Count);
        if (menuSize == 0)
        {
            Plugin.Log.LogError("No usable recipes for Dinner Rush; ending the run.");
            Run.EndGame(new GameOverReason(GameOverKind.Debug, "No usable recipes were found for this kitchen."));
            return;
        }
        Plugin.Log.LogInfo($"Menu picker: offering {choices.Count} dishes: {string.Join(", ", choices.Select(r => Localization.Get(r)))}");
        MenuPickerScreen.Open(transform, choices, pool, menuSize, DinnerRushConfig.UseWorkloadDifficulty.Value, OnMenuConfirmed, QuitToMenu);
    }

    private void OnMenuConfirmed(List<Recipe> picked, MenuRating rating)
    {
        MenuRecipes = picked;
        Plugin.Log.LogInfo($"Menu confirmed: {string.Join(", ", picked.Select(r => $"{Localization.Get(r)} [{r.CoreId.Id}, {r.RecipeDifficulty}]"))}. Difficulty {rating.Display}, score x{rating.Multiplier(DinnerRushConfig.MultiplierPerDifficultyPoint.Value):0.##}.");
        Run.ConfirmMenu(picked.Select(r => r.CoreId.Id).ToList(), rating);
        PinMenuToTvs(picked);
    }

    /// <summary>Pins the chosen dishes' recipes to every TV, as the player could from the computer.</summary>
    private static void PinMenuToTvs(List<Recipe> picked)
    {
        RecipesManager manager = TemporalSingleton<RecipesManager>.Me;
        foreach (Recipe recipe in picked)
        {
            try
            {
                Recipe template = manager != null ? manager.GetRecipeTemplate(recipe.CoreId.Id) : null;
                CS.GUI.TV.TVUI.CreateRecipeTemplate(template ?? recipe); // no-op if already pinned
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not pin {Localization.Get(recipe)} to the TVs: {e.Message}");
            }
        }
    }

    private static void QuitToMenu()
    {
        MenuPickerScreen.Close();
        ResultsScreen.Close();
        // The game's own exit path (as used by the pause menu); the mode ends when the game state returns to Menu.
        GUIKitchen.Me.QuitToMenu();
    }

    private void Update()
    {
        if (Run == null)
        {
            return;
        }
        // Something (e.g. a kitchen reset) may switch saving back on; keep it off for the whole run.
        if (Game.SaveLoadManager != null && !Game.SaveLoadManager.SavingDisabled)
        {
            Game.SaveLoadManager.DisableSaving();
        }

        // Scaled time on purpose: the game pauses with Time.timeScale = 0.
        Run.Tick(Time.deltaTime);
        if (Run.Phase == RunPhase.Service)
        {
            director?.Update();
        }

        if (TvPanel.Active && Time.unscaledTime >= nextTvSync)
        {
            nextTvSync = Time.unscaledTime + TvSyncInterval;
            TvPanel.ShowOnAll();
        }
        HandleDebugKeys();
    }

    private void LateUpdate()
    {
        if (Run == null)
        {
            return;
        }
        // After Update and event handlers, so a dish served this frame wins over its expiry.
        if (Run.Phase == RunPhase.Service && watcher != null)
        {
            Recipe expired = watcher.Tick(Time.deltaTime);
            if (expired != null)
            {
                Run.EndGame(new GameOverReason(GameOverKind.OrderExpired, Localization.Get(expired)));
            }
        }
        // GameManager.UnpauseTime always restores the time scale, even if someone else paused first,
        // so closing e.g. the pause menu during our paused phases would resume the game. Re-assert every frame.
        if (ShouldHoldPause)
        {
            HoldPause();
        }
        else if (holdingPause)
        {
            ReleasePause();
        }
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        Plugin.Log.LogInfo($"Dinner Rush phase -> {phase}" + (phase == RunPhase.GameOver ? $" ({Run.EndReason})" : ""));
        if (phase != RunPhase.Prep)
        {
            ConfirmPopup.Dismiss();
        }
        if (phase == RunPhase.GameOver)
        {
            ShowResults();
        }
        if (phase == RunPhase.Service)
        {
            watcher = new OrderTimeoutWatcher();
            director = new OrderDirector(Run, watcher, rng);
        }
        if (TvPanel.Active)
        {
            TvPanel.ShowOnAll(); // reconfigures each TV for the new phase
        }
        else
        {
            TvPanel.HideAll();
        }
    }

    private void ShowResults()
    {
        try
        {
            var results = new RunResults
            {
                Reason = Run.EndReason,
                ServiceSeconds = Run.ServiceElapsed,
                OrdersReceived = Run.OrdersSpawned,
                Score = Run.Score,
                Menu = MenuRecipes,
                Rating = Run.Rating,
                Multiplier = Run.ScoreMultiplier,
                FinalScore = Run.FinalScore,
            };
            // Only real runs count: not debug endings, debug-key runs, or runs that never got a menu.
            bool saveable = !debugKeysUsed && Run.EndReason?.Kind != GameOverKind.Debug && MenuRecipes.Count > 0;
            if (saveable)
            {
                results.HighScore = HighScores.Submit(new HighScoreRecord
                {
                    KitchenId = Kitchen.KitchenId,
                    Menu = Run.Menu.ToArray(),
                    MenuNames = MenuRecipes.Select(r => Localization.Get(r)).ToArray(),
                    BestScore = Run.FinalScore,
                    BestHalfStars = Run.Score.TotalHalfStars,
                    BestTimeSeconds = Run.ServiceElapsed,
                    BestDishes = Run.Score.DishesServed,
                    Difficulty = Run.Rating?.Score ?? 0f,
                    DifficultyLabel = Run.Rating?.Label,
                    Timestamp = DateTime.Now,
                });
                Plugin.Log.LogInfo($"High score: new menu best {results.HighScore.IsNewMenuBest}, new kitchen best {results.HighScore.IsNewKitchenBest}.");
            }
            string kitchenId = Kitchen.KitchenId;
            ResultsScreen.Open(transform, results, () => PlayAgain(kitchenId), QuitToMenu);
        }
        catch (Exception e)
        {
            // The HUD's game-over message stays up as a fallback.
            Plugin.Log.LogError($"Could not show the results screen: {e}");
        }
    }

    /// <summary>Back to the main menu, then straight into a fresh Dinner Rush in the same kitchen (new 10 dishes).</summary>
    private static void PlayAgain(string kitchenId)
    {
        ResultsScreen.Close();
        GUIKitchen.Me.QuitToMenu(() => CustomModesApi.StartMode(Plugin.ModeId, new KitchenContext(kitchenId)));
    }

    private void OnServed(RecipeScoreData score)
    {
        if (Run == null || Run.Phase != RunPhase.Service || score == null)
        {
            return;
        }
        bool strikedOut = Run.Score.RegisterServe(score.stars, score.failed, out bool strike);
        Plugin.Log.LogInfo($"Served dish {score.ID}: {ScoreTracker.FormatStars(score.stars)} stars (fame {score.fameDelta:+0;-0;0})" +
            $"{(strike ? " -> STRIKE" : "")}. Score {ScoreTracker.FormatStars(Run.Score.TotalHalfStars)}, strikes {Run.Score.Strikes}/{Run.Score.StrikesToLose}.");
        if (strikedOut)
        {
            Run.EndGame(new GameOverReason(GameOverKind.TooManyStrikes, null));
        }
    }

    private void HandleDebugKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (!DinnerRushConfig.DebugMode.Value || keyboard == null)
        {
            return;
        }
        if (WasPressed(keyboard, DinnerRushConfig.DebugRushOrderKey.Value) && Run.Phase == RunPhase.Service && watcher != null)
        {
            debugKeysUsed = true;
            Recipe rushed = watcher.RushMostUrgent(DebugRushSeconds);
            Plugin.Log.LogInfo(rushed != null
                ? $"Debug: {Localization.Get(rushed)} now has {rushed.RemainingTime:0.0}s left."
                : "Debug: no open order to rush.");
        }
        else if (WasPressed(keyboard, DinnerRushConfig.DebugSpawnOrderKey.Value) && Run.Phase == RunPhase.Service && director != null)
        {
            debugKeysUsed = true;
            director.SpawnNow();
        }
        else if (WasPressed(keyboard, DinnerRushConfig.DebugGameOverKey.Value))
        {
            Run.EndGame(new GameOverReason(GameOverKind.Debug, "Ended with debug key"));
        }
    }

    private static bool WasPressed(Keyboard keyboard, Key key)
    {
        return key != Key.None && keyboard[key].wasPressedThisFrame;
    }

    private void HoldPause()
    {
        GameManager manager = Singleton<GameManager>.Me;
        if (manager != null && Time.timeScale != 0f)
        {
            manager.PauseTime();
        }
        holdingPause = true;
    }

    private void ReleasePause()
    {
        if (!holdingPause)
        {
            return;
        }
        holdingPause = false;
        GameManager manager = Singleton<GameManager>.Me;
        if (manager != null && GameManager.Paused)
        {
            manager.UnpauseTime();
        }
    }

    private void OnDestroy()
    {
        CheckoutStand.OnBringOutOrder -= OnServed;
        if (Run != null)
        {
            Run.PhaseChanged -= OnPhaseChanged;
        }
        if (Current == this)
        {
            Current = null;
        }
    }
}
