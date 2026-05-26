using UnityEngine;

namespace MuscSwarm
{
    /// <summary>
    /// Siła odpychajaca od przeszkod zarejestrowanych w <see cref="ObstacleRegistry"/>.
    /// Soft repulsion - dziala w plaszczyznie XZ, z kwadratowym falloffem w strefie sensowania.
    /// </summary>
    public static class ObstacleAvoidance
    {
        /// <summary>
        /// Liczy wektor odpychania dla drona w danej pozycji od wszystkich przeszkod w zasiegu.
        /// </summary>
        /// <param name="dronePosition">Pozycja drona w world space.</param>
        /// <param name="droneAvoidRadius">"Bufor bezpieczenstwa" wokol drona (dodawany do promienia przeszkody).</param>
        /// <param name="sensingMultiplier">Mnoznik zasiegu sensowania (1 = sila zaczyna sie na styk z radius; 1.5 = czuje juz 50% poza radius).</param>
        /// <param name="globalStrength">Globalny mnoznik z DroneSettings.</param>
        /// <returns>Wektor sily odpychajacej (w plaszczyznie XZ). Magnitude moze byc duza dla bliskich przeszkod.</returns>
        public static Vector3 ComputeRepulsion(
            Vector3 dronePosition,
            float droneAvoidRadius,
            float sensingMultiplier,
            float globalStrength)
        {
            if (globalStrength <= 0f) return Vector3.zero;

            Vector3 total = Vector3.zero;
            var obstacles = ObstacleRegistry.All;

            for (int i = 0; i < obstacles.Count; i++)
            {
                Obstacle o = obstacles[i];
                if (o == null) continue;

                Vector3 away = dronePosition - o.transform.position;
                away.y = 0f;
                float dist = away.magnitude;
                if (dist < 1e-4f)
                {
                    // Stoimy idealnie w srodku - wypchnij w losowym kierunku.
                    away = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                    dist = 0.01f;
                }

                float safeDist = (o.radius + droneAvoidRadius) * Mathf.Max(1f, sensingMultiplier);
                if (dist > safeDist) continue;

                // Kwadratowy falloff: dla dist = safeDist sila = 0, dla dist <= radius sila = max.
                float t = 1f - (dist / safeDist);     // 0..1
                float magnitude = t * t * o.repulsionStrength * globalStrength;

                total += (away / dist) * magnitude;
            }

            return total;
        }
    }
}
