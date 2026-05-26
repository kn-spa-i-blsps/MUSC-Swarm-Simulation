#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MuscSwarm.EditorTools
{
    /// <summary>
    /// Custom Inspector dla <see cref="DroneAI"/>: standardowe pola + panel runtime
    /// (predkosc, tryb, dlugosc listy polaczen, stan UWB - widoczne w Play Mode).
    /// </summary>
    [CustomEditor(typeof(DroneAI))]
    public class DroneAIEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var ai = (DroneAI)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Wejdz w Play Mode by zobaczyc panel runtime.", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field("Position", ai.transform.position);
                EditorGUILayout.Vector3Field("Velocity (jed/s)", ai.SimulatedVelocity);
                EditorGUILayout.FloatField("Speed (jed/s)", ai.SimulatedVelocity.magnitude);

                if (ai.droneSettings != null)
                {
                    EditorGUILayout.LabelField("Mode", ai.droneSettings.movementMode.ToString());
                    EditorGUILayout.LabelField("UWB interval", $"{ai.droneSettings.uwbIntervalMs} ms");
                }

                EditorGUILayout.IntField("Connections", ai.connections.Count);
                EditorGUILayout.Toggle("Is anchor", ai.isAnchor);

                if (ai.transmissionNode != null)
                    EditorGUILayout.LabelField("Network pairs", ai.transmissionNode.TrackedPairs.ToString());
            }

            // Force a repaint so the runtime panel keeps refreshing.
            Repaint();
        }
    }
}
#endif
