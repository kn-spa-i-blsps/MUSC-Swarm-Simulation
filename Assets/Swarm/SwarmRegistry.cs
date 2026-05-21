using System.Collections.Generic;
using UnityEngine;

public static class SwarmRegistry
{
    static readonly List<DroneAI> allDrones = new List<DroneAI>();

    public static void Register(IEnumerable<DroneAI> drones)
    {
        allDrones.Clear();
        allDrones.AddRange(drones);
    }

    public static IReadOnlyList<DroneAI> All => allDrones;

    public static void GetNeighborsInRadius(DroneAI self, float radius, List<DroneAI> buffer)
    {
        buffer.Clear();
        Vector3 p = self.transform.position;

        for (int i = 0; i < allDrones.Count; i++)
        {
            DroneAI other = allDrones[i];
            if (other == null || other == self) continue;

            if (Vector3.SqrMagnitude(other.transform.position - p) <= radius * radius)
                buffer.Add(other);
        }
    }
}
