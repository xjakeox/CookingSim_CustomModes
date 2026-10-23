using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace CustomModes.API;

[BepInPlugin(CustomModesApi.PluginGuid, "Custom Modes API", "1.0.0")]
public sealed class CustomModesPlugin : BaseUnityPlugin
{
    private const string ConfigFileName = "cookingsim_custommodes_api.cfg";

    internal static ConfigEntry<bool> DumpUiHierarchy { get; private set; }

    private void Awake()
    {
        Log.Source = Logger;
        ConfigFile config = OpenConfig();
        DumpUiHierarchy = config.Bind("Debug", "LogUiHierarchy", false,
            "Log the sandbox menu's UI hierarchy the first time it opens (used to tune button placement).");

        // Patch class by class so one broken target (e.g. after a game update) doesn't disable the rest.
        var harmony = new Harmony(CustomModesApi.PluginGuid);
        foreach (System.Type type in AccessTools.GetTypesFromAssembly(typeof(CustomModesPlugin).Assembly))
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
            {
                continue;
            }
            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (System.Exception e)
            {
                Log.Error($"Failed to apply patch {type.Name}: {e}");
            }
        }
        Log.Info("Custom Modes API loaded.");
    }

    /// <summary>
    /// Our own file name instead of BepInEx's default "&lt;GUID&gt;.cfg". Settings saved under the old name are carried over once.
    /// </summary>
    private ConfigFile OpenConfig()
    {
        string path = Path.Combine(Paths.ConfigPath, ConfigFileName);
        string legacy = Path.Combine(Paths.ConfigPath, CustomModesApi.PluginGuid + ".cfg");
        if (!File.Exists(path) && File.Exists(legacy))
        {
            try
            {
                File.Move(legacy, path);
            }
            catch (System.Exception e)
            {
                Log.Warning($"Could not carry over {legacy}: {e.Message}");
            }
        }
        return new ConfigFile(path, true, Info.Metadata);
    }
}
