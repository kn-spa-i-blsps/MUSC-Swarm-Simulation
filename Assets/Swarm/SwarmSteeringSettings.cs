using UnityEngine;

public enum DroneMovementMode
{
    PidSpring = 0,
    VffOrca = 1,
}

[System.Serializable]
public class SwarmSteeringSettings
{
    [Header("Mode")]
    public DroneMovementMode movementMode = DroneMovementMode.VffOrca;

    [Header("VFF weights")]
    public float separationWeight = 2.5f;
    public float alignmentWeight = 1f;
    public float cohesionWeight = 1f;
    public float goalWeight = 2f;

    [Header("VFF radii")]
    public float perceptionRadius = 8f;
    public float separationRadius = 2.5f;

    [Header("ORCA (XZ)")]
    public float agentRadius = 0.6f;
    public float orcaNeighborRadius = 6f;
    public float orcaTimeHorizon = 1.5f;

    [Header("Debug")]
    public bool drawSteeringVectors = false;
}
