using System.Collections.Generic;
using UnityEngine;

public static class VffSteering
{
    public static Vector3 ComputePreferredVelocity(
        DroneAI self,
        IReadOnlyList<DroneAI> neighbors,
        Vector3 goalPosition,
        SwarmSteeringSettings settings,
        float maxSpeed)
    {
        Vector3 pos = self.transform.position;
        Vector3 selfVel = self.SimulatedVelocity;

        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        int alignCount = 0;
        int cohCount = 0;

        for (int i = 0; i < neighbors.Count; i++)
        {
            DroneAI n = neighbors[i];
            if (n == null) continue;

            Vector3 toNeighbor = n.transform.position - pos;
            float dist = toNeighbor.magnitude;
            if (dist < 0.001f) continue;

            if (dist < settings.separationRadius)
            {
                separation -= toNeighbor.normalized / dist;
            }

            alignment += n.SimulatedVelocity;
            alignCount++;

            cohesion += n.transform.position;
            cohCount++;
        }

        Vector3 goal = Vector3.zero;
        Vector3 toGoal = goalPosition - pos;
        toGoal.y = 0f;
        if (toGoal.sqrMagnitude > 0.01f)
            goal = toGoal.normalized;

        Vector3 steer = Vector3.zero;
        if (separation.sqrMagnitude > 0.0001f)
            steer += separation.normalized * settings.separationWeight;
        if (alignCount > 0)
        {
            Vector3 avgVel = alignment / alignCount;
            steer += (avgVel - selfVel) * settings.alignmentWeight;
        }
        if (cohCount > 0)
        {
            Vector3 center = cohesion / cohCount;
            Vector3 toCenter = center - pos;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.01f)
                steer += toCenter.normalized * settings.cohesionWeight;
        }
        steer += goal * settings.goalWeight;

        if (steer.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        steer.y = 0f;
        return steer.normalized * maxSpeed;
    }
}
