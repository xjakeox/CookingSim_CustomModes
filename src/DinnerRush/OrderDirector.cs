using System;
using System.Collections.Generic;
using DinnerRush.Core;
using DinnerRush.Ui;
using WIUtils;

namespace DinnerRush;

/// <summary>
/// Spawns service orders on the ramp schedule through the game's own RecipesManager.StartRecipe
/// (the same call the sandbox "order" button uses), leaving the order time at the career default.
/// </summary>
internal sealed class OrderDirector
{
    private readonly DinnerRushRun run;
    private readonly OrderSchedule schedule;
    private readonly DishPicker picker;
    private readonly OrderTimeoutWatcher watcher;
    private readonly int maxActiveOrders;

    /// <summary>True only while Dinner Rush itself is starting an order.</summary>
    internal static bool Spawning { get; private set; }

    public OrderDirector(DinnerRushRun run, OrderTimeoutWatcher watcher, Random rng)
    {
        this.run = run;
        this.watcher = watcher;
        schedule = new OrderSchedule(
            DinnerRushConfig.FirstOrderDelaySeconds.Value,
            DinnerRushConfig.SecondOrderDelaySeconds.Value,
            DinnerRushConfig.GapDecayPerOrder.Value,
            DinnerRushConfig.MinGapSeconds.Value);
        picker = new DishPicker(run.Menu, DinnerRushConfig.MaxSameDishInARow.Value, rng);
        maxActiveOrders = DinnerRushConfig.MaxActiveOrders.Value;
        run.NextOrderAt = schedule.NextOrderAt;
    }

    /// <summary>Call every frame during service.</summary>
    public void Update()
    {
        // Optional cap: hold the schedule while the kitchen is full (0 = no limit, the default).
        if (maxActiveOrders > 0 && watcher.ActiveCount >= maxActiveOrders)
        {
            return;
        }
        while (schedule.TryConsume(run.ServiceElapsed))
        {
            float gap = schedule.CurrentGap;
            Spawn(picker.Next());
            run.NextOrderAt = schedule.NextOrderAt;
            Plugin.Log.LogInfo($"Order #{run.OrdersSpawned} at {UiKit.FormatElapsed(run.ServiceElapsed)}; next in {schedule.NextOrderAt - run.ServiceElapsed:0.0}s (gap after that {gap:0.0}s).");
        }
    }

    /// <summary>Debug: starts the next dish right away, outside the schedule (which carries on unchanged).</summary>
    public void SpawnNow()
    {
        Spawn(picker.Next());
        Plugin.Log.LogInfo($"Debug: extra order #{run.OrdersSpawned} at {UiKit.FormatElapsed(run.ServiceElapsed)}.");
    }

    private void Spawn(int recipeId)
    {
        RecipesManager manager = TemporalSingleton<RecipesManager>.Me;
        if (manager == null)
        {
            Plugin.Log.LogError("RecipesManager missing; cannot start an order.");
            return;
        }
        // During a run every other StartRecipe call is blocked (see AntiExploitPatches); let ours through.
        Recipe order;
        Spawning = true;
        try
        {
            order = manager.StartRecipe(recipeId);
        }
        finally
        {
            Spawning = false;
        }
        if (order == null)
        {
            Plugin.Log.LogError($"StartRecipe({recipeId}) returned null.");
            return;
        }
        // Sandbox orders are cancellable; a cancelled order would dodge its timeout.
        order.IsCancelable = false;
        run.RecordOrderSpawned();
        watcher.Track(order);
        Plugin.Log.LogInfo($"  -> {Localization.Get(order)} ({order.EstimatedTimeRAW:0}s to serve).");
    }
}
