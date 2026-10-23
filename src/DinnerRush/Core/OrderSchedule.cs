using System;
using System.Collections.Generic;
using System.Linq;

namespace DinnerRush.Core;

/// <summary>
/// The order ramp (guide §5.5), in service-elapsed seconds:
///   t(1) = first delay;  gap(1) = second delay;  gap(n) = max(min gap, gap(n-1) * (1 - decay));  t(n+1) = t(n) + gap(n).
/// </summary>
internal sealed class OrderSchedule
{
    private readonly float decay;
    private readonly float minGap;

    public OrderSchedule(float firstDelay, float secondDelay, float decay, float minGap)
    {
        this.decay = decay;
        this.minGap = minGap;
        NextOrderAt = firstDelay;
        CurrentGap = Math.Max(minGap, secondDelay);
    }

    /// <summary>Service time at which the next order is due.</summary>
    public float NextOrderAt { get; private set; }

    /// <summary>Gap that will follow the next order.</summary>
    public float CurrentGap { get; private set; }

    public int OrdersDue { get; private set; }

    /// <summary>True (once per order) when an order is due at <paramref name="serviceElapsed"/>; advances the schedule.</summary>
    public bool TryConsume(float serviceElapsed)
    {
        if (serviceElapsed < NextOrderAt)
        {
            return false;
        }
        OrdersDue++;
        NextOrderAt += CurrentGap;
        CurrentGap = Math.Max(minGap, CurrentGap * (1f - decay));
        return true;
    }
}

/// <summary>Picks each order's dish uniformly from the menu, avoiding long runs of the same dish.</summary>
internal sealed class DishPicker
{
    private readonly IReadOnlyList<int> menu;
    private readonly int maxSameInARow;
    private readonly Random rng;
    private readonly List<int> history = new List<int>();

    /// <param name="maxSameInARow">0 disables the rule.</param>
    public DishPicker(IReadOnlyList<int> menu, int maxSameInARow, Random rng)
    {
        this.menu = menu;
        this.maxSameInARow = maxSameInARow;
        this.rng = rng;
    }

    public int Next()
    {
        int pick = menu[rng.Next(menu.Count)];
        if (maxSameInARow > 0 && menu.Distinct().Count() > 1 && history.Count >= maxSameInARow
            && history.Skip(history.Count - maxSameInARow).All(id => id == pick))
        {
            List<int> others = menu.Where(id => id != pick).ToList();
            pick = others[rng.Next(others.Count)];
        }
        history.Add(pick);
        return pick;
    }
}
