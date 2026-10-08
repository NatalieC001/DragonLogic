using UnityEngine;
using UnityEditor;
using VRDragonBoss.AI;

namespace VRDragonBoss.EditorScripts
{
    [CustomEditor(typeof(DragonBrain))]
    public class DragonBrainEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DragonBrain brain = (DragonBrain)target;

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Live AI State", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Current State:", brain.CurrentState.ToString());

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Utility Scores", EditorStyles.boldLabel);

                if (brain.UtilityScores != null)
                {
                    foreach (var kvp in brain.UtilityScores)
                    {
                        // Highlight the highest score
                        GUIStyle style = new GUIStyle(EditorStyles.label);
                        if (kvp.Key == brain.CurrentState)
                        {
                            style.normal.textColor = Color.green;
                            style.fontStyle = FontStyle.Bold;
                        }

                        EditorGUILayout.LabelField(kvp.Key.ToString(), kvp.Value.ToString("F1"), style);
                    }
                }

                // Repaint constantly while playing to see live updates
                Repaint();
            }
            else
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Enter Play Mode to view live AI state and utility scores.", MessageType.Info);
            }
        }
    }
}
