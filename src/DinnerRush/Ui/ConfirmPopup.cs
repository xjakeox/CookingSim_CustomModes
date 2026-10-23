using System;
using CS.GUI;

namespace DinnerRush.Ui;

/// <summary>
/// Yes/No confirmation using the game's own popup (GUIKitchen.defaultPopup).
/// The popup localizes every string, so our text is registered as localization replacements first.
/// </summary>
internal static class ConfirmPopup
{
    private const string KeyPrefix = "dinnerrush_";

    internal static bool IsOpen { get; private set; }

    /// <summary>Returns false if the game's popup is unavailable, so the caller can fall back.</summary>
    internal static bool Show(string id, string message, Action onYes, Action onNo)
    {
        var popup = GUIKitchen.Me != null ? GUIKitchen.Me.defaultPopup : null;
        if (popup == null)
        {
            return false;
        }

        string key = KeyPrefix + id;
        Localization.ReplaceKey(key, message);
        IsOpen = true;
        // The TV is used with a locked cursor; free it so the popup buttons can be clicked.
        Game.KitchenServices.KitchenInput.CursorLocked = false;
        Game.KitchenServices.KitchenInput.CursorVisible = true;

        popup.Show("", key, Localization.Get("menu_yes"), Localization.Get("menu_no"),
            () => Close(onYes),
            () => Close(onNo));
        return true;
    }

    /// <summary>Closes the popup without running either callback (run ended, back to menu).</summary>
    internal static void Dismiss()
    {
        if (IsOpen)
        {
            Close(null);
        }
    }

    private static void Close(Action then)
    {
        IsOpen = false;
        if (GUIKitchen.Me != null && GUIKitchen.Me.defaultPopup != null)
        {
            GUIKitchen.Me.defaultPopup.Hide();
        }
        // Restore whatever cursor state the game's open windows call for.
        GUIGenericWindow.CheckCursor();
        then?.Invoke();
    }
}
