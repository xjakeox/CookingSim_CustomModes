using BepInEx.Logging;

namespace CustomModes.API;

internal static class Log
{
    internal static ManualLogSource Source { get; set; }

    internal static void Info(string message) => Source?.LogInfo(message);
    internal static void Warning(string message) => Source?.LogWarning(message);
    internal static void Error(string message) => Source?.LogError(message);
}
