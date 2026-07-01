using System.Collections.Generic;
using MuscSwarm;
using UnityEngine;

/// <summary>
/// Virtual Force Field steering (boidsy Reynoldsa): separation + alignment + cohesion + goal.
/// Wyznacza "preferowana" predkosc, ktora pozniej (w trybie VffOrca) jest poprawiana
/// przez ORCA tak, by uniknac kolizji.
/// </summary>
public static class VffSteering
{
    /// <summary>
    /// Liczy preferowana predkosc drona w plaszczyznie XZ.
    /// </summary>
    /// <param name="neighbors">
    /// Kandydaci na sasiadow (juz z rozwiazana pozycja/predkoscia - ground truth albo UWB,
    /// patrz <see cref="SwarmSteeringSettings.useUwbNeighborSensing"/>). Filtrowanie po
    /// perceptionRadius/separationRadius dzieje sie tutaj, na bazie tej rozwiazanej pozycji.
    /// </param>
    public static Vector3 ComputePreferredVelocity(
        DroneAI self,
        IReadOnlyList<NeighborSample> neighbors,
        Vector3 goalPosition,
        SwarmSteeringSettings s,
        float maxSpeed)
    {
        Vector3 pos = self.transform.position;
        Vector3 selfVel = self.SimulatedVelocity;

        Vector3 separation = Vector3.zero;
        Vector3 alignmentSum = Vector3.zero;
        Vector3 cohesionSum = Vector3.zero;
        int crowdCount = 0;

        float perceptionR2 = s.perceptionRadius * s.perceptionRadius;

        for (int i = 0; i < neighbors.Count; i++)
        {
            NeighborSample n = neighbors[i];
            if (n.drone == null || n.drone == self) continue;

            Vector3 toNeighbor = n.position - pos;
            float distSq = toNeighbor.sqrMagnitude;
            if (distSq > perceptionR2) continue;

            float dist = Mathf.Sqrt(distSq);
            if (dist < 0.001f) continue;

            // Separation: silne odpychanie skalowane 1/dist (im blizej, tym mocniej).
            if (dist < s.separationRadius)
                separation -= toNeighbor.normalized / dist;

            alignmentSum += n.velocity;
            cohesionSum += n.position;
            crowdCount++;
        }

        Vector3 steer = Vector3.zero;

        if (separation.sqrMagnitude > 1e-4f)
            steer += separation.normalized * s.separationWeight;

        if (crowdCount > 0)
        {
            Vector3 avgVel = alignmentSum / crowdCount;
            steer += (avgVel - selfVel) * s.alignmentWeight;

            Vector3 center = cohesionSum / crowdCount;
            Vector3 toCenter = center - pos; toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.01f)
                steer += toCenter.normalized * s.cohesionWeight;
        }

        Vector3 toGoal = goalPosition - pos; toGoal.y = 0f;
        if (toGoal.sqrMagnitude > 0.01f)
            steer += toGoal.normalized * s.goalWeight;

        if (steer.sqrMagnitude < 1e-4f) return Vector3.zero;

        steer.y = 0f;
        return steer.normalized * maxSpeed;
    }
}
