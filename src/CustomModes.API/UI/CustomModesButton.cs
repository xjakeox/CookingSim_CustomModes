using System.Collections.Generic;
using System.Linq;
using CS.GUI;
using CS.GUI.GameModes;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CustomModes.API.UI;

/// <summary>
/// The "Custom Game Modes" button, cloned from the sandbox Play button so it keeps the game's look.
/// </summary>
internal sealed class CustomModesButton : MonoBehaviour
{
    internal const string Label = "Custom Game Modes";
    private const float FallbackSpacing = 12f;

    private static readonly AccessTools.FieldRef<GUIGameMode, TextButton> PlayButtonRef =
        AccessTools.FieldRefAccess<GUIGameMode, TextButton>("playButton");

    private static readonly AccessTools.FieldRef<GUISandboxGameMode, GUIKitchenSceneSelection> SceneSelectionRef =
        AccessTools.FieldRefAccess<GUISandboxGameMode, GUIKitchenSceneSelection>("sceneSelection");

    private static readonly List<CustomModesButton> Instances = new List<CustomModesButton>();

    private static bool hierarchyDumped;

    private GUISandboxGameMode owner;

    internal TextButton Button { get; private set; }

    internal static TextButton GetPlayButton(GUIGameMode mode) => PlayButtonRef(mode);

    /// <summary>Raw kitchen id, exactly what GUISandboxGameMode.Play passes to StartBaseSandbox.</summary>
    internal static string GetSelectedKitchenId(GUISandboxGameMode sandbox) => SceneSelectionRef(sandbox).SelectedKitchenID;

    internal static void Attach(GUISandboxGameMode sandbox)
    {
        if (Find(sandbox) != null)
        {
            return;
        }

        TextButton play = GetPlayButton(sandbox);
        if (play == null)
        {
            Log.Error("Sandbox Play button not found; Custom Game Modes button not added.");
            return;
        }

        if (CustomModesPlugin.DumpUiHierarchy.Value && !hierarchyDumped)
        {
            hierarchyDumped = true;
            HierarchyDumper.Dump("Sandbox game mode", sandbox.transform, 8);
            HierarchyDumper.DumpAncestors("Play button", play.transform);
        }

        GameObject clone = Instantiate(play.gameObject, play.transform.parent, false);
        clone.name = "CustomModesButton";
        clone.transform.SetSiblingIndex(play.transform.GetSiblingIndex());

        var component = clone.AddComponent<CustomModesButton>();
        component.owner = sandbox;
        component.Button = clone.GetComponent<TextButton>();
        Instances.Add(component);

        // Instantiate copies persistent (inspector) listeners too, so replace the event instead of clearing it.
        component.Button.onClick = new Button.ButtonClickedEvent();
        component.Button.onClick.AddListener(() => CustomModesPopup.Open(sandbox));
        SetLabel(component.Button, Label);

        // The Play button may carry a gamepad glyph; it would be misleading on our button.
        foreach (GUIStaticInputTutorial glyph in clone.GetComponentsInChildren<GUIStaticInputTutorial>(true))
        {
            glyph.gameObject.SetActive(false);
        }

        PlaceAbove(clone.GetComponent<RectTransform>(), play.GetComponent<RectTransform>());
        Log.Info($"Custom Game Modes button added under '{HierarchyDumper.PathOf(play.transform.parent)}'.");
        Refresh(sandbox);
    }

    internal static void Refresh(GUISandboxGameMode sandbox)
    {
        CustomModesButton component = Find(sandbox);
        if (component == null)
        {
            return;
        }

        // The Play button lives in "Base Elements", shared by every game mode, so only show ours while
        // the sandbox entry is the selected mode. v1.0 also supports base-game sandbox kitchens only.
        bool isCurrentMode = GUIMenu.Me != null && GUIMenu.Me.GameModesUI != null && GUIMenu.Me.GameModesUI.CurrentGameMode == sandbox;
        bool supported = !sandbox.DlcIDToGameStateBindsCollection.ContainsKey(sandbox.SelectedKitchenId);
        bool visible = isCurrentMode && supported;
        component.gameObject.SetActive(visible);
        if (visible)
        {
            var context = new KitchenContext(GetSelectedKitchenId(sandbox));
            bool anyAvailable = CustomModesApi.Modes.Any(m => m.CheckAvailable(context));
            component.Button.interactable = anyAvailable;
            SetLabel(component.Button, anyAvailable ? Label : Label + " (none available)");
            component.Button.Refresh();
        }
        LinkNavigation(sandbox);
    }

