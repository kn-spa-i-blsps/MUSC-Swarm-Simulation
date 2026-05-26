using UnityEngine;

/// <summary>
/// Polaczenie miedzy dwoma dronami uzywane przez regulator PID.
/// Zadana odleglosc bedzie utrzymywana przez Smith Predictor + PID.
/// </summary>
[System.Serializable]
public class Connection
{
    [Tooltip("Sasiad - drugi dron w parze.")]
    public DroneAI target;

    [Tooltip("Zadana odleglosc do sasiada (jednostki Unity).")]
    [Min(0.01f)] public float desiredDistance;
}
