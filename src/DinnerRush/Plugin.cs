using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CustomModes.API;
using HarmonyLib;

namespace DinnerRush;

[BepInPlugin(PluginGuid, "Dinner Rush", "0.1.0")]
[BepInDependency(CustomModesApi.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.viper.dinnerrush";
    public const string ModeId = "dinnerrush";
    private const string ConfigFileName = "cookingsim_dinnerrush.cfg";

    internal static ManualLogSource Log { get; private set; }

    private void Awake()
    {
        Log = Logger;
        DinnerRushConfig.Bind(OpenConfig());
        ApplyPatches();

        CustomModesApi.RegisterMode(new CustomModeDefinition
        {
            Id = ModeId,
            DisplayName = "Dinner Rush",
            Description = "Pick 3 dishes, prep for 10 minutes, then survive an ever-faster stream of orders. One expired order ends the run.",
            SortOrder = 0,
            OnSelected = DinnerRushMode.Begin,
        });
        CustomModesApi.OnModeEnded += mode =>
        {
            if (mode.Id == ModeId)
            {
                DinnerRushMode.End();
            }
        };
        Logger.LogInfo("Dinner Rush loaded.");
    }

    /// <summary>
    /// Our own file name instead of BepInEx's default "&lt;GUID&gt;.cfg". Settings saved under the old name are carried over once.
    /// </summary>
    private ConfigFile OpenConfig()
    {
        string path = Path.Combine(Paths.ConfigPath, ConfigFileName);
        string legacy = Path.Combine(Paths.ConfigPath, PluginGuid + ".cfg");
        if (!File.Exists(path) && File.Exists(legacy))
        {
            try
            {
                File.Move(legacy, path);
            }
            catch (System.Exception e)
            {
                Logger.LogWarning($"Could not carry over {legacy}: {e.Message}");
            }
        }
        return new ConfigFile(path, true, Info.Metadata);
    }

    // Patch class by class so one broken target (e.g. after a game update) doesn't disable the rest.
    private static void ApplyPatches()
    {
        var harmony = new Harmony(PluginGuid);
        foreach (System.Type type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
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
                Log.LogError($"Failed to apply patch {type.Name}: {e}");
            }
        }
    }
}
