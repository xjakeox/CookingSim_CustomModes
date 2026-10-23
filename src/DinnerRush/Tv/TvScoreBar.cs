using System.Collections.Generic;
using DinnerRush.Core;
using DinnerRush.Ui;
using TMPro;
using UnityEngine;

namespace DinnerRush.Tv;

/// <summary>
/// Strike marks (centred; grey until earned, then red) and the star score (right-aligned), placed inside the TV's
/// "BottomBar" strip next to the game's clock and skip button, styled like the clock text so it looks built in.
/// </summary>
internal sealed class TvScoreBar : MonoBehaviour
{
    private const string ObjectName = "DinnerRushScoreBar";
    private const string BottomBarName = "BottomBar";

    // TV canvas units (the bar is 140 tall; the game's clock text is 80 tall).
    private const float MarkWidth = 70f;
    private const float RowHeight = 100f;
    private const float ScoreWidth = 420f;
    private const float RightPadding = 40f;

    private static readonly Color StrikeColor = new Color(0.95f, 0.22f, 0.18f, 1f);
    private static readonly Color StrikeEmptyColor = new Color(0.55f, 0.55f, 0.55f, 0.9f);

    private readonly List<TextMeshProUGUI> marks = new List<TextMeshProUGUI>();
    private TextMeshProUGUI scoreText;
    private int lastHalfStars = -1;
    private int lastStrikes = -1;

    /// <summary>Adds the bar to the panel's bottom strip unless it is already there.</summary>
    /// <param name="panel">TVUIDaySkipping's "Panel" object.</param>
    /// <param name="style">The game's clock text, whose font and size we copy.</param>
    internal static void Attach(GameObject panel, TMP_Text style, int slots)
    {
        Transform host = BottomBar(panel);
        if (host == null || host.Find(ObjectName) != null)
        {
            return;
        }
        RectTransform root = UiKit.CreateRect(ObjectName, host);
        UiKit.Stretch(root);
        root.gameObject.AddComponent<TvScoreBar>().Build(root, style, slots);
        // Match the TV's layer so whichever camera draws the TV also draws the bar.
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = host.gameObject.layer;
        }
    }

    internal static void Detach(GameObject panel)
    {
        Transform host = BottomBar(panel);
        Transform bar = host != null ? host.Find(ObjectName) : null;
        if (bar != null)
        {
            Destroy(bar.gameObject);
        }
    }

    private static Transform BottomBar(GameObject panel)
    {
        if (panel == null)
        {
            return null;
        }
        Transform bar = panel.transform.Find(BottomBarName);
        if (bar == null)
        {
            Plugin.Log.LogWarning("TV bottom bar not found; score bar not shown.");
        }
        return bar;
    }

    private void Build(RectTransform root, TMP_Text style, int slots)
    {
        // Strike marks: centred in the strip.
        RectTransform strip = UiKit.CreateRect("Strikes", root);
        UiKit.Place(strip, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(slots * MarkWidth, RowHeight));
        for (int i = 0; i < slots; i++)
        {
            TextMeshProUGUI mark = StyledText("Strike" + i, strip, style, TextAlignmentOptions.Center, StrikeEmptyColor);
            mark.text = "X";
            UiKit.Place(mark.rectTransform, new Vector2(0f, 0.5f), new Vector2(i * MarkWidth, 0f), new Vector2(MarkWidth, RowHeight));
            marks.Add(mark);
        }

        // Score: right edge, right-aligned.
        scoreText = StyledText("Score", root, style, TextAlignmentOptions.Right, style != null ? style.color : Color.white);
        UiKit.Place(scoreText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-RightPadding, 0f), new Vector2(ScoreWidth, RowHeight));
    }

    private static TextMeshProUGUI StyledText(string name, Transform parent, TMP_Text style, TextAlignmentOptions alignment, Color color)
    {
        TextMeshProUGUI text = UiKit.CreateText(name, parent, 56f, alignment, color);
        if (style != null)
        {
            text.font = style.font;
            text.fontSharedMaterial = style.fontSharedMaterial;
            text.fontSize = style.fontSize;
            text.fontStyle = style.fontStyle;
        }
        return text;
    }

    private void Update()
    {
        DinnerRushMode mode = DinnerRushMode.Current;
        if (mode == null)
        {
            return;
        }
        ScoreTracker score = mode.Run.Score;
        if (score.TotalHalfStars != lastHalfStars)
        {
            lastHalfStars = score.TotalHalfStars;
            scoreText.text = "Stars " + ScoreTracker.FormatStars(score.TotalHalfStars);
        }
        if (score.Strikes != lastStrikes)
        {
            lastStrikes = score.Strikes;
            for (int i = 0; i < marks.Count; i++)
            {
                marks[i].color = i < score.Strikes ? StrikeColor : StrikeEmptyColor;
            }
        }
    }
}
