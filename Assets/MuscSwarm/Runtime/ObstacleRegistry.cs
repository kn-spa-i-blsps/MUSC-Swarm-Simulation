using System.Collections.Generic;

namespace MuscSwarm
{
    /// <summary>
    /// Globalny rejestr przeszkod w scenie. Kazda <see cref="Obstacle"/> auto-rejestruje
    /// sie w OnEnable i wyrejestrowuje w OnDisable.
    /// </summary>
    public static class ObstacleRegistry
    {
        static readonly List<Obstacle> all = new List<Obstacle>();

        public static IReadOnlyList<Obstacle> All => all;

        public static void Register(Obstacle obstacle)
        {
            if (obstacle == null || all.Contains(obstacle)) return;
            all.Add(obstacle);
        }

        public static void Unregister(Obstacle obstacle)
        {
            if (obstacle == null) return;
            all.Remove(obstacle);
        }

        public static void Clear() => all.Clear();
    }
}
