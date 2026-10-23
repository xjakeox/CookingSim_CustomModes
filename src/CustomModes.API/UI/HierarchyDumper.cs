using System.Linq;
using System.Text;
using UnityEngine;

namespace CustomModes.API.UI;

/// <summary>Debug helper: logs transform paths, rects and components so UI placement can be tuned from the log.</summary>
internal static class HierarchyDumper
{
    private const int MaxLines = 600;

    internal static void Dump(string title, Transform root, int maxDepth)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[UI dump] {title}: {PathOf(root)}");
        int lines = 0;
        Append(sb, root, 0, maxDepth, ref lines);
        Log.Info(sb.ToString());
    }

    internal static void DumpAncestors(string title, Transform leaf)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[UI dump] ancestors of {title}:");
        for (Transform t = leaf; t != null; t = t.parent)
        {
            sb.AppendLine("  " + Describe(t));
        }
        Log.Info(sb.ToString());
    }

    internal static string PathOf(Transform t)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (; t != null; t = t.parent)
        {
            parts.Add(t.name);
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void Append(StringBuilder sb, Transform t, int depth, int maxDepth, ref int lines)
    {
        if (lines++ >= MaxLines)
        {
            return;
        }
        sb.Append(' ', depth * 2).AppendLine(Describe(t));
        if (depth >= maxDepth)
        {
            return;
        }
        foreach (Transform child in t)
        {
            Append(sb, child, depth + 1, maxDepth, ref lines);
        }
    }

    private static string Describe(Transform t)
    {
        string rect = t is RectTransform rt
            ? $" pos={rt.anchoredPosition} size={rt.rect.size} anchors={rt.anchorMin}-{rt.anchorMax} pivot={rt.pivot}"
            : "";
        string components = string.Join(", ", t.GetComponents<Component>()
            .Where(c => c != null && !(c is Transform))
            .Select(c => c.GetType().Name));
        return $"{t.name}{(t.gameObject.activeSelf ? "" : " (inactive)")}{rect} [{components}]";
    }
}
