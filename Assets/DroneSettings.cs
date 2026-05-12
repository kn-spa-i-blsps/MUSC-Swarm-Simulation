using UnityEngine;

[System.Serializable]
public class DroneSettings
{
    [Header("Drone speed")]
    [Range(0.1f, 10f)]
    public float horizontalSpeed = 3f;
    public float verticalSpeed = 3f;
    public float yawSpeed = 100f;

    [Header("PID Settings")]
    public float P = 50f;
    public float I = 0.05f;
    public float D = 5f;
    [Range(0.01f, 1f)]
    public float derivativeSmoothing = 0.2f;

    [Header("Distance Error Settings")]
    [Range(0.01f, 0.1f)]
    public float minDThreshold = 0.05f;
    [Range(0f, 10f)]
    public float maxDistanceErrorInCentimeters = 5f;
    [Range(0f, 0.5f)]
    public float minVelocity = 0.1f;

    [Header("Physics")]
    [Range(0f, 1f)]
    public float drag = 0.95f;

    [Header("Other")]
    public Mission mission;

    [Header("UWB Transmission")]
    [Range(0, 300)]
    public int uwbIntervalMs = 200;

    [Header("Errors")]
    [Range(0.001f, 0.05f)]
    public float driftSpeed;
}
