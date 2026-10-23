using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Newtonsoft.Json;

namespace DinnerRush;

/// <summary>Best result for one kitchen + menu combination.</summary>
internal sealed class HighScoreRecord
{
    public string KitchenId { get; set; }
    public int[] Menu { get; set; }
    public string[] MenuNames { get; set; }
    /// <summary>Final score (stars x difficulty multiplier). The ranking value.</summary>
    public float BestScore { get; set; }
    public int BestHalfStars { get; set; }
    public float BestTimeSeconds { get; set; }
    public int BestDishes { get; set; }
    public float Difficulty { get; set; }
    public string DifficultyLabel { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>What a finished run is compared against.</summary>
internal sealed class HighScoreResult
{
    /// <summary>Best for this kitchen + menu before this run (null if first time).</summary>
    public HighScoreRecord PreviousMenuBest { get; set; }

    /// <summary>Best for this kitchen across all menus, after this run.</summary>
    public HighScoreRecord KitchenBest { get; set; }

    public bool IsNewMenuBest { get; set; }
    public bool IsNewKitchenBest { get; set; }
}

/// <summary>
/// Dinner Rush high scores in BepInEx\config\DinnerRush.highscores.json (never the game's own save system).
/// Keyed by kitchen + sorted recipe ids. File errors are logged and never break a run.
/// </summary>
internal static class HighScores
{
    private static string FilePath => Path.Combine(Paths.ConfigPath, "DinnerRush.highscores.json");

    /// <summary>Records a finished run and reports how it compares.</summary>
    internal static HighScoreResult Submit(HighScoreRecord run)
    {
        Dictionary<string, HighScoreRecord> scores = Load();
        string key = Key(run.KitchenId, run.Menu);
        HighScoreRecord kitchenBestBefore = BestFor(scores, run.KitchenId);

        scores.TryGetValue(key, out HighScoreRecord previous);
        var result = new HighScoreResult
        {
            PreviousMenuBest = previous,
            IsNewMenuBest = previous == null || IsBetter(run, previous),
            IsNewKitchenBest = kitchenBestBefore == null || IsBetter(run, kitchenBestBefore),
        };
        if (result.IsNewMenuBest)
        {
            scores[key] = run;
            Save(scores);
        }
        result.KitchenBest = BestFor(scores, run.KitchenId);
        return result;
    }

    /// <summary>Higher final score wins; on a tie, surviving longer wins.</summary>
    private static bool IsBetter(HighScoreRecord a, HighScoreRecord b) =>
        ScoreOf(a) > ScoreOf(b) || (ScoreOf(a) == ScoreOf(b) && a.BestTimeSeconds > b.BestTimeSeconds);

    /// <summary>Records saved before the multiplier existed have no BestScore; their stars stand in.</summary>
    internal static float ScoreOf(HighScoreRecord r) => r.BestScore > 0f ? r.BestScore : r.BestHalfStars / 2f;

    private static HighScoreRecord BestFor(Dictionary<string, HighScoreRecord> scores, string kitchenId)
    {
        HighScoreRecord best = null;
        foreach (HighScoreRecord record in scores.Values.Where(r => r.KitchenId == kitchenId))
        {
            if (best == null || IsBetter(record, best))
            {
                best = record;
            }
        }
        return best;
    }

    private static string Key(string kitchenId, IEnumerable<int> menu) => kitchenId + "|" + string.Join(",", menu.OrderBy(id => id));

    private static Dictionary<string, HighScoreRecord> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new Dictionary<string, HighScoreRecord>();
            }
            return JsonConvert.DeserializeObject<Dictionary<string, HighScoreRecord>>(File.ReadAllText(FilePath))
                ?? new Dictionary<string, HighScoreRecord>();
        }
        catch (Exception e)
        {
            // Keep the unreadable file for inspection and start fresh.
            Plugin.Log.LogWarning($"High score file unreadable, starting a new one: {e.Message}");
            try
            {
                File.Copy(FilePath, FilePath + ".bad", overwrite: true);
            }
            catch (Exception)
            {
                // Best effort only.
            }
            return new Dictionary<string, HighScoreRecord>();
        }
    }

    private static void Save(Dictionary<string, HighScoreRecord> scores)
    {
        try
        {
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(scores, Formatting.Indented));
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
            File.Move(temp, FilePath);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not save high scores: {e.Message}");
        }
    }
}
