using System;
using System.Collections.Generic;
using System.Linq;
using WIUtils;

namespace CustomModes.API;

/// <summary>
/// Public entry point for mods that add game modes. Keep this surface small and stable:
/// other plugins compile against it.
/// </summary>
public static class CustomModesApi
{
    public const string PluginGuid = "com.viper.custommodes.api";

    private static readonly object Gate = new object();
    private static readonly List<CustomModeDefinition> Registered = new List<CustomModeDefinition>();
    private static GameManager subscribedManager;

    /// <summary>Raised after a custom mode's <see cref="CustomModeDefinition.OnSelected"/> has run.</summary>
    public static event Action<CustomModeDefinition, KitchenContext> OnModeStarted;

    /// <summary>Raised when the player leaves a custom mode (back to the main menu, or the game state changes).</summary>
    public static event Action<CustomModeDefinition> OnModeEnded;

    /// <summary>Registered modes, sorted by <see cref="CustomModeDefinition.SortOrder"/> then name.</summary>
    public static IReadOnlyList<CustomModeDefinition> Modes
    {
        get
        {
            lock (Gate)
            {
                return Registered
                    .OrderBy(m => m.SortOrder)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
    }

    /// <summary>Id of the running custom mode, or null outside one. Set before the kitchen starts loading.</summary>
    public static string ActiveModeId => ActiveMode?.Id;

    public static CustomModeDefinition ActiveMode { get; private set; }

    public static KitchenContext ActiveContext { get; private set; }

    public static bool IsModeActive(string id) => id != null && ActiveModeId == id;

    /// <summary>
    /// Adds a mode to the popup. Safe to call at any time; a duplicate Id logs a warning and is ignored.
    /// Returns true if the mode was added.
    /// </summary>
    public static bool RegisterMode(CustomModeDefinition definition)
    {
        if (definition == null)
        {
            Log.Warning("RegisterMode called with null definition; ignored.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            Log.Warning($"RegisterMode: mode '{definition.DisplayName}' has no Id; ignored.");
            return false;
        }
        if (definition.OnSelected == null)
        {
            Log.Warning($"RegisterMode: mode '{definition.Id}' has no OnSelected callback; ignored.");
            return false;
        }
        lock (Gate)
        {
            if (Registered.Any(m => string.Equals(m.Id, definition.Id, StringComparison.OrdinalIgnoreCase)))
            {
                Log.Warning($"RegisterMode: a mode with Id '{definition.Id}' is already registered; ignored.");
                return false;
            }
            Registered.Add(definition);
        }
        Log.Info($"Registered custom mode '{definition.Id}' ({definition.Name}).");
        return true;
    }

    public static CustomModeDefinition GetMode(string id)
    {
        lock (Gate)
        {
            return Registered.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Starts a registered mode in a base-sandbox kitchen, exactly as if it were picked from the popup.
    /// Intended for "play again" flows: call it from the main menu (e.g. in GUIKitchen.QuitToMenu's onComplete).
    /// Returns false if the mode is unknown or not available for that kitchen.
    /// </summary>
    public static bool StartMode(string modeId, KitchenContext context)
    {
        CustomModeDefinition definition = GetMode(modeId);
        if (definition == null || context == null || !definition.CheckAvailable(context))
        {
            Log.Warning($"StartMode: mode '{modeId}' is not registered or not available for kitchen '{context}'.");
            return false;
        }
        StartMode(definition, context);
        return true;
    }

    internal static void StartMode(CustomModeDefinition definition, KitchenContext context)
    {
        if (ActiveMode != null)
        {
            EndActiveMode();
        }

        GameManager manager = Singleton<GameManager>.Me;
        if (manager == null)
        {
            Log.Error("StartMode: GameManager is not available.");
            return;
        }

        // Must be set before StartBaseSandbox so patches can tell this is a custom run.
        ActiveMode = definition;
        ActiveContext = context;
        Subscribe(manager);
        Log.Info($"Starting custom mode '{definition.Id}' in kitchen '{context.KitchenId}'.");

        manager.StartBaseSandbox(context.KitchenId, () =>
        {
            if (ActiveMode != definition)
            {
                return;
            }
            try
            {
                definition.OnSelected(context);
            }
            catch (Exception e)
            {
                Log.Error($"OnSelected for mode '{definition.Id}' threw: {e}");
            }
            Raise(OnModeStarted, "OnModeStarted", definition, context);
        });
    }

    internal static void EndActiveMode()
    {
        CustomModeDefinition ended = ActiveMode;
        if (ended == null)
        {
            return;
        }
        ActiveMode = null;
        ActiveContext = null;
        Unsubscribe();
        Log.Info($"Custom mode '{ended.Id}' ended.");
        Raise(OnModeEnded, "OnModeEnded", ended);
    }

    private static void Subscribe(GameManager manager)
    {
        Unsubscribe();
        subscribedManager = manager;
        manager.GameStateChanged += OnGameStateChanged;
    }

    private static void Unsubscribe()
    {
        if (subscribedManager != null)
        {
            subscribedManager.GameStateChanged -= OnGameStateChanged;
            subscribedManager = null;
        }
    }

    private static void OnGameStateChanged()
    {
        // StartBaseSandbox sets GameState = Sandbox while loading; anything else (Menu, career, DLC) ends the run.
        if (subscribedManager == null || subscribedManager.GameState != GameState.Sandbox)
        {
            EndActiveMode();
        }
    }

    // Invokes each subscriber separately so one failing mod cannot break the others.
    private static void Raise(Delegate handlers, string eventName, params object[] args)
    {
        if (handlers == null)
        {
            return;
        }
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                handler.DynamicInvoke(args);
            }
            catch (Exception e)
            {
                Log.Error($"{eventName} handler threw: {e.InnerException ?? e}");
            }
        }
    }
}
