using System.Collections.Generic;
using UnityEngine;

namespace MuscSwarm
{
    /// <summary>
    /// Kompozycja steering dla trybu VffOrca:
    ///   1. VFF (Reynolds boids) liczy "preferowana" predkosc - separation + alignment + cohesion + goal.
    ///   2. ORCA bierze ja jako input i poprawia rezultat tak, zeby uniknac kolizji.
    /// Wynik: bezpieczna predkosc, ktora dron stosuje.
    /// </summary>
    public sealed class SwarmSteeringPipeline
    {
        readonly List<DroneAI> neighborBuffer = new List<DroneAI>(32);
        readonly List<Orca2D.AgentState> orcaBuffer = new List<Orca2D.AgentState>(32);

        // Cache wynikow dla debugowania (rysowanie wektorow).
        public Vector3 PreferredVelocity { get; private set; }
        public Vector3 SafeVelocity { get; private set; }

        /// <summary>
        /// Wylicza bezpieczna predkosc na biezacy krok fizyki.
        /// </summary>
        public Vector3 ComputeSafeVelocity(
            DroneAI self,
            Vector3 missionGhost,
            Vector3 currentVelocity,
            DroneSettings tuning)
        {
            SwarmSteeringSettings swarm = tuning.swarm;

            // --- VFF: preferred velocity --------------------------------------------------
            SwarmRegistry.GetNeighborsInRadius(self, swarm.perceptionRadius, neighborBuffer);
            Vector3 preferred = VffSteering.ComputePreferredVelocity(
                self,
                neighborBuffer,
                missionGhost,
                swarm,
                tuning.horizontalSpeed);
            PreferredVelocity = preferred;

            // --- ORCA: safe velocity ------------------------------------------------------
            Orca2D.FillAgentStates(self, SwarmRegistry.All, swarm.orcaNeighborRadius, orcaBuffer);

            Vector3 selfPos = self.transform.position;
            Vector2 pos2 = new Vector2(selfPos.x, selfPos.z);
            Vector2 vel2 = new Vector2(currentVelocity.x, currentVelocity.z);
            Vector2 pref2 = new Vector2(preferred.x, preferred.z);

            Vector2 safe2 = Orca2D.ComputeSafeVelocity(
                pos2,
                vel2,
                swarm.agentRadius,
                orcaBuffer,
                swarm.orcaTimeHorizon,
                pref2,
                tuning.horizontalSpeed);

            Vector3 safe = new Vector3(safe2.x, 0f, safe2.y);
            SafeVelocity = safe;
            return safe;
        }
    }
}
