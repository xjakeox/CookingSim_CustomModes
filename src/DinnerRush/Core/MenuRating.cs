using System;
using System.Collections.Generic;
using System.Linq;

namespace DinnerRush.Core;

/// <summary>
/// Difficulty of a chosen menu (guide §5.3): the sum of each dish's RecipeDifficulty (Easy=1, Medium=2, Hard=3),
/// optionally plus each dish's workload intensity normalized to 0..1 across the pool.
/// </summary>
internal sealed class MenuRating
{
    private MenuRating(int difficultySum, int dishCount, float workloadBonus)
    {
        DifficultySum = difficultySum;
        DishCount = dishCount;
        WorkloadBonus = workloadBonus;
    }

    /// <summary>Sum of RecipeDifficulty over the menu (3..9 for 3 dishes).</summary>
    public int DifficultySum { get; }
    public int DishCount { get; }

    /// <summary>0 unless the workload refinement is enabled.</summary>
    public float WorkloadBonus { get; }

    public float Score => DifficultySum + WorkloadBonus;

    /// <summary>For 3 dishes: 3–4 Easy, 5–6 Medium, 7–9 Hard (scaled by dish count for other menu sizes).</summary>
    public string Label
    {
        get
        {
            if (DishCount == 0)
            {
                return "-";
            }
            float average = (float)DifficultySum / DishCount;
            return average < 1.5f ? "Easy" : average <= 2f ? "Medium" : "Hard";
        }
    }

    /// <summary>
    /// Score multiplier for this menu: 1x for the easiest possible menu (every dish Easy), plus
    /// <paramref name="perPoint"/> for each difficulty point above that. With 0.25 and 3 dishes: 3 = 1x, 6 = 1.75x, 9 = 2.5x.
    /// </summary>
    public float Multiplier(float perPoint) => 1f + perPoint * Math.Max(0f, Score - DishCount);

    public string Display => WorkloadBonus > 0f ? $"{Score:0.0} ({Label})" : $"{DifficultySum} ({Label})";

    /// <param name="difficulties">(int)RecipeDifficulty of each picked dish.</param>
    /// <param name="workloads">WorkloadIntensity of each picked dish (same order); ignored unless <paramref name="useWorkload"/>.</param>
    /// <param name="poolWorkloads">WorkloadIntensity of every dish in the pool, for normalization.</param>
    public static MenuRating Compute(IList<int> difficulties, IList<float> workloads, IList<float> poolWorkloads, bool useWorkload)
    {
        int sum = difficulties.Sum();
        float bonus = 0f;
        if (useWorkload && workloads.Count > 0 && poolWorkloads.Count > 0)
        {
            float min = poolWorkloads.Min();
            float max = poolWorkloads.Max();
            float range = max - min;
            if (range > 0f)
            {
                bonus = workloads.Sum(w => (w - min) / range);
            }
        }
        return new MenuRating(sum, difficulties.Count, bonus);
    }
}
