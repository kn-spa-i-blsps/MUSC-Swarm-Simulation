using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight reciprocal velocity obstacle solver on the XZ plane (ORCA-style).
/// </summary>
public static class Orca2D
{
    public struct AgentState
    {
        public Vector2 position;
        public Vector2 velocity;
        public float radius;
    }

    public static Vector2 ComputeSafeVelocity(
        Vector2 position,
        Vector2 velocity,
        float radius,
        IReadOnlyList<AgentState> others,
        float timeHorizon,
        Vector2 preferredVelocity,
        float maxSpeed)
    {
        Vector2 result = preferredVelocity;
        float tau = Mathf.Max(timeHorizon, 0.05f);

        for (int i = 0; i < others.Count; i++)
        {
            AgentState other = others[i];
            Vector2 relativePosition = other.position - position;
            Vector2 relativeVelocity = result - other.velocity;
            float combinedRadius = radius + other.radius;
            float distSq = relativePosition.sqrMagnitude;
            float combinedRadiusSq = combinedRadius * combinedRadius;

            Vector2 u;
            if (distSq > combinedRadiusSq)
            {
                Vector2 w = relativeVelocity - relativePosition / tau;
                float wLengthSq = w.sqrMagnitude;
                float dot = Vector2.Dot(w, relativePosition);

                if (dot < 0f && dot * dot > combinedRadiusSq * wLengthSq)
                    continue;

                float wLength = Mathf.Sqrt(Mathf.Max(wLengthSq, 1e-8f));
                Vector2 unitW = w / wLength;
                u = unitW * (combinedRadius / tau - wLength);
            }
            else
            {
                float invDt = 1f / Mathf.Max(Time.fixedDeltaTime, 0.02f);
                Vector2 w = relativeVelocity - relativePosition * invDt;
                float wLength = w.magnitude;
                if (wLength < 1e-5f)
                    continue;
                u = (w / wLength) * (combinedRadius * invDt - wLength);
            }

            result += 0.5f * u;
        }

        if (result.sqrMagnitude > maxSpeed * maxSpeed)
            result = result.normalized * maxSpeed;

        return result;
    }

    public static void FillAgentStates(
        DroneAI self,
        IReadOnlyList<DroneAI> neighbors,
        float neighborRadius,
        List<AgentState> buffer)
    {
        buffer.Clear();
        Vector3 p = self.transform.position;

        for (int i = 0; i < neighbors.Count; i++)
        {
            DroneAI n = neighbors[i];
            if (n == null || n == self) continue;

            Vector3 np = n.transform.position;
            if (Vector3.SqrMagnitude(np - p) > neighborRadius * neighborRadius)
                continue;

            Vector3 nv = n.SimulatedVelocity;
            buffer.Add(new AgentState
            {
                position = new Vector2(np.x, np.z),
                velocity = new Vector2(nv.x, nv.z),
                radius = n.droneSettings != null ? n.droneSettings.swarm.agentRadius : 0.6f,
            });
        }
    }
}
