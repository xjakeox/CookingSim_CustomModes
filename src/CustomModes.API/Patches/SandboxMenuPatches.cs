using System;
using CS.GUI.GameModes;
using CustomModes.API.UI;
using HarmonyLib;

namespace CustomModes.API.Patches;

// All patches here only touch the sandbox entry of the game modes window, and only add our own button.

[HarmonyPatch(typeof(GUISandboxGameMode), nameof(GUISandboxGameMode.Init), new Type[0])]
internal static class GUISandboxGameMode_Init
{
    private static void Postfix(GUISandboxGameMode __instance) => Guard(() => CustomModesButton.Attach(__instance));

    internal static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Error($"Custom Modes UI error: {e}");
        }
    }
}

// Covers both opening the sandbox entry and KitchenSelectionOnValueChanged, which calls base.Show().
[HarmonyPatch(typeof(GUIDLCGameMode), nameof(GUIDLCGameMode.Show))]
internal static class GUIDLCGameMode_Show
{
    private static void Postfix(GUIDLCGameMode __instance)
    {
        if (__instance is GUISandboxGameMode sandbox)
        {
            GUISandboxGameMode_Init.Guard(() => CustomModesButton.Refresh(sandbox));
        }
    }
}

// GUISandboxGameMode.Show restores the saved kitchen selection after base.Show(), so refresh again.
[HarmonyPatch(typeof(GUISandboxGameMode), nameof(GUISandboxGameMode.Show))]
internal static class GUISandboxGameMode_Show
{
    private static void Postfix(GUISandboxGameMode __instance) => GUISandboxGameMode_Init.Guard(() => CustomModesButton.Refresh(__instance));
}

[HarmonyPatch(typeof(GUISandboxGameMode), "KitchenSelectionOnValueChanged")]
internal static class GUISandboxGameMode_KitchenSelectionOnValueChanged
{
    private static void Postfix(GUISandboxGameMode __instance) => GUISandboxGameMode_Init.Guard(() => CustomModesButton.Refresh(__instance));
}

[HarmonyPatch(typeof(GUISandboxGameMode), nameof(GUISandboxGameMode.Hide))]
internal static class GUISandboxGameMode_Hide
{
    private static void Postfix(GUISandboxGameMode __instance) => GUISandboxGameMode_Init.Guard(() =>
    {
        CustomModesPopup.Close();
        CustomModesButton.Hide(__instance);
    });
}

// Every mode's Show runs through GUIGameMode.Show; when it is not the sandbox, our button must go.
[HarmonyPatch(typeof(GUIGameMode), nameof(GUIGameMode.Show))]
internal static class GUIGameMode_Show
{
    private static void Postfix(GUIGameMode __instance)
    {
        if (!(__instance is GUISandboxGameMode))
        {
            GUISandboxGameMode_Init.Guard(() => CustomModesButton.OnModeShown(__instance));
        }
    }
}

[HarmonyPatch(typeof(GUIGameMode), nameof(GUIGameMode.SetNavigation))]
internal static class GUIGameMode_SetNavigation
{
    private static void Postfix(GUIGameMode __instance)
    {
        if (__instance is GUISandboxGameMode sandbox)
        {
            GUISandboxGameMode_Init.Guard(() => CustomModesButton.LinkNavigation(sandbox));
        }
    }
}

// While the popup is open it owns cancel input, so Esc / B closes the popup instead of leaving the window.
[HarmonyPatch(typeof(GUIGameModesWindow), "Update")]
internal static class GUIGameModesWindow_Update
{
    private static bool Prefix()
    {
        if (!CustomModesPopup.IsOpen)
        {
            return true;
        }
        GUISandboxGameMode_Init.Guard(CustomModesPopup.HandleInput);
        return false;
    }
}

[HarmonyPatch(typeof(GUIGameModesWindow), nameof(GUIGameModesWindow.Hide))]
internal static class GUIGameModesWindow_Hide
{
    private static void Postfix() => GUISandboxGameMode_Init.Guard(CustomModesPopup.Close);
}
