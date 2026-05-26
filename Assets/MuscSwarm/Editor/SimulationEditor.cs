#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MuscSwarm.EditorTools
{
    /// <summary>
    /// Custom Inspector dla <see cref="Simulation"/>: tabelaryczna lista polaczen
    /// miedzy trio (zamiast nieczytelnej domyslnej listy struktur) i panel runtime stats.
    /// </summary>
    [CustomEditor(typeof(Simulation))]
    public class SimulationEditor : Editor
    {
        SerializedProperty spDronePrefab;
        SerializedProperty spTrioDistance;
        SerializedProperty spSpawnPositions;
        SerializedProperty spConnections;
        SerializedProperty spGlobalDroneSettings;
        SerializedProperty spEnableTrails;
        SerializedProperty spPersistentTrails;
        SerializedProperty spTrailDuration;
        SerializedProperty spTrailWidth;
        SerializedProperty spAnchorTrailColor;
        SerializedProperty spFollowerTrailColor;
        SerializedProperty spDroneDebugNumber;
        SerializedProperty spDebugForces;
        SerializedProperty spDebugPosition;
        SerializedProperty spDebugInterval;

        void OnEnable()
        {
            spDronePrefab = serializedObject.FindProperty("dronePrefab");
            spTrioDistance = serializedObject.FindProperty("trioDistance");
            spSpawnPositions = serializedObject.FindProperty("spawnPositions");
            spConnections = serializedObject.FindProperty("connections");
            spGlobalDroneSettings = serializedObject.FindProperty("globalDroneSettings");
            spEnableTrails = serializedObject.FindProperty("enableTrails");
            spPersistentTrails = serializedObject.FindProperty("persistentTrails");
            spTrailDuration = serializedObject.FindProperty("trailDuration");
            spTrailWidth = serializedObject.FindProperty("trailWidth");
            spAnchorTrailColor = serializedObject.FindProperty("anchorTrailColor");
            spFollowerTrailColor = serializedObject.FindProperty("followerTrailColor");
            spDroneDebugNumber = serializedObject.FindProperty("droneDebugNumber");
            spDebugForces = serializedObject.FindProperty("debugForces");
            spDebugPosition = serializedObject.FindProperty("debugPosition");
            spDebugInterval = serializedObject.FindProperty("debugInterval");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // -- Spawning section --
            EditorGUILayout.LabelField("Spawning", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spDronePrefab);
            EditorGUILayout.PropertyField(spTrioDistance);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Trio spawn positions ({spSpawnPositions.arraySize})", EditorStyles.miniBoldLabel);
            DrawSpawnTable();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Inter-trio connections ({spConnections.arraySize})", EditorStyles.miniBoldLabel);
            DrawConnectionsTable();

            // -- Tuning --
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Drone tuning", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spGlobalDroneSettings);
            if (spGlobalDroneSettings.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Brak ScriptableObject DroneTuning. Stworz przez Assets > Create > MuscSwarm > Drone Tuning Profile, lub uzyj DefaultDroneTuning.asset.", MessageType.Warning);

            // -- Trails --
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Trails (wizualizacja toru lotu)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spEnableTrails, new GUIContent("Enable trails"));
            using (new EditorGUI.DisabledScope(!spEnableTrails.boolValue))
            {
                EditorGUILayout.PropertyField(spPersistentTrails, new GUIContent("Persistent (never fade)"));
                using (new EditorGUI.DisabledScope(spPersistentTrails.boolValue))
                {
                    EditorGUILayout.PropertyField(spTrailDuration, new GUIContent("Duration (s)"));
                }
                EditorGUILayout.PropertyField(spTrailWidth, new GUIContent("Width"));
                EditorGUILayout.PropertyField(spAnchorTrailColor, new GUIContent("Anchor color"));
                EditorGUILayout.PropertyField(spFollowerTrailColor, new GUIContent("Follower color"));
            }
            if (spEnableTrails.boolValue && Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Slady aktualizuja sie live - zmiany koloru/dlugosci/szerokosci dzialaja bez restartu.", MessageType.Info);
                if (GUILayout.Button("Clear trails now"))
                {
                    var sim = (Simulation)target;
                    foreach (DroneAI d in sim.AllDrones)
                    {
                        if (d == null) continue;
                        var tr = d.GetComponent<MuscSwarm.DroneTrail>();
                        if (tr != null) tr.ClearTrail();
                    }
                }
            }

            // -- Debug --
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spDroneDebugNumber);
            EditorGUILayout.PropertyField(spDebugForces);
            EditorGUILayout.PropertyField(spDebugPosition);
            EditorGUILayout.PropertyField(spDebugInterval);

            // -- Runtime stats --
            if (Application.isPlaying)
            {
                var sim = (Simulation)target;
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Trio count:", sim.TrioCount.ToString());
                EditorGUILayout.LabelField("Drone count:", sim.AllDrones.Count.ToString());
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawSpawnTable()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("#", GUILayout.Width(30));
            EditorGUILayout.LabelField("x", GUILayout.Width(70));
            EditorGUILayout.LabelField("z", GUILayout.Width(70));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(24)))
                spSpawnPositions.arraySize++;
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < spSpawnPositions.arraySize; i++)
            {
                SerializedProperty entry = spSpawnPositions.GetArrayElementAtIndex(i);
                SerializedProperty x = entry.FindPropertyRelative("x");
                SerializedProperty z = entry.FindPropertyRelative("z");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{i + 1}", GUILayout.Width(30));
                x.floatValue = EditorGUILayout.FloatField(x.floatValue, GUILayout.Width(70));
                z.floatValue = EditorGUILayout.FloatField(z.floatValue, GUILayout.Width(70));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    spSpawnPositions.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        void DrawConnectionsTable()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Trio A", GUILayout.Width(60));
            EditorGUILayout.LabelField("Trio B", GUILayout.Width(60));
            EditorGUILayout.LabelField("Dist", GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(24)))
                spConnections.arraySize++;
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < spConnections.arraySize; i++)
            {
                SerializedProperty entry = spConnections.GetArrayElementAtIndex(i);
                SerializedProperty x = entry.FindPropertyRelative("x");
                SerializedProperty y = entry.FindPropertyRelative("y");
                SerializedProperty d = entry.FindPropertyRelative("d");

                EditorGUILayout.BeginHorizontal();
                x.intValue = EditorGUILayout.IntField(x.intValue, GUILayout.Width(60));
                y.intValue = EditorGUILayout.IntField(y.intValue, GUILayout.Width(60));
                d.floatValue = EditorGUILayout.FloatField(d.floatValue, GUILayout.Width(60));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    spConnections.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
