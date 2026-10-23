using CS.GUI.TV;
using DinnerRush.Tv;
using HarmonyLib;

namespace DinnerRush.Patches;

// All patches are no-ops unless a Dinner Rush run is in progress.

/// <summary>During a run, show our clock (prep countdown / service elapsed) instead of the frozen sandbox clock.</summary>
[HarmonyPatch(typeof(TVUIDaySkipping), "Update")]
internal static class TVUIDaySkipping_Update
{
    private static bool Prefix(TVUIDaySkipping __instance)
    {
        if (!TvPanel.Active)
        {
            return true;
        }
        TvPanel.WriteTime(__instance);
        return false;
    }
}

/// <summary>Never let the career skip touch the frozen sandbox clock during a run (e.g. via an inspector-wired listener).</summary>
[HarmonyPatch(typeof(TVUIDaySkipping), nameof(TVUIDaySkipping.SkipFirstPhase))]
internal static class TVUIDaySkipping_SkipFirstPhase
{
    private static bool Prefix() => DinnerRushMode.Current == null;
}

/// <summary>The game hides the skip panel in sandbox whenever it re-evaluates it; put ours back during a run.</summary>
[HarmonyPatch(typeof(TVUI), "ToggleSkipTime")]
internal static class TVUI_ToggleSkipTime
{
    private static void Postfix(TVUI __instance)
    {
        if (TvPanel.Active)
        {
            TvPanel.ShowOn(__instance);
        }
    }
}
