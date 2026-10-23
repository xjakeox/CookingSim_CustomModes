using System;
using System.Collections.Generic;

namespace DinnerRush.Core;

internal enum RunPhase
{
    Idle,
    MenuSelect,
    Prep,
    Service,
    GameOver,
}

internal enum GameOverKind
{
    OrderExpired,
    TooManyStrikes,
    Debug,
}

internal sealed class GameOverReason
{
    public GameOverReason(GameOverKind kind, string detail)
    {
        Kind = kind;
        Detail = detail;
    }

    public GameOverKind Kind { get; }

    /// <summary>For OrderExpired, the dish name.</summary>
    public string Detail { get; }

    public override string ToString() => Kind switch
    {
        GameOverKind.OrderExpired => $"Order expired: {Detail}",
        GameOverKind.TooManyStrikes => "Too many low-rated dishes",
        _ => string.IsNullOrEmpty(Detail) ? "Run ended" : Detail,
    };
}

/// <summary>
/// The Dinner Rush state machine: Idle -> MenuSelect -> Prep -> Service -> GameOver.
/// Plain class driven by an external clock (<see cref="Tick"/>), so it stays testable and a future host can drive it.
/// </summary>
internal sealed class DinnerRushRun
{
    public DinnerRushRun(float prepSeconds, int strikesToLose, float strikeAtOrBelowStars, float multiplierPerDifficultyPoint)
    {
        PrepSeconds = prepSeconds;
        multiplierPerPoint = multiplierPerDifficultyPoint;
        Score = new ScoreTracker(strikesToLose, strikeAtOrBelowStars);
    }

    private readonly float multiplierPerPoint;

    public event Action<RunPhase> PhaseChanged;

    public RunPhase Phase { get; private set; } = RunPhase.Idle;
    public float PrepSeconds { get; }
    public float PrepRemaining { get; private set; }
    public float ServiceElapsed { get; private set; }
    public ScoreTracker Score { get; }
    public GameOverReason EndReason { get; private set; }

    /// <summary>Recipe ids of the chosen menu (set when the menu is confirmed).</summary>
    public IReadOnlyList<int> Menu { get; private set; } = Array.Empty<int>();
    public MenuRating Rating { get; private set; }

    /// <summary>Difficulty bonus for the chosen menu (1x until a menu is confirmed).</summary>
    public float ScoreMultiplier { get; private set; } = 1f;

    /// <summary>Stars earned times the menu's difficulty multiplier, rounded to one decimal.</summary>
    public float FinalScore => (float)Math.Round(Score.TotalStars * ScoreMultiplier, 1);

    /// <summary>Orders started during service.</summary>
    public int OrdersSpawned { get; private set; }

    /// <summary>Service time of the next scheduled order (set by the order director; for the debug HUD).</summary>
    public float NextOrderAt { get; set; }

    public void RecordOrderSpawned() => OrdersSpawned++;

    /// <summary>Whether the game should be held paused in this phase.</summary>
    public bool HoldsPause => Phase == RunPhase.MenuSelect || Phase == RunPhase.GameOver;

    public void BeginMenuSelect()
    {
        if (Phase == RunPhase.Idle)
        {
            SetPhase(RunPhase.MenuSelect);
        }
    }

    public void ConfirmMenu(IReadOnlyList<int> menu, MenuRating rating)
    {
        if (Phase != RunPhase.MenuSelect)
        {
            return;
        }
        Menu = menu;
        Rating = rating;
        ScoreMultiplier = rating != null ? rating.Multiplier(multiplierPerPoint) : 1f;
        PrepRemaining = PrepSeconds;
        SetPhase(RunPhase.Prep);
    }

    /// <summary>Ends prep early (TV skip button or debug key) and starts service.</summary>
    public void SkipPrep()
    {
        if (Phase == RunPhase.Prep)
        {
            StartService();
        }
    }

    public void EndGame(GameOverReason reason)
    {
        if (Phase == RunPhase.GameOver || Phase == RunPhase.Idle)
        {
            return;
        }
        EndReason = reason;
        SetPhase(RunPhase.GameOver);
    }

    /// <summary>Advance by scaled game time. Pausing the game (timeScale 0) therefore freezes every clock.</summary>
    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }
        switch (Phase)
        {
            case RunPhase.Prep:
                PrepRemaining -= deltaTime;
                if (PrepRemaining <= 0f)
                {
                    PrepRemaining = 0f;
                    StartService();
                }
                break;
            case RunPhase.Service:
                ServiceElapsed += deltaTime;
                break;
        }
    }

    private void StartService()
    {
        PrepRemaining = 0f;
        ServiceElapsed = 0f;
        SetPhase(RunPhase.Service);
    }

    private void SetPhase(RunPhase phase)
    {
        Phase = phase;
        PhaseChanged?.Invoke(phase);
    }
}
