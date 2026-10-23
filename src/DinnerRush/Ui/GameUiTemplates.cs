using System.Linq;
using System.Text;
using CS.GUI.ScreenUI;
using CS.GUI.TV;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DinnerRush.Ui;

/// <summary>
/// Finds the kitchen computer's own UI pieces (the sandbox recipe card prefab and the recipe details view)
/// so our screens can clone them and look exactly like the game. Every lookup may fail; callers fall back.
/// </summary>
internal static class GameUiTemplates
{
    private static readonly AccessTools.FieldRef<ScreenGUISandboxRecipeStarter, SandboxRecipeButton> CardPrefabRef =
        AccessTools.FieldRefAccess<ScreenGUISandboxRecipeStarter, SandboxRecipeButton>("prefabRecipeButton");
    private static readonly AccessTools.FieldRef<ScreenGUISandboxDetailsView, TVUIRecipeInfo> RecipeInfoRef =
        AccessTools.FieldRefAccess<ScreenGUISandboxDetailsView, TVUIRecipeInfo>("recipeInfo");
    private static readonly AccessTools.FieldRef<SandboxRecipeButton, Button> CardOrderButtonRef =
        AccessTools.FieldRefAccess<SandboxRecipeButton, Button>("btnStartRecipe");
    private static readonly AccessTools.FieldRef<SandboxRecipeButton, Button> CardInfoButtonRef =
        AccessTools.FieldRefAccess<SandboxRecipeButton, Button>("btnShowInfo");
    private static readonly AccessTools.FieldRef<SandboxRecipeButton, RectTransform> CardNamePanelRef =
        AccessTools.FieldRefAccess<SandboxRecipeButton, RectTransform>("rtNamePanel");
    private static readonly AccessTools.FieldRef<TVUIRecipeInfo, GameObject> PinObjectRef =
        AccessTools.FieldRefAccess<TVUIRecipeInfo, GameObject>("togglePinRecipeObject");

    private static readonly System.Collections.Generic.HashSet<string> Dumped = new System.Collections.Generic.HashSet<string>();

    /// <summary>The computer's sandbox recipe card prefab, or null.</summary>
    internal static SandboxRecipeButton FindCardPrefab()
    {
        ScreenGUISandboxRecipeStarter starter = Resources.FindObjectsOfTypeAll<ScreenGUISandboxRecipeStarter>()
            .FirstOrDefault(s => s != null && s.gameObject.scene.IsValid());
        return starter != null ? CardPrefabRef(starter) : null;
    }

    /// <summary>The computer's recipe details view (the same component the TVs use), or null.</summary>
    internal static TVUIRecipeInfo FindRecipeInfoTemplate()
    {
        ScreenGUISandboxDetailsView details = Resources.FindObjectsOfTypeAll<ScreenGUISandboxDetailsView>()
            .FirstOrDefault(d => d != null && d.gameObject.scene.IsValid());
        return details != null ? RecipeInfoRef(details) : null;
    }

    internal static Button CardOrderButton(SandboxRecipeButton card) => CardOrderButtonRef(card);
    internal static Button CardInfoButton(SandboxRecipeButton card) => CardInfoButtonRef(card);
    internal static RectTransform CardNamePanel(SandboxRecipeButton card) => CardNamePanelRef(card);

    /// <summary>
    /// Hides the details view's action controls: pin/buy act on the real TVs and recipe unlocks,
    /// and "Button start" (the computer's Order button) would start an order.
    /// </summary>
    internal static void HideRecipeInfoActions(TVUIRecipeInfo info)
    {
        GameObject pin = PinObjectRef(info);
        if (pin != null)
        {
            pin.SetActive(false);
        }
        foreach (BuyRecipeButton buy in info.GetComponentsInChildren<BuyRecipeButton>(true))
        {
            buy.gameObject.SetActive(false);
        }
        foreach (Button button in info.GetComponentsInChildren<Button>(true))
        {
            if (button.name == "Button start")
            {
                button.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// A clone keeps inspector-wired click handlers that point at objects outside it (the real computer/TV).
    /// Neutralize every such button so clicking inside our copy can never drive the original UI.
    /// </summary>
    internal static void DisableExternalButtons(GameObject clone)
    {
        foreach (Button button in clone.GetComponentsInChildren<Button>(true))
        {
            int count = button.onClick.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                Object target = button.onClick.GetPersistentTarget(i);
                if (target != null && !IsInside(target, clone.transform))
                {
                    button.onClick = new Button.ButtonClickedEvent();
                    button.interactable = false;
                    break;
                }
            }
        }
    }

    /// <summary>Sets every visible text under <paramref name="root"/> to a raw (non-localized) string.</summary>
    internal static void SetRawText(GameObject root, string value)
    {
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            if (text is AutoLocalizedText localized)
            {
                localized.LocalizeText = false;
                localized.text = value;
            }
            else
            {
                text.text = value;
            }
        }
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text is AutoLocalizedTextMeshProUGUI localized)
            {
                localized.LocalizeText = false;
            }
            text.text = value;
        }
    }

    /// <summary>Size of a cloned element's rect, falling back when the prefab relies on its parent layout.</summary>
    internal static Vector2 SizeOf(RectTransform rect, Vector2 fallback)
    {
        Vector2 size = rect.rect.size;
        if (size.x < 1f || size.y < 1f)
        {
            size = rect.sizeDelta;
        }
        return size.x < 1f || size.y < 1f ? fallback : size;
    }

    /// <summary>Logs a clone's hierarchy once per title per session, to tune placement from the log.</summary>
    internal static void DumpOnce(string title, Transform root)
    {
        if (!DinnerRushConfig.DebugMode.Value || !Dumped.Add(title))
        {
            return;
        }
        var sb = new StringBuilder($"[UI dump] {title}\n");
        Append(sb, root, 0);
        Plugin.Log.LogInfo(sb.ToString());
    }

    private static void Append(StringBuilder sb, Transform t, int depth)
    {
        if (depth > 6)
        {
            return;
        }
        var rt = t as RectTransform;
        string rect = rt != null ? $" size={rt.rect.size} sizeDelta={rt.sizeDelta} anchors={rt.anchorMin}-{rt.anchorMax}" : "";
        string components = string.Join(", ", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
        sb.Append(' ', depth * 2).AppendLine($"{t.name}{(t.gameObject.activeSelf ? "" : " (inactive)")}{rect} [{components}]");
        foreach (Transform child in t)
        {
            Append(sb, child, depth + 1);
        }
    }

    private static bool IsInside(Object target, Transform root)
    {
        Transform t = target switch
        {
            Component c => c.transform,
            GameObject g => g.transform,
            _ => null,
        };
        return t != null && t.IsChildOf(root);
    }
}
