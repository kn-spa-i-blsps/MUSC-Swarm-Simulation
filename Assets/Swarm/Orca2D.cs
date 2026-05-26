using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uproszczony solver "ORCA-like" w plaszczyznie XZ.
/// </summary>
/// <remarks>
/// To NIE jest pelna implementacja ORCA z linearnym programowaniem na polplaszczyznach.
/// Dla kazdego sasiada liczona jest reciprokalna korekta predkosci u i akumulowana jako
/// <c>result += 0.5 * u</c> (czyli polowa "kary" przypada na nas, polowa na sasiada).
/// To uproszczenie dziala dla rzadkich rojow z umiarkowanymi predkosciami.
/// Dla gestych scenariuszy zastap pelnym solverem (np. Berg/Snape/Manocha).
/// </remarks>
public static class Orca2D
{
    /// <summary>Stan agenta dla solvera (rzut na XZ).</summary>
    public struct AgentState
    {
        public Vector2 position;
        public Vector2 velocity;
        public float radius;
    }

    /// <summary>
    /// Liczy bezpieczna predkosc startujac od <paramref name="preferredVelocity"/> i poprawiajac ja
    /// na podstawie pozycji/predkosci sasiadow.
    /// </summary>
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
            Vector2 relPos = other.position - position;
            Vector2 relVel = result - other.velocity;
            float combinedRadius = radius + other.radius;
            float distSq = relPos.sqrMagnitude;
            float combinedRSq = combinedRadius * combinedRadius;

            Vector2 u;
            if (distSq > combinedRSq)
            {
                // Outside collision range - standard ORCA formulation.
                Vector2 w = relVel - relPos / tau;
                float wLengthSq = w.sqrMagnitude;
                float dot = Vector2.Dot(w, relPos);

                // No imminent collision in time horizon.
                if (dot < 0f && dot * dot > combinedRSq * wLengthSq) continue;

                float wLength = Mathf.Sqrt(Mathf.Max(wLengthSq, 1e-8f));
                Vector2 unitW = w / wLength;
                u = unitW * (combinedRadius / tau - wLength);
            }
            else
            {
                // Already overlapping - eject using inverse-dt as horizon.
                float invDt = 1f / Mathf.Max(Time.fixedDeltaTime, 0.02f);
                Vector2 w = relVel - relPos * invDt;
                float wLength = w.magnitude;
                if (wLength < 1e-5f) continue;
                u = (w / wLength) * (combinedRadius * invDt - wLength);
            }

            // Reciprocal weighting (po polowie korekty na obie strony).
            result += 0.5f * u;
        }

        if (result.sqrMagnitude > maxSpeed * maxSpeed)
            result = result.normalized * maxSpeed;

        return result;
    }

    /// <summary>
    /// Wypelnia <paramref name="buffer"/> stanami sasiadow do solver'a (rzut na XZ).
    /// </summary>
    public static void FillAgentStates(
        DroneAI self,
        IReadOnlyList<DroneAI> neighbors,
        float neighborRadius,
        List<AgentState> buffer)
    {
        buffer.Clear();
        Vector3 p = self.transform.position;
        float r2 = neighborRadius * neighborRadius;

        for (int i = 0; i < neighbors.Count; i++)
        {
            DroneAI n = neighbors[i];
            if (n == null || n == self) continue;

            Vector3 np = n.transform.position;
            if (Vector3.SqrMagnitude(np - p) > r2) continue;

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
