using DinnerRush.MenuPicker;
using HarmonyLib;

namespace DinnerRush.Patches;

/// <summary>
/// Our modals aren't GUIGenericWindows, so the game would open the pause menu (behind them) on Esc.
/// Block it while the menu picker or the results popup is open.
/// </summary>
[HarmonyPatch(typeof(KitchenInputService), nameof(KitchenInputService.TryOpenPauseMenu))]
internal static class KitchenInputService_TryOpenPauseMenu
{
    private static bool Prefix() => !MenuPickerScreen.IsOpen && !ResultsScreen.IsOpen;
}
