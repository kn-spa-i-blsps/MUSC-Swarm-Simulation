using UnityEngine;

[System.Serializable]
public class DroneSettings
{
    public float verticalSpeed = 3f;
    public float yawSpeed = 100f;

    [Header("PID Settings")]
    public float P = 50f;
    public float I = 0.05f;
    public float D = 5f;
    [Range(0.01f, 1f)]
    public float derivativeSmoothing = 0.2f;

    [Header("Physics")]
    public float moveSpeed = 1f;
    public float drag = 0.95f;

    [Header("Other")]
    public Mission mission;

    [Header("UWB Transmission")]
    public int uwbIntervalMs = 200;
    public float springK = 0.01f;

    [Header("Errors")]
    public float driftSpeed;
    public int globalLatency;
}
