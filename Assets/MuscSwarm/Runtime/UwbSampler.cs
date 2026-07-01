using UnityEngine;

namespace MuscSwarm
{
    /// <summary>
    /// Per-dronowy sampler UWB. Co <see cref="DroneSettings.uwbIntervalMs"/> wysyla do globalnej
    /// sieci <see cref="UWBTransmission"/>:
    ///   1) zaszumiony pomiar dystansu do kazdego sasiada z listy polaczen drona (kanal par-do-par,
    ///      konsumowany przez <see cref="FormationPid"/> w trybie PidSpring),
    ///   2) zaszumiony broadcast wlasnej pozycji+predkosci (kanal rozgloszeniowy, konsumowany
    ///      opcjonalnie przez <see cref="SwarmSteeringPipeline"/> w trybie VffOrca,
    ///      patrz <see cref="SwarmSteeringSettings.useUwbNeighborSensing"/>).
    /// Oba kanaly dostaja sie do odbiorcow z prawdziwym opoznieniem transmisji
    /// (<see cref="DroneSettings.uwbTransmissionDelayMs"/>), a nie natychmiast.
    /// </summary>
    /// <remarks>
    /// Timer startuje z losowym offsetem aby zdesynchronizowac pomiary roznych dronow
    /// (rozklada obciazenie sieci w oknie interwalu). Interwal probkowania i lag transmisji
    /// to dwa niezalezne zrodla opoznienia ktore sie sumuja.
    /// </remarks>
    public sealed class UwbSampler
    {
        float timer;
        float intervalSeconds;
        float noiseAmplitude;
        float transmissionDelaySeconds;

        public float Interval => intervalSeconds;
        public float TransmissionDelay => transmissionDelaySeconds;

        /// <summary>Reset z losowym jitterem na start - rozprasza pomiary w roju.</summary>
        public void Reset(DroneSettings settings)
        {
            intervalSeconds = Mathf.Max(0.001f, settings.uwbIntervalMs / 1000f);
            noiseAmplitude = settings.uwbNoiseAmplitude;
            transmissionDelaySeconds = Mathf.Max(0f, settings.uwbTransmissionDelayMs / 1000f);
            timer = Random.Range(0f, intervalSeconds);
        }

        /// <summary>
        /// Tyknij timerem. Jesli minal interwal - opublikuj swieze pomiary dystansu dla wszystkich
        /// sasiadow na liscie polaczen drona ORAZ broadcast wlasnej (zaszumionej) pozycji+predkosci.
        /// </summary>
        public void Tick(
            float deltaTime,
            DroneAI self,
            UWBTransmission network)
        {
            if (network == null) return;

            timer += deltaTime;
            if (timer < intervalSeconds) return;
            timer = 0f;

            Vector3 selfPos = self.transform.position;
            var connections = self.connections;
            for (int i = 0; i < connections.Count; i++)
            {
                DroneAI other = connections[i].target;
                if (other == null) continue;

                float realDistance = Vector3.Distance(selfPos, other.transform.position);
                float noisyDistance = realDistance + Random.Range(-noiseAmplitude, noiseAmplitude);

                network.PushMeasurement(self, other, noisyDistance, transmissionDelaySeconds);
            }

            // Broadcast wlasnej pozycji - uzywane opcjonalnie przez VffOrca (patrz SwarmSteeringPipeline).
            // Ten sam kanal radiowy, wiec ten sam szum i lag transmisji jak przy rangingu dystansow.
            Vector3 positionNoise = new Vector3(
                Random.Range(-noiseAmplitude, noiseAmplitude),
                Random.Range(-noiseAmplitude, noiseAmplitude),
                Random.Range(-noiseAmplitude, noiseAmplitude));
            network.BroadcastPose(self, selfPos + positionNoise, self.SimulatedVelocity, transmissionDelaySeconds);
        }
    }
}
