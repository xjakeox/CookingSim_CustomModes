namespace DinnerRush.Core;

/// <summary>
/// Stars, dishes and strikes for one run. Plain class: no Unity or game dependencies.
/// The game scores dishes in half-star steps: RecipeScoreData.stars is half-stars (3 = 1.5★, 8 = 4★).
/// </summary>
internal sealed class ScoreTracker
{
    /// <param name="strikesToLose">The strike that ends the run (3 = two X's are fine, the third ends it).</param>
    /// <param name="strikeAtOrBelowStars">A dish rated this many stars or fewer is a strike (half-stars allowed, e.g. 3 or 2.5).</param>
    public ScoreTracker(int strikesToLose, float strikeAtOrBelowStars)
    {
        StrikesToLose = strikesToLose;
        StrikeAtOrBelowHalfStars = (int)System.Math.Round(strikeAtOrBelowStars * 2f);
    }

    public int StrikesToLose { get; }

    /// <summary>The strike cutoff in half-stars (6 = 3★).</summary>
    public int StrikeAtOrBelowHalfStars { get; }

    public int TotalHalfStars { get; private set; }

    /// <summary>Total score in stars, e.g. 5.5.</summary>
    public float TotalStars => TotalHalfStars / 2f;

    public int DishesServed { get; private set; }
    public int Strikes { get; private set; }

    /// <summary>
    /// With StrikesToLose = 3 you survive 2 strikes and the 3rd ends the run (confirmed by the user).
    /// To survive one more instead, change ">=" to ">".
    /// </summary>
    public bool StrikedOut => StrikesToLose > 0 && Strikes >= StrikesToLose;

    /// <summary>Records a served dish. Returns true when this dish ends the run.</summary>
    /// <param name="halfStars">RecipeScoreData.stars (half-star units).</param>
    public bool RegisterServe(int halfStars, bool failed, out bool strike)
    {
        // A failed dish shouldn't happen in sandbox; treat it as zero stars.
        if (failed || halfStars < 0)
        {
            halfStars = 0;
        }
        DishesServed++;
        TotalHalfStars += halfStars;
        strike = halfStars <= StrikeAtOrBelowHalfStars;
        if (strike)
        {
            Strikes++;
        }
        return StrikedOut;
    }

    /// <summary>Formats half-stars as stars: 7 -> "3.5", 8 -> "4".</summary>
    public static string FormatStars(int halfStars) => halfStars % 2 == 0 ? (halfStars / 2).ToString() : $"{halfStars / 2}.5";
}
