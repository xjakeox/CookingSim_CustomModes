using System;
using System.Collections.Generic;
using System.Linq;
using BC.GamingPlatforms;
using Recipes;
using WIUtils;

namespace DinnerRush.MenuPicker;

/// <summary>
/// The recipes Dinner Rush may offer: the sandbox's own recipe list (base + Food Network content),
/// minus DLC recipes the player doesn't own, seasonal event recipes, and anything unusable as an order.
/// </summary>
internal static class RecipePool
{
    private const string FoodNetworkContentId = "foodnetwork";

    // Event-only dishes (e.g. Halloween's "Braaains Shake", Christmas ham) are never offered.
    private static readonly HashSet<RecipePackage> SeasonalPackages = new HashSet<RecipePackage>
    {
        RecipePackage.Halloween,
        RecipePackage.Christmas,
        RecipePackage.Valentines2022,
        RecipePackage.Oktoberfest,
    };

    internal static List<Recipe> Build()
    {
        RecipesManager manager = TemporalSingleton<RecipesManager>.Me;
        if (manager == null || manager.Recipes == null)
        {
            Plugin.Log.LogError("RecipesManager not available; recipe pool is empty.");
            return new List<Recipe>();
        }

        // Checked once per run, not per recipe or per frame.
        bool foodNetworkOwned = IsDlcOwned(DLCService.FOODNETWORK_DLC_NAME);
        var pool = new List<Recipe>();
        int skippedDlc = 0, skippedSeasonal = 0, skippedTime = 0;
        foreach (Recipe recipe in manager.GetRecipes())
        {
            if (recipe == null)
            {
                continue;
            }
            if (!foodNetworkOwned && recipe.GetContentID() == FoodNetworkContentId)
            {
                skippedDlc++;
                continue;
            }
            if (SeasonalPackages.Contains(recipe.RecipePackageType))
            {
                skippedSeasonal++;
                continue;
            }
            if (SafeEstimatedTime(recipe) <= 0f)
            {
                skippedTime++;
                continue;
            }
            pool.Add(recipe);
        }

        string difficulties = string.Join(", ", pool.GroupBy(r => r.RecipeDifficulty).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}"));
        Plugin.Log.LogInfo("Recipe pool contents: " + string.Join("; ", pool.Select(r => $"{Localization.Get(r)} [{r.RecipePackageType}/{r.GetContentID()}]")));
        Plugin.Log.LogInfo($"Recipe pool: {pool.Count} usable (Food Network owned: {foodNetworkOwned}; skipped DLC {skippedDlc}, seasonal {skippedSeasonal}, no time {skippedTime}). Difficulty spread: {difficulties}");
        return pool;
    }

    /// <summary>
    /// Draws up to <paramref name="count"/> distinct dishes at random: skips recipes without a dish render
    /// and recipes whose display name duplicates one already drawn (e.g. upgrade tiers).
    /// </summary>
    internal static List<Recipe> Draw(List<Recipe> pool, int count, Random rng)
    {
        var shuffled = new List<Recipe>(pool);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var drawn = new List<Recipe>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Recipe recipe in shuffled)
        {
            if (drawn.Count >= count)
            {
                break;
            }
            if (recipe.image == null || !names.Add(Localization.Get(recipe)))
            {
                continue;
            }
            drawn.Add(recipe);
        }
        return drawn;
    }

    internal static float SafeEstimatedTime(Recipe recipe)
    {
        try
        {
            return recipe.EstimatedTimeRAW;
        }
        catch (Exception)
        {
            // Recipes with no components throw from FreeTime/FastTime; treat them as unusable.
            return 0f;
        }
    }

    internal static float SafeWorkload(Recipe recipe)
    {
        try
        {
            return recipe.WorkloadIntensity;
        }
        catch (Exception)
        {
            return 0f;
        }
    }

    private static bool IsDlcOwned(string dlcName)
    {
        try
        {
            return (GamingPlatformManager.InAppPurchases.IsDlcPurchased(dlcName, out bool purchased) & purchased)
                && GamingPlatformManager.InAppPurchases.IsDlcInstalled(dlcName, out bool installed) && installed;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"DLC check for '{dlcName}' failed, treating as not owned: {e.Message}");
            return false;
        }
    }
}
