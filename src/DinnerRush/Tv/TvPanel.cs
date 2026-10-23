using System.Collections.Generic;
using CS.GUI.TV;
using DinnerRush.Core;
using DinnerRush.Ui;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DinnerRush.Tv;

/// <summary>
/// Reuses the game's career "skip time" panel on every kitchen TV during a Dinner Rush run:
/// in prep its clock counts down and its button asks to start service early;
/// in service its clock shows elapsed service time, the button is disabled, and the bottom bar also shows the
/// score and strike marks (TvScoreBar).
/// The game hides this panel in sandbox and its own clock/skip logic would read the frozen sandbox time,
/// so we show it ourselves, rewire the button, and write the clock (see TvPatches).
/// </summary>
internal static class TvPanel
{
    private const string PrepLabelKey = "time_skip_prep"; // the game's own "skip prep" string
    private const string ServiceLabelKey = "dinnerrush_service_time";
    private const string ServiceLabel = "Service time";

    private static readonly AccessTools.FieldRef<TVUIDaySkipping, Button> SkipButtonRef =
        AccessTools.FieldRefAccess<TVUIDaySkipping, Button>("btnSkipTime");
    private static readonly AccessTools.FieldRef<TVUIDaySkipping, AutoLocalizedText> LabelRef =
        AccessTools.FieldRefAccess<TVUIDaySkipping, AutoLocalizedText>("text");
    private static readonly AccessTools.FieldRef<TVUIDaySkipping, TMP_Text> TimeRef =
        AccessTools.FieldRefAccess<TVUIDaySkipping, TMP_Text>("time");
    private static readonly AccessTools.FieldRef<TVUIDaySkipping, GameObject> PanelRef =
        AccessTools.FieldRefAccess<TVUIDaySkipping, GameObject>("panel");
    private static readonly AccessTools.FieldRef<TVUIDaySkipping, GameObject> ClockRef =
        AccessTools.FieldRefAccess<TVUIDaySkipping, GameObject>("clock");

    // Which phase each panel is currently set up for.
    private static readonly Dictionary<TVUIDaySkipping, RunPhase> Configured = new Dictionary<TVUIDaySkipping, RunPhase>();
    private static bool loggedFailure;

    private static RunPhase? CurrentPhase
    {
        get
        {
            DinnerRushMode mode = DinnerRushMode.Current;
            if (mode == null)
            {
                return null;
            }
            RunPhase phase = mode.Run.Phase;
            return phase == RunPhase.Prep || phase == RunPhase.Service ? phase : (RunPhase?)null;
        }
    }

    /// <summary>True while Dinner Rush is in prep or service, i.e. while we own the TV skip panels.</summary>
    internal static bool Active => CurrentPhase.HasValue;

    /// <summary>Makes sure every TV shows the panel for the current phase. Cheap when already set up; call periodically.</summary>
    internal static void ShowOnAll()
    {
        foreach (TVUI tv in TVUI.GetTVs())
        {
            ShowOn(tv);
        }
    }

    internal static void ShowOn(TVUI tv)
    {
        RunPhase? phase = CurrentPhase;
        if (!phase.HasValue || tv == null || tv.daySkipPanel == null)
        {
            return;
        }
        TVUIDaySkipping skip = tv.daySkipPanel;
        try
        {
            GameObject panel = PanelRef(skip);
            if (Configured.TryGetValue(skip, out RunPhase configuredFor) && configuredFor == phase.Value && panel != null && panel.activeSelf)
            {
                return;
            }

            // Show wires btnSkipTime to the career skip; we replace that listener below.
            skip.Show(DayTimeType.Preparation);
            Button button = SkipButtonRef(skip);
            button.onClick.RemoveAllListeners();
            AutoLocalizedText label = LabelRef(skip);
            if (phase.Value == RunPhase.Prep)
            {
                button.onClick.AddListener(OnSkipClicked);
                button.interactable = true;
                SetLabel(label, PrepLabelKey);
            }
            else
            {
                // Nothing to skip during service; the panel is only a clock.
                button.interactable = false;
                Localization.ReplaceKey(ServiceLabelKey, ServiceLabel);
                SetLabel(label, ServiceLabelKey);
            }

            GameObject clock = ClockRef(skip);
            if (clock != null && !clock.activeSelf)
            {
                clock.SetActive(true);
            }
            Configured[skip] = phase.Value;
            // Score and strike marks sit in the bottom bar during service only.
            if (phase.Value == RunPhase.Service)
            {
                TvScoreBar.Attach(panel, TimeRef(skip), DinnerRushMode.Current.Run.Score.StrikesToLose);
            }
            else
            {
                TvScoreBar.Detach(panel);
            }
            WriteTime(skip);
        }
        catch (System.Exception e)
        {
            if (!loggedFailure)
            {
                loggedFailure = true;
                Plugin.Log.LogError($"Could not set up the Dinner Rush panel on a TV: {e}");
            }
        }
    }

    internal static void HideAll()
    {
        foreach (TVUIDaySkipping skip in Configured.Keys)
        {
            if (skip == null)
            {
                continue;
            }
            Button button = SkipButtonRef(skip);
            if (button != null)
            {
                button.interactable = true;
            }
            TvScoreBar.Detach(PanelRef(skip));
            skip.Hide();
        }
        Configured.Clear();
    }

    /// <summary>Writes the run clock into a panel. Called from the TVUIDaySkipping.Update patch.</summary>
    internal static void WriteTime(TVUIDaySkipping skip)
    {
        TMP_Text time = TimeRef(skip);
        DinnerRushMode mode = DinnerRushMode.Current;
        if (time == null || mode == null)
        {
            return;
        }
        time.text = mode.Run.Phase == RunPhase.Prep
            ? UiKit.FormatCountdown(mode.Run.PrepRemaining)
            : UiKit.FormatElapsed(mode.Run.ServiceElapsed);
    }

    private static void SetLabel(AutoLocalizedText label, string key)
    {
        if (label != null)
        {
            // Keys are resolved through Localization (our own key is registered via ReplaceKey).
            label.LocalizeText = true;
            label.text = key;
        }
    }

    private static void OnSkipClicked()
    {
        DinnerRushMode.Current?.RequestSkipPrep();
    }
}