    /// <summary>Hides the button whenever a game mode other than its sandbox owner is shown.</summary>
    internal static void OnModeShown(GUIGameMode mode)
    {
        Instances.RemoveAll(b => b == null);
        foreach (CustomModesButton button in Instances)
        {
            if (button.owner != mode)
            {
                button.gameObject.SetActive(false);
            }
        }
    }

    internal static void Hide(GUISandboxGameMode sandbox)
    {
        CustomModesButton component = Find(sandbox);
        if (component != null)
        {
            component.gameObject.SetActive(false);
            LinkNavigation(sandbox);
        }
    }

    private void OnDestroy() => Instances.Remove(this);

    /// <summary>Inserts our button directly above Play in the explicit gamepad navigation chain built by SetNavigation.</summary>
    internal static void LinkNavigation(GUISandboxGameMode sandbox)
    {
        CustomModesButton component = Find(sandbox);
        TextButton play = GetPlayButton(sandbox);
        if (component == null || play == null)
        {
            return;
        }

        Selectable ours = component.Button;
        Navigation playNav = play.navigation;
        Selectable above = playNav.selectOnUp == ours ? ours.navigation.selectOnUp : playNav.selectOnUp;
        bool active = component.gameObject.activeInHierarchy && ours.interactable;

        Navigation ourNav = ours.navigation;
        ourNav.mode = Navigation.Mode.Explicit;
        ourNav.selectOnUp = above;
        ourNav.selectOnDown = play;
        ourNav.selectOnLeft = null;
        ourNav.selectOnRight = null;
        ours.navigation = ourNav;

        Selectable target = active ? ours : play;
        playNav.selectOnUp = active ? ours : above;
        play.navigation = playNav;
        if (above != null)
        {
            Navigation aboveNav = above.navigation;
            aboveNav.selectOnDown = target;
            above.navigation = aboveNav;
        }
    }

    internal static void Focus(GUISandboxGameMode sandbox)
    {
        CustomModesButton component = Find(sandbox);
        if (component != null && component.gameObject.activeInHierarchy && Game.Input.IsUsingGamepad)
        {
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(component.gameObject);
        }
    }

    internal static void SetLabel(TextButton button, string text)
    {
        Text label = button.Text;
        if (label == null)
        {
            label = button.GetComponentInChildren<Text>(true);
        }
        if (label is AutoLocalizedText localized)
        {
            // Our strings have no localization keys; show the raw text.
            localized.LocalizeText = false;
            localized.text = text;
        }
        else if (label != null)
        {
            label.text = text;
        }
    }

    private static CustomModesButton Find(GUISandboxGameMode sandbox)
    {
        TextButton play = GetPlayButton(sandbox);
        if (play == null || play.transform.parent == null)
        {
            return null;
        }
        return play.transform.parent.GetComponentsInChildren<CustomModesButton>(true).FirstOrDefault(b => b.owner == sandbox);
    }

    private static void PlaceAbove(RectTransform clone, RectTransform play)
    {
        Transform parent = play.parent;
        if (parent.GetComponent<LayoutGroup>() != null)
        {
            Log.Info($"Play button parent uses {parent.GetComponent<LayoutGroup>().GetType().Name}; layout handles placement.");
            return;
        }
        // Manually positioned: stack our button above Play.
        clone.anchoredPosition = play.anchoredPosition + new Vector2(0f, play.rect.height + FallbackSpacing);
        Log.Info($"Play button parent has no LayoutGroup; offset button by {play.rect.height + FallbackSpacing}px.");
    }
}
