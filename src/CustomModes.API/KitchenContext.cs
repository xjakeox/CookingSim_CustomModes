namespace CustomModes.API;

/// <summary>
/// Describes the kitchen a custom mode is being offered for or started in.
/// </summary>
public sealed class KitchenContext
{
    public KitchenContext(string kitchenId)
    {
        KitchenId = kitchenId;
    }

    /// <summary>
    /// Kitchen id as passed to <c>GameManager.StartBaseSandbox</c> (the sandbox screen's selected kitchen).
    /// </summary>
    public string KitchenId { get; }

    public override string ToString() => KitchenId;
}
