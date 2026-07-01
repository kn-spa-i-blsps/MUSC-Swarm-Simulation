using UnityEngine;

namespace MuscSwarm
{
    /// <summary>
    /// Zrzut stanu sasiada uzywany przez VFF/ORCA do liczenia steeringu.
    /// Pozycja/predkosc moga pochodzic z ground truth (idealna znajomosc, "boska" wiedza)
    /// albo z symulowanego UWB radiobroadcastu (szum + lag transmisji, jak w PidSpring) -
    /// w zaleznosci od <see cref="SwarmSteeringSettings.useUwbNeighborSensing"/>.
    /// </summary>
    public readonly struct NeighborSample
    {
        public readonly DroneAI drone;
        public readonly Vector3 position;
        public readonly Vector3 velocity;

        public NeighborSample(DroneAI drone, Vector3 position, Vector3 velocity)
        {
            this.drone = drone;
            this.position = position;
            this.velocity = velocity;
        }
    }
}
