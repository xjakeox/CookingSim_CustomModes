using System;
using UnityEngine;

namespace CustomModes.API;

/// <summary>
/// A game mode shown in the "Custom Game Modes" popup on the sandbox screen.
/// Register it with <see cref="CustomModesApi.RegisterMode"/> from your plugin's Awake.
/// </summary>
public sealed class CustomModeDefinition
{
    /// <summary>Unique, stable id (e.g. "dinnerrush"). Required.</summary>
    public string Id { get; set; }

    /// <summary>Name shown in the popup. Falls back to <see cref="Id"/>.</summary>
    public string DisplayName { get; set; }

    /// <summary>One or two sentences shown under the name.</summary>
    public string Description { get; set; }

    /// <summary>Optional icon shown beside the name.</summary>
    public Sprite Icon { get; set; }

    /// <summary>Lower values are listed first. Ties sort by display name.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Optional. Returns whether the mode can be played in the given kitchen.
    /// Null means always available. Called every time the popup opens.
    /// </summary>
    public Func<KitchenContext, bool> IsAvailable { get; set; }

    /// <summary>
    /// Required. Called once the sandbox kitchen has fully loaded
    /// (scene shown, products unlocked, <c>SandboxManager.Initialize</c> done).
    /// </summary>
    public Action<KitchenContext> OnSelected { get; set; }

    internal string Name => string.IsNullOrEmpty(DisplayName) ? Id : DisplayName;

    internal bool CheckAvailable(KitchenContext context)
    {
        if (IsAvailable == null)
        {
            return true;
        }
        try
        {
            return IsAvailable(context);
        }
        catch (Exception e)
        {
            Log.Error($"IsAvailable for mode '{Id}' threw: {e}");
            return false;
        }
    }
}
