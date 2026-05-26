using UnityEngine;

namespace MuscSwarm
{
    /// <summary>
    /// Per-dronowy sampler UWB. Co <see cref="DroneSettings.uwbIntervalMs"/> wpycha do globalnej
    /// sieci <see cref="UWBTransmission"/> zaszumiony pomiar dystansu do kazdego sasiada
    /// z listy polaczen drona.
    /// </summary>
    /// <remarks>
    /// Timer startuje z losowym offsetem aby zdesynchronizowac pomiary roznych dronow
    /// (rozklada obciazenie sieci w oknie interwalu).
    /// </remarks>
    public sealed class UwbSampler
    {
        float timer;
        float intervalSeconds;
        float noiseAmplitude;

        public float Interval => intervalSeconds;

        /// <summary>Reset z losowym jitterem na start - rozprasza pomiary w roju.</summary>
        public void Reset(DroneSettings settings)
        {
            intervalSeconds = Mathf.Max(0.001f, settings.uwbIntervalMs / 1000f);
            noiseAmplitude = settings.uwbNoiseAmplitude;
            timer = Random.Range(0f, intervalSeconds);
        }

        /// <summary>
        /// Tyknij timerem. Jesli minal interwal - opublikuj swieze pomiary dla wszystkich sasiadow
        /// na liscie polaczen drona.
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

                network.PushMeasurement(self, other, noisyDistance);
            }
        }
    }
}
