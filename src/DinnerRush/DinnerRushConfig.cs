using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace DinnerRush;

/// <summary>All tunables, bound to BepInEx\config\cookingsim_dinnerrush.cfg.</summary>
internal static class DinnerRushConfig
{
    internal static ConfigEntry<float> PrepSeconds { get; private set; }
    internal static ConfigEntry<float> FirstOrderDelaySeconds { get; private set; }
    internal static ConfigEntry<float> SecondOrderDelaySeconds { get; private set; }
    internal static ConfigEntry<float> GapDecayPerOrder { get; private set; }
    internal static ConfigEntry<float> MinGapSeconds { get; private set; }
    internal static ConfigEntry<int> MaxActiveOrders { get; private set; }
    internal static ConfigEntry<int> MaxSameDishInARow { get; private set; }
    internal static ConfigEntry<int> StrikesToLose { get; private set; }
    internal static ConfigEntry<float> StrikeAtOrBelowStars { get; private set; }
    internal static ConfigEntry<float> MultiplierPerDifficultyPoint { get; private set; }
    internal static ConfigEntry<int> MenuChoices { get; private set; }
    internal static ConfigEntry<int> MenuSize { get; private set; }
    internal static ConfigEntry<bool> UseWorkloadDifficulty { get; private set; }
    internal static ConfigEntry<bool> DebugMode { get; private set; }
    internal static ConfigEntry<bool> ShowDebugHud { get; private set; }
    internal static ConfigEntry<Key> DebugRushOrderKey { get; private set; }
    internal static ConfigEntry<Key> DebugGameOverKey { get; private set; }
    internal static ConfigEntry<Key> DebugSpawnOrderKey { get; private set; }

    internal static bool DebugHudActive => DebugMode.Value && ShowDebugHud.Value;

    internal static void Bind(ConfigFile config)
    {
        PrepSeconds = config.Bind("Timing", "PrepSeconds", 600f,
            new ConfigDescription("Length of the prep phase before orders start.", new AcceptableValueRange<float>(5f, 3600f)));

        FirstOrderDelaySeconds = config.Bind("Orders", "FirstOrderDelaySeconds", 30f,
            new ConfigDescription("Seconds after service starts before the first order.", new AcceptableValueRange<float>(0f, 600f)));
        SecondOrderDelaySeconds = config.Bind("Orders", "SecondOrderDelaySeconds", 90f,
            new ConfigDescription("Gap between the first and second order.", new AcceptableValueRange<float>(1f, 600f)));
        GapDecayPerOrder = config.Bind("Orders", "GapDecayPerOrder", 0.05f,
            new ConfigDescription("Each following gap shrinks by this fraction (0.05 = 5%).", new AcceptableValueRange<float>(0f, 0.9f)));
        MinGapSeconds = config.Bind("Orders", "MinGapSeconds", 15f,
            new ConfigDescription("The gap never drops below this.", new AcceptableValueRange<float>(1f, 600f)));
        MaxActiveOrders = config.Bind("Orders", "MaxActiveOrders", 0,
            new ConfigDescription("Hold new orders while this many are open. 0 = no limit.", new AcceptableValueRange<int>(0, 50)));
        MaxSameDishInARow = config.Bind("Orders", "MaxSameDishInARow", 2,
            new ConfigDescription("Never order the same dish more than this many times in a row. 0 = no rule.", new AcceptableValueRange<int>(0, 10)));

        StrikesToLose = config.Bind("Scoring", "StrikesToLose", 3,
            new ConfigDescription("The run ends on this strike (3 = two strikes are fine, the third ends it). 0 = strikes never end the run.", new AcceptableValueRange<int>(0, 10)));
        StrikeAtOrBelowStars = config.Bind("Scoring", "StrikeAtOrBelowStars", 3f,
            new ConfigDescription("A served dish rated this many stars or fewer is a strike. Half stars count (e.g. 2.5).", new AcceptableValueRange<float>(0f, 5f)));
        MultiplierPerDifficultyPoint = config.Bind("Scoring", "MultiplierPerDifficultyPoint", 0.25f,
            new ConfigDescription("Final score = stars x multiplier. The easiest menu (difficulty 3) is 1x; each difficulty point above adds this (0.25: difficulty 9 = 2.5x). 0 = no bonus.", new AcceptableValueRange<float>(0f, 2f)));

        MenuChoices = config.Bind("Menu", "MenuChoices", 10,
            new ConfigDescription("Dishes offered in the menu picker.", new AcceptableValueRange<int>(3, 20)));
        MenuSize = config.Bind("Menu", "MenuSize", 3,
            new ConfigDescription("Dishes the player must pick.", new AcceptableValueRange<int>(1, 10)));
        UseWorkloadDifficulty = config.Bind("Menu", "UseWorkloadDifficulty", false,
            "Add each dish's workload intensity (normalized 0-1 across the pool) to the menu difficulty score.");

        DebugMode = config.Bind("Debug", "DebugMode", false,
            "Master switch for testing aids: the debug hotkeys below, the debug HUD line, and UI layout logging. Runs that use a debug key are not saved as high scores.");
        ShowDebugHud = config.Bind("Debug", "ShowDebugHud", false,
            "With DebugMode on: show an extra HUD line with internal state (phase, time scale, pause flags, orders).");
        // Unity Input System key names (the game has the legacy input API disabled, so BepInEx KeyboardShortcut can't see keys).
        DebugRushOrderKey = config.Bind("Debug", "RushOrderKey", Key.F7,
            "DebugMode only: set the most urgent open order to 10 seconds left (prep is skipped from the TV).");
        DebugGameOverKey = config.Bind("Debug", "ForceGameOverKey", Key.F8,
            "DebugMode only: end the run immediately.");
        DebugSpawnOrderKey = config.Bind("Debug", "SpawnOrderKey", Key.F6,
            "DebugMode only: start an extra order right now (service only). The normal schedule continues.");
    }
}
