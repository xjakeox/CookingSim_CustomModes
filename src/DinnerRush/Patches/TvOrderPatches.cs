using CS.GUI.TV;
using HarmonyLib;
using UnityEngine;

namespace DinnerRush.Patches;

/// <summary>
/// Sandbox hides the timer ribbon and progress bar on TV orders (orders are "not time constrained").
/// During Dinner Rush they are, so show them.
/// </summary>
[HarmonyPatch(typeof(TVUIOrderIcon), nameof(TVUIOrderIcon.RefreshIconElements))]
internal static class TVUIOrderIcon_RefreshIconElements
{
    private static void Prefix(ref bool isRecipeTimeConstrained)
    {
        if (DinnerRushMode.Current != null)
        {
            isRecipeTimeConstrained = true;
        }
    }
}

/// <summary>Anti-exploit: hide the cancel cross on TV orders during a run (SetData shows it for sandbox orders).</summary>
[HarmonyPatch(typeof(TVUIOrderIcon), nameof(TVUIOrderIcon.SetData))]
internal static class TVUIOrderIcon_SetData
{
    private static readonly AccessTools.FieldRef<TVUIOrderIcon, GameObject> RedCrossRef =
        AccessTools.FieldRefAccess<TVUIOrderIcon, GameObject>("redCross");

    private static void Postfix(TVUIOrderIcon __instance)
    {
        if (DinnerRushMode.Current != null)
        {
            RedCrossRef(__instance)?.SetActive(false);
        }
    }
}

/// <summary>Anti-exploit: a cancelled order would dodge its timeout, so cancelling does nothing during a run.</summary>
[HarmonyPatch(typeof(TVUIOrderIcon), nameof(TVUIOrderIcon.CancelRecipe))]
internal static class TVUIOrderIcon_CancelRecipe
{
    private static bool Prefix() => DinnerRushMode.Current == null;
}
