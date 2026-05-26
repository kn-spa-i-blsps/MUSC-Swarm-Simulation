using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pojedynczy waypoint misji: wektor przesuniecia od poczatku biezacego segmentu
/// + czas w jakim segment ma zostac wykonany.
/// </summary>
[System.Serializable]
public class MissionTarget
{
    [Tooltip("Wektor przesuniecia segmentu (jednostki Unity).")]
    public Vector3 target;

    [Tooltip("Czas trwania segmentu w sekundach.")]
    [Min(0.01f)] public float timeStamp = 1f;
}

/// <summary>
/// Misja waypointowa rojowa - sekwencja segmentow, ktore duch celu pokonuje liniowo.
/// Tworz przez Assets > Create > MuscSwarm > Mission.
/// </summary>
[CreateAssetMenu(fileName = "Mission", menuName = "MuscSwarm/Mission", order = 1)]
public class Mission : ScriptableObject
{
    [Tooltip("Lista waypointow. Kazdy waypoint to wektor przesuniecia + czas dolotu.")]
    public List<MissionTarget> targets = new List<MissionTarget>();

    [Header("Editor only")]
    [Tooltip("Profil drona dla ostrzezen w Inspectorze - sluzy do oceny czy segmenty sa wykonalne (porownanie target/timeStamp z limitami horizontal/vertical Speed). NIE wplywa na runtime.")]
    public DroneSettings referenceTuning;
}
