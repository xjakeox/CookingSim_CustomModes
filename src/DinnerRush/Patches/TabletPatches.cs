using CS.GUI.ScreenUI;
using HarmonyLib;
using UnityEngine.UI;

namespace DinnerRush.Patches;

/// <summary>
/// The serving tablet hides each order's countdown ribbon in sandbox (orders are "not time constrained").
/// During Dinner Rush show it, as in career, so the player can serve the oldest order first.
/// Its Update keeps writing the remaining time, so re-enabling the ribbon is enough.
/// </summary>
[HarmonyPatch(typeof(TabletGUIRecipeChoiceButton), nameof(TabletGUIRecipeChoiceButton.SetData))]
internal static class TabletGUIRecipeChoiceButton_SetData
{
    private static readonly AccessTools.FieldRef<TabletGUIRecipeChoiceButton, Image> RibbonRef =
        AccessTools.FieldRefAccess<TabletGUIRecipeChoiceButton, Image>("ribbon");

    private static void Postfix(TabletGUIRecipeChoiceButton __instance)
    {
        if (DinnerRushMode.Current == null)
        {
            return;
        }
        Image ribbon = RibbonRef(__instance);
        if (ribbon != null)
        {
            ribbon.gameObject.SetActive(true);
        }
    }
}
