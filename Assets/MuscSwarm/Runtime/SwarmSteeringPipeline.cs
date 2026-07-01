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
        readonly List<DroneAI> discoveredBuffer = new List<DroneAI>(32);
        readonly List<NeighborSample> neighborSamples = new List<NeighborSample>(32);
        readonly List<Orca2D.AgentState> orcaBuffer = new List<Orca2D.AgentState>(32);

        // Cache wynikow dla debugowania (rysowanie wektorow).
        public Vector3 PreferredVelocity { get; private set; }
        public Vector3 SafeVelocity { get; private set; }

        /// <summary>Ile z ostatnio uzytych sasiadow mialo pozycje z UWB (a nie ground truth) - diagnostyka.</summary>
        public int NeighborsFromUwb { get; private set; }

        /// <summary>
        /// Wylicza bezpieczna predkosc na biezacy krok fizyki.
        /// </summary>
        /// <param name="network">
        /// Wspolna siec UWB - uzywana do rozwiazania pozycji/predkosci sasiadow gdy
        /// <see cref="SwarmSteeringSettings.useUwbNeighborSensing"/> jest wlaczone. Moze byc null
        /// (wtedy zawsze ground truth, tak jak w oryginalnej, "idealnej" wersji).
        /// </param>
        public Vector3 ComputeSafeVelocity(
            DroneAI self,
            Vector3 missionGhost,
            Vector3 currentVelocity,
            DroneSettings tuning,
            UWBTransmission network)
        {
            SwarmSteeringSettings swarm = tuning.swarm;

            // --- Odkrywanie "kto jest w pobliżu" zawsze po ground truth (fizyczny zasieg radia/wzroku).
            // To co "gdzie dokladnie sasiad jest" rozwiazujemy pozniej - albo ground truth, albo UWB.
            float discoveryRadius = Mathf.Max(swarm.perceptionRadius, swarm.orcaNeighborRadius);
            SwarmRegistry.GetNeighborsInRadius(self, discoveryRadius, discoveredBuffer);
            BuildNeighborSamples(self, network, swarm.useUwbNeighborSensing, discoveredBuffer, neighborSamples);

            // --- VFF: preferred velocity --------------------------------------------------
            Vector3 preferred = VffSteering.ComputePreferredVelocity(
                self,
                neighborSamples,
                missionGhost,
                swarm,
                tuning.horizontalSpeed);
            PreferredVelocity = preferred;

            // --- ORCA: safe velocity ------------------------------------------------------
            Orca2D.FillAgentStates(self, neighborSamples, swarm.orcaNeighborRadius, orcaBuffer);

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

            // --- Connection spring: trzyma zadane dystanse z Connections (trojkat trio +
            // polaczenia miedzy-trio) NIEZALEZNIE od perceptionRadius - patrz ConnectionSpring.
            // Dodawana PO ORCA jako realna korekta predkosci (P+D), nie jako "glos" w znormalizowanej
            // sumie VFF - dzieki temu naturalnie zwalnia w okolicy zadanego dystansu, bez oscylacji/przestrzelen.
            Vector3 connectionCorrection = ConnectionSpring.ComputeVelocityCorrection(self, tuning, network);
            Vector2 corrected = safe2 + new Vector2(connectionCorrection.x, connectionCorrection.z);

            float maxSpeedSq = tuning.horizontalSpeed * tuning.horizontalSpeed;
            if (corrected.sqrMagnitude > maxSpeedSq)
                corrected = corrected.normalized * tuning.horizontalSpeed;

            Vector3 safe = new Vector3(corrected.x, 0f, corrected.y);
            SafeVelocity = safe;
            return safe;
        }

        /// <summary>
        /// Rozwiazuje pozycje/predkosci odkrytych sasiadow: UWB (szum+lag) jesli wlaczone i dostepne,
        /// inaczej ground truth (rowniez jako naturalny fallback przy braku jeszcze odebranego broadcastu).
        /// </summary>
        void BuildNeighborSamples(
            DroneAI self,
            UWBTransmission network,
            bool useUwb,
            List<DroneAI> discovered,
            List<NeighborSample> output)
        {
            output.Clear();
            int fromUwb = 0;

            for (int i = 0; i < discovered.Count; i++)
            {
                DroneAI n = discovered[i];
                if (n == null || n == self) continue;

                Vector3 pos = n.transform.position;
                Vector3 vel = n.SimulatedVelocity;

                if (useUwb && network != null && network.TryGetPose(n, out UWBTransmission.PoseData pose))
                {
                    pos = pose.position;
                    vel = pose.velocity;
                    fromUwb++;
                }

                output.Add(new NeighborSample(n, pos, vel));
            }

            NeighborsFromUwb = fromUwb;
        }
    }
}
