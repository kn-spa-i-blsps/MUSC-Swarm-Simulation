using UnityEngine;

/// <summary>
/// Directed formation edge. PID / Smith state is stored on the owner <see cref="DroneAI"/>,
/// not here. Inter-trio edges exist only between mothers (see <see cref="DroneTrioList"/>).
/// </summary>
[System.Serializable]
public class Connection
{
    public DroneAI target;
    public float desiredDistance;
}
