#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MuscSwarm.EditorTools
{
    /// <summary>
    /// Custom Inspector dla <see cref="Mission"/>: per-waypoint pokazuje wymagane
    /// predkosci H/V i koloruje ostrzezenie gdy przekraczaja limity referencyjnego DroneSettings.
    /// </summary>
    /// <remarks>
    /// "Wymagana" predkosc waypointa = magnitude przesuniecia / timeStamp:
    ///   - H = sqrt(target.x^2 + target.z^2) / timeStamp
    ///   - V = |target.y| / timeStamp
    /// Jesli H > tuning.horizontalSpeed lub V > tuning.verticalSpeed, ghost bedzie szybszy
    /// niz dron - dron zostanie w tyle (cap zadziala) i misja "wykona sie po skrocie".
    /// </remarks>
    [CustomEditor(typeof(Mission))]
    public class MissionEditor : Editor
    {
        SerializedProperty spTargets;
        SerializedProperty spReferenceTuning;

        const float WarnSoftMargin = 0.9f; // 90% capa juz daje zolte ostrzezenie

        void OnEnable()
        {
            spTargets = serializedObject.FindProperty("targets");
            spReferenceTuning = serializedObject.FindProperty("referenceTuning");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(spReferenceTuning,
                new GUIContent("Reference DroneSettings",
                    "Przeciagnij DroneSettings (np. DefaultDroneTuning) aby zobaczyc ostrzezenia o nieosiagalnych segmentach."));

            DroneSettings tuning = spReferenceTuning.objectReferenceValue as DroneSettings;

            if (tuning == null)
            {
                EditorGUILayout.HelpBox(
                    "Aby zobaczyc ostrzezenia o nieosiagalnych segmentach, przeciagnij tu profil DroneSettings " +
                    "(np. Assets/MuscSwarm/Assets/DefaultDroneTuning).",
                    MessageType.Info);
            }
            else
            {
                DrawMissionSummary(tuning);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"Waypoints ({spTargets.arraySize})", EditorStyles.boldLabel);

            Vector3 cumulative = Vector3.zero;
            for (int i = 0; i < spTargets.arraySize; i++)
            {
                DrawWaypoint(i, spTargets.GetArrayElementAtIndex(i), tuning, ref cumulative);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(30)))
                spTargets.arraySize++;
            if (GUILayout.Button("-", GUILayout.Width(30)) && spTargets.arraySize > 0)
                spTargets.arraySize--;
            EditorGUILayout.EndHorizontal();

            if (tuning != null && spTargets.arraySize > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Cumulative offset", EditorStyles.miniBoldLabel);
                EditorGUILayout.HelpBox(
                    $"Suma wszystkich przesuniec: {FormatVec(cumulative)}\n" +
                    $"To pozycja drona wzgledem startu po wykonaniu calej misji.",
                    MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawMissionSummary(DroneSettings tuning)
        {
            float maxH = 0f, maxV = 0f;
            int worstHIdx = -1, worstVIdx = -1;

            for (int i = 0; i < spTargets.arraySize; i++)
            {
                SerializedProperty entry = spTargets.GetArrayElementAtIndex(i);
                Vector3 t = entry.FindPropertyRelative("target").vector3Value;
                float dur = Mathf.Max(0.001f, entry.FindPropertyRelative("timeStamp").floatValue);

                float h = new Vector2(t.x, t.z).magnitude / dur;
                float v = Mathf.Abs(t.y) / dur;

                if (h > maxH) { maxH = h; worstHIdx = i; }
                if (v > maxV) { maxV = v; worstVIdx = i; }
            }

            bool hOver = maxH > tuning.horizontalSpeed;
            bool vOver = maxV > tuning.verticalSpeed;
            bool hClose = !hOver && maxH > tuning.horizontalSpeed * WarnSoftMargin;
            bool vClose = !vOver && maxV > tuning.verticalSpeed * WarnSoftMargin;

            MessageType type = (hOver || vOver) ? MessageType.Error
                              : (hClose || vClose) ? MessageType.Warning
                              : MessageType.Info;

            string status = (hOver || vOver) ? "PRZEKROCZONY LIMIT" :
                            (hClose || vClose) ? "blisko limitu" : "OK";

            string msg = $"Status: {status}\n" +
                         $"Max H (#{worstHIdx}): {maxH:F2} u/s   vs   limit {tuning.horizontalSpeed:F1} u/s\n" +
                         $"Max V (#{worstVIdx}): {maxV:F2} u/s   vs   limit {tuning.verticalSpeed:F1} u/s";

            EditorGUILayout.HelpBox(msg, type);
        }

        void DrawWaypoint(int idx, SerializedProperty entry, DroneSettings tuning, ref Vector3 cumulative)
        {
            SerializedProperty targetProp = entry.FindPropertyRelative("target");
            SerializedProperty timeProp = entry.FindPropertyRelative("timeStamp");

            Vector3 t = targetProp.vector3Value;
            float dur = Mathf.Max(0.001f, timeProp.floatValue);

            float h = new Vector2(t.x, t.z).magnitude / dur;
            float v = Mathf.Abs(t.y) / dur;

            // Kolorowanie tla per status (bez DroneSettings = neutralne).
            Color prev = GUI.backgroundColor;
            if (tuning != null)
            {
                bool hOver = h > tuning.horizontalSpeed;
                bool vOver = v > tuning.verticalSpeed;
                bool hClose = !hOver && h > tuning.horizontalSpeed * WarnSoftMargin;
                bool vClose = !vOver && v > tuning.verticalSpeed * WarnSoftMargin;

                if (hOver || vOver) GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                else if (hClose || vClose) GUI.backgroundColor = new Color(1f, 0.85f, 0.45f);
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prev;

            EditorGUILayout.LabelField($"Element {idx}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targetProp, new GUIContent("Target (Δx, Δy, Δz)"));
            EditorGUILayout.PropertyField(timeProp, new GUIContent("Time Stamp (s)"));

            if (tuning != null)
            {
                bool hOver = h > tuning.horizontalSpeed;
                bool vOver = v > tuning.verticalSpeed;

                string hLabel = hOver ? $"H: {h:F2} u/s  >  cap {tuning.horizontalSpeed:F1}  - dron NIE wyrobi"
                                       : $"H: {h:F2} u/s  (cap {tuning.horizontalSpeed:F1})";
                string vLabel = vOver ? $"V: {v:F2} u/s  >  cap {tuning.verticalSpeed:F1}  - dron NIE wyrobi"
                                       : $"V: {v:F2} u/s  (cap {tuning.verticalSpeed:F1})";

                GUIStyle hStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = hOver ? new Color(0.85f, 0.2f, 0.2f) : Color.gray }
                };
                GUIStyle vStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = vOver ? new Color(0.85f, 0.2f, 0.2f) : Color.gray }
                };

                EditorGUILayout.LabelField(hLabel, hStyle);
                EditorGUILayout.LabelField(vLabel, vStyle);
            }

            cumulative += t;
            EditorGUILayout.LabelField($"After this WP, drone position offset: {FormatVec(cumulative)}",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        static string FormatVec(Vector3 v) => $"({v.x:F1}, {v.y:F1}, {v.z:F1})";
    }
}
#endif
