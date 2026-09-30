using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(DragonBrain))]
public class DragonBrainEditor : Editor
{
    private Dictionary<DragonState, Color> stateColors = new Dictionary<DragonState, Color>
    {
        { DragonState.FleeToHeal, new Color(0.2f, 0.8f, 0.2f) },      // Green
        { DragonState.DefendCrystal, new Color(0.1f, 0.6f, 0.1f) },   // Dark Green
        { DragonState.TopplePillar, new Color(1f, 0.5f, 0f) },        // Orange
        { DragonState.DenyArea, new Color(0.8f, 0.1f, 0.8f) },        // Purple
        { DragonState.AttackPlayer, new Color(0.9f, 0.1f, 0.1f) },    // Red
        { DragonState.DefendMinions, new Color(0.2f, 0.4f, 0.9f) },   // Blue
        { DragonState.Roam, new Color(0.5f, 0.5f, 0.5f) }             // Grey
    };

    public override void OnInspectorGUI()
    {
        // Draw the default inspector properties (Configuration, UnityEvents, Subsystems, etc.)
        DrawDefaultInspector();

        DragonBrain brain = (DragonBrain)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Live AI Desire Table", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to view live desire scores.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"Current State: {brain.CurrentState}", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (brain.LastEvaluatedScores == null || brain.LastEvaluatedScores.Count == 0)
        {
            EditorGUILayout.HelpBox("No scores evaluated yet.", MessageType.Warning);
            return;
        }

        // We want to sort the scores to show the highest desire at the top
        List<KeyValuePair<DragonState, float>> sortedScores = new List<KeyValuePair<DragonState, float>>(brain.LastEvaluatedScores);
        sortedScores.Sort((a, b) => b.Value.CompareTo(a.Value)); // Descending order

        foreach (var kvp in sortedScores)
        {
            DrawDesireBar(kvp.Key, kvp.Value);
        }

        // Force the editor to repaint constantly during playmode to ensure the bars animate smoothly
        Repaint();
    }

    private void DrawDesireBar(DragonState state, float score)
    {
        // Normalizing the score for the progress bar (0 to 1). We assume 100f is the absolute max priority.
        float normalizedScore = Mathf.Clamp01(score / 100f);

        Color barColor = stateColors.ContainsKey(state) ? stateColors[state] : Color.white;

        // Temporarily change the GUI color for the progress bar
        Color defaultColor = GUI.color;
        GUI.color = barColor;

        Rect rect = GUILayoutUtility.GetRect(18, 18, "TextField");
        EditorGUI.ProgressBar(rect, normalizedScore, $"{state}: {score:F1}");

        GUI.color = defaultColor;

        EditorGUILayout.Space(2);
    }
}
