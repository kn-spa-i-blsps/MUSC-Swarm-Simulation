using UnityEngine;

namespace MuscSwarm
{
    /// <summary>
    /// Dodatkowa "sprezyna" utrzymujaca zadane dystanse z listy <see cref="DroneAI.connections"/>
    /// (trojkat trio + zadane dystanse miedzy kotwicami roznych trio) w trybie VffOrca.
    /// </summary>
    /// <remarks>
    /// W przeciwienstwie do VFF cohesion/separation (ktore dzialaja tylko w promieniu
    /// <see cref="SwarmSteeringSettings.perceptionRadius"/>/<see cref="SwarmSteeringSettings.separationRadius"/>),
    /// ta sprezyna dziala NIEZALEZNIE od odleglosci - polaczenie jest trzymane nawet jesli
    /// sasiad jest daleko poza zasiegiem VFF (np. kotwice dwoch trio oddalone o wiecej niz perceptionRadius).
    ///
    /// Kierunek liczony z realnej pozycji (ground truth) - jak w <see cref="FormationPid"/>, zakladamy
    /// ze "gdzie w przyblizeniu jest sasiad" wynika z innych czujnikow, a UWB dostarcza tylko precyzyjny
    /// DYSTANS. Sam dystans, jesli <see cref="SwarmSteeringSettings.useUwbNeighborSensing"/> jest wlaczone,
    /// pochodzi z tego samego (zaszumionego, z lagiem) kanalu UWB co w PidSpring.
    ///
    /// WAZNE: zwraca realna korekte predkosci (jednostki/s), NIE jednostkowy "glos kierunkowy".
    /// Dlatego jest dodawana do bezpiecznej predkosci PO ORCA (patrz <see cref="SwarmSteeringPipeline"/>),
    /// a nie wrzucana do sumy VFF, ktora i tak zawsze normalizuje sie do pelnej maxSpeed - gdyby
    /// wrzucic ja tam, dron pedzilby z pelna sila nawet przy malym bledzie, przestrzeliwal cel
    /// i oscylowal. Ma czlon P (proporcjonalny do bledu dystansu) i D (tlumienie predkosci
    /// zbliżania/oddalania), czyli prawdziwa sprezyna-tlumik, nie sam P.
    /// </remarks>
    public static class ConnectionSpring
    {
        public static Vector3 ComputeVelocityCorrection(DroneAI self, DroneSettings tuning, UWBTransmission network)
        {
            SwarmSteeringSettings s = tuning.swarm;
            if (s.connectionWeight <= 0f) return Vector3.zero;

            Vector3 selfPos = self.transform.position;
            Vector3 selfVel = self.SimulatedVelocity;
            Vector3 correction = Vector3.zero;
            var connections = self.connections;

            for (int i = 0; i < connections.Count; i++)
            {
                Connection conn = connections[i];
                DroneAI target = conn.target;
                if (target == null) continue;

                if (conn.weight <= 0f) continue;

                Vector3 toTarget = target.transform.position - selfPos;
                float realDist = toTarget.magnitude;
                if (realDist < 1e-4f) continue;
                Vector3 dir = toTarget / realDist;

                float dist = realDist;
                if (s.useUwbNeighborSensing && network != null &&
                    network.TryGetDistance(self, target, out UWBTransmission.MeasurementData measurement))
                {
                    dist = measurement.distance;
                }

                float error = dist - conn.desiredDistance;

                // D: predkosc "rozjezdzania sie" wzdluz osi polaczenia (dodatnia = oddalaja sie).
                float closingSpeed = Vector3.Dot(target.SimulatedVelocity - selfVel, dir);

                float pTerm = error * s.connectionWeight * conn.weight;
                float dTerm = closingSpeed * s.connectionDamping * conn.weight;

                correction += dir * (pTerm + dTerm);
            }

            correction.y = 0f;
            return correction;
        }
    }
}
