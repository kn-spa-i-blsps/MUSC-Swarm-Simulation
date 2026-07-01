#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MuscSwarm.EditorTools
{
    /// <summary>
    /// Custom Inspector dla <see cref="DroneSettings"/>: rozdziela pola na "Wspolne",
    /// "PidSpring" i "VffOrca" i domyslnie chowa sekcje nieuzywane przez aktualnie
    /// wybrany <see cref="DroneMovementMode"/>, zeby nie gubic sie w ~30 polach naraz.
    /// </summary>
    [CustomEditor(typeof(DroneSettings))]
    [CanEditMultipleObjects]
    public class DroneSettingsEditor : Editor
    {
        // ----- Wspolne (uzywane w OBU trybach) -----------------------------------------
        SerializedProperty spHorizontalSpeed;
        SerializedProperty spVerticalSpeed;
        SerializedProperty spYawSpeed;
        SerializedProperty spMovementMode;
        SerializedProperty spMissionP;
        SerializedProperty spVelocityRetention;
        SerializedProperty spObstacleAvoidanceStrength;
        SerializedProperty spObstacleSensingMultiplier;
        SerializedProperty spDroneAvoidBuffer;
        SerializedProperty spMission;

        // ----- UWB (wspolny kanal radiowy - PidSpring uzywa go zawsze, VffOrca opcjonalnie) -----
        SerializedProperty spUwbIntervalMs;
        SerializedProperty spUwbTransmissionDelayMs;
        SerializedProperty spUwbNoiseAmplitude;

        // ----- PidSpring only -----------------------------------------------------------
        SerializedProperty spPidP;
        SerializedProperty spPidI;
        SerializedProperty spPidD;
        SerializedProperty spDerivativeSmoothing;
        SerializedProperty spDeadzoneCm;
        SerializedProperty spMinDThreshold;
        SerializedProperty spSettleVelocity;
        SerializedProperty spMissionD;

        // ----- VffOrca only ---------------------------------------------------------------
        SerializedProperty spSwarm;

        static bool showInactivePidSpring;
        static bool showInactiveVffOrca;

        static readonly Color PidSpringColor = new Color(0.35f, 0.65f, 1f);
        static readonly Color VffOrcaColor = new Color(0.55f, 1f, 0.55f);

        void OnEnable()
        {
            spHorizontalSpeed = serializedObject.FindProperty("horizontalSpeed");
            spVerticalSpeed = serializedObject.FindProperty("verticalSpeed");
            spYawSpeed = serializedObject.FindProperty("yawSpeed");
            spMovementMode = serializedObject.FindProperty("movementMode");
            spMissionP = serializedObject.FindProperty("missionP");
            spVelocityRetention = serializedObject.FindProperty("velocityRetention");
            spObstacleAvoidanceStrength = serializedObject.FindProperty("obstacleAvoidanceStrength");
            spObstacleSensingMultiplier = serializedObject.FindProperty("obstacleSensingMultiplier");
            spDroneAvoidBuffer = serializedObject.FindProperty("droneAvoidBuffer");
            spMission = serializedObject.FindProperty("mission");

            spUwbIntervalMs = serializedObject.FindProperty("uwbIntervalMs");
            spUwbTransmissionDelayMs = serializedObject.FindProperty("uwbTransmissionDelayMs");
            spUwbNoiseAmplitude = serializedObject.FindProperty("uwbNoiseAmplitude");

            spPidP = serializedObject.FindProperty("pidP");
            spPidI = serializedObject.FindProperty("pidI");
            spPidD = serializedObject.FindProperty("pidD");
            spDerivativeSmoothing = serializedObject.FindProperty("derivativeSmoothing");
            spDeadzoneCm = serializedObject.FindProperty("deadzoneCm");
            spMinDThreshold = serializedObject.FindProperty("minDThreshold");
            spSettleVelocity = serializedObject.FindProperty("settleVelocity");
            spMissionD = serializedObject.FindProperty("missionD");

            spSwarm = serializedObject.FindProperty("swarm");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var mode = (DroneMovementMode)spMovementMode.enumValueIndex;
            bool isPidSpring = mode == DroneMovementMode.PidSpring;
            bool isVffOrca = mode == DroneMovementMode.VffOrca;

            // ---- Mode selector, na samej gorze, wyroznione ----------------------------
            EditorGUILayout.Space(2);
            Color modeColor = isPidSpring ? PidSpringColor : VffOrcaColor;
            DrawColoredBox(modeColor, () =>
            {
                EditorGUILayout.LabelField("Movement mode", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(spMovementMode, new GUIContent("Mode"));
                EditorGUILayout.LabelField(isPidSpring
                    ? "Aktywne: PID + Spring (formacja po UWB)."
                    : "Aktywne: VFF + ORCA (boidsy + unikanie kolizji).", EditorStyles.miniLabel);
            });

            // ---- Wspolne dla obu trybow ------------------------------------------------
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Wspolne (oba tryby)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spHorizontalSpeed);
            EditorGUILayout.PropertyField(spVerticalSpeed);
            EditorGUILayout.PropertyField(spYawSpeed, new GUIContent("Yaw Speed (tylko manual)"));
            EditorGUILayout.PropertyField(spVelocityRetention);
            EditorGUILayout.PropertyField(spMissionP, new GUIContent("Mission P (cel: pozycja XZ w PidSpring / wysokosc Y w VffOrca)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Obstacle avoidance (oba tryby)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spObstacleAvoidanceStrength);
            EditorGUILayout.PropertyField(spObstacleSensingMultiplier);
            EditorGUILayout.PropertyField(spDroneAvoidBuffer);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Mission (oba tryby)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spMission);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("UWB (kanal radiowy)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spUwbIntervalMs);
            EditorGUILayout.PropertyField(spUwbTransmissionDelayMs);
            EditorGUILayout.PropertyField(spUwbNoiseAmplitude);
            EditorGUILayout.LabelField(isPidSpring
                ? "Uzywane zawsze w PidSpring (dystanse do formacji)."
                : "W VffOrca uzywane TYLKO jesli 'Use Uwb Neighbor Sensing' jest wlaczone w sekcji VFF+ORCA nizej.",
                EditorStyles.miniLabel);

            // ---- Sekcja PidSpring -------------------------------------------------------
            EditorGUILayout.Space(10);
            showInactivePidSpring = DrawModeSection(
                "PID + Spring (formacja po UWB)",
                isPidSpring,
                PidSpringColor,
                showInactivePidSpring,
                () =>
                {
                    EditorGUILayout.LabelField("PID", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(spPidP);
                    EditorGUILayout.PropertyField(spPidI);
                    EditorGUILayout.PropertyField(spPidD);
                    EditorGUILayout.PropertyField(spDerivativeSmoothing);

                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Dead-zone / settle", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(spDeadzoneCm);
                    EditorGUILayout.PropertyField(spMinDThreshold);
                    EditorGUILayout.PropertyField(spSettleVelocity);

                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Mission anchor damping", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(spMissionD, new GUIContent("Mission D"));
                });

            // ---- Sekcja VffOrca ----------------------------------------------------------
            EditorGUILayout.Space(10);
            showInactiveVffOrca = DrawModeSection(
                "VFF + ORCA (boidsy + unikanie kolizji)",
                isVffOrca,
                VffOrcaColor,
                showInactiveVffOrca,
                () =>
                {
                    EditorGUILayout.PropertyField(spSwarm, includeChildren: true);
                });

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Rysuje sekcje pol nalezacych do jednego trybu. Gdy tryb NIE jest aktywny,
        /// sekcja jest domyslnie zwinieta w foldout (odblokowywalny) i wyszarzona,
        /// zamiast po prostu zniknac - zeby nic nie zgubic. Zwraca nowy stan foldouta -
        /// zapisz go z powrotem do pola przechowujacego stan (proste zamiast ref przez lambde).
        /// </summary>
        static bool DrawModeSection(string title, bool active, Color color, bool showInactive, System.Action drawFields)
        {
            if (active)
            {
                DrawColoredBox(color, () =>
                {
                    EditorGUILayout.LabelField(title + "  -  AKTYWNE", EditorStyles.boldLabel);
                    drawFields();
                });
                return showInactive;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            bool newShow = EditorGUILayout.Foldout(showInactive, title + "  (nieaktywne w tym trybie)", true);
            GUI.color = Color.white;
            if (newShow)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    drawFields();
                }
            }
            EditorGUILayout.EndVertical();

            return newShow;
        }

        static void DrawColoredBox(Color tint, System.Action content)
        {
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(Color.white, tint, 0.35f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevBg;
            content();
            EditorGUILayout.EndVertical();
        }
    }
}
#endif
