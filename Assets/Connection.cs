using UnityEngine;

/// <summary>
/// Directed formation edge used by <see cref="MuscSwarm.FormationPid"/> and
/// <see cref="MuscSwarm.ConnectionSpring"/>. No PID state here (unlike branch main).
/// </summary>
[System.Serializable]
public class Connection
{
    [Tooltip("Sasiad - drugi dron w parze.")]
    public DroneAI target;

    [Tooltip("Zadana odleglosc do sasiada (jednostki Unity).")]
    [Min(0.01f)] public float desiredDistance;
}
