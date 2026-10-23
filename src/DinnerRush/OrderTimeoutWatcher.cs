using System.Collections.Generic;
using CS.GUI;
using Recipes;
using WIUtils;

namespace DinnerRush;

/// <summary>
/// Ticks our orders' timers (the base game doesn't in sandbox) and reports the first one to run out.
/// There is no overtime grace: an order expires when the time shown on the TV reaches zero.
/// </summary>
internal sealed class OrderTimeoutWatcher
{
    private const float NearEndSeconds = 60f; // same threshold the career uses

    // currentTime we last wrote per order, to detect anyone else ticking the same timer.
    private readonly Dictionary<Recipe, float> tracked = new Dictionary<Recipe, float>();
    private readonly List<Recipe> finished = new List<Recipe>();
    private bool externalTickWarned;

    public int ActiveCount => tracked.Count;

    public void Track(Recipe order)
    {
        tracked[order] = order.currentTime;
    }

    /// <summary>
    /// Debug: sets the most urgent open order that still has more than <paramref name="secondsLeft"/> to exactly that.
    /// Repeated presses rush the next order. Returns the rushed order, or null if none qualifies.
    /// </summary>
    public Recipe RushMostUrgent(float secondsLeft)
    {
        Recipe target = null;
        foreach (Recipe order in tracked.Keys)
        {
            if (order != null && order.RemainingTime > secondsLeft && (target == null || order.RemainingTime < target.RemainingTime))
            {
                target = order;
            }
        }
        if (target != null)
        {
            target.currentTime = target.EstimatedTimeRAW - secondsLeft;
            tracked[target] = target.currentTime; // our own write, not an external tick
        }
        return target;
    }

    /// <summary>
    /// Advances every live order by scaled <paramref name="deltaTime"/> and returns the first expired order, or null.
    /// Run in LateUpdate so a dish served this frame (removed from currentRecipes by EndRecipe) wins over expiry.
    /// </summary>
    public Recipe Tick(float deltaTime)
    {
        RecipesManager manager = TemporalSingleton<RecipesManager>.Me;
        if (manager == null)
        {
            return null;
        }

        finished.Clear();
        Recipe expired = null;
        foreach (KeyValuePair<Recipe, float> entry in tracked)
        {
            Recipe order = entry.Key;
            if (order == null || !manager.currentRecipes.Contains(order))
            {
                finished.Add(order); // served (or otherwise ended)
                continue;
            }

            // If something else has advanced the timer since our last write (e.g. a game update re-enabling
            // sandbox ticking), adopt its value instead of adding ours, so the order doesn't expire at 2x speed.
            if (order.currentTime - entry.Value > 0.0001f)
            {
                if (!externalTickWarned)
                {
                    externalTickWarned = true;
                    Plugin.Log.LogWarning("Order timers are being advanced by something else; Dinner Rush will stop adding its own time.");
                }
            }
            else
            {
                order.currentTime += deltaTime;
            }

            if (order.RemainingTime <= NearEndSeconds && order.RemainingTime > 0f && order.timeStage == RecipeTimeStage.STARTED)
            {
                order.timeStage = RecipeTimeStage.NEAR_END;
                GUIKitchen.Me?.notifications?.Notify(NotificationType.NearEndOrder, order);
            }
            if (expired == null && order.RemainingTime <= 0f)
            {
                expired = order;
            }
        }

        foreach (Recipe order in finished)
        {
            tracked.Remove(order);
        }
        // Record our writes after the loop (can't modify the dictionary while enumerating it).
        foreach (Recipe order in new List<Recipe>(tracked.Keys))
        {
            tracked[order] = order.currentTime;
        }
        return expired;
    }
}
