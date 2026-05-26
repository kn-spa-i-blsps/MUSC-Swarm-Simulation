using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Globalny rejestr wszystkich dronow w roju - sluzy do szybkiego wyszukiwania
/// sasiadow w promieniu na potrzeby VFF i ORCA.
/// </summary>
/// <remarks>
/// Implementacja: prosta lista + linear scan. Dla setek+ dronow warto zamienic na
/// spatial hash grid; dla kilkunastu trio (kilkudziesieciu dronow) wystarczy.
/// </remarks>
public static class SwarmRegistry
{
    static readonly List<DroneAI> all = new List<DroneAI>();

    public static IReadOnlyList<DroneAI> All => all;

    /// <summary>Zarejestruj zbior dronow (np. po Start() w Simulation).</summary>
    public static void Register(IEnumerable<DroneAI> drones)
    {
        all.Clear();
        all.AddRange(drones);
    }

    /// <summary>Wyczysc rejestr (np. przy restarcie sceny).</summary>
    public static void Clear() => all.Clear();

    /// <summary>
    /// Wypelnia <paramref name="buffer"/> dronami w promieniu <paramref name="radius"/>
    /// od <paramref name="self"/>, z pominieciem sieboe. Bufor jest najpierw czyszczony.
    /// </summary>
    public static void GetNeighborsInRadius(DroneAI self, float radius, List<DroneAI> buffer)
    {
        buffer.Clear();
        Vector3 p = self.transform.position;
        float r2 = radius * radius;

        for (int i = 0; i < all.Count; i++)
        {
            DroneAI other = all[i];
            if (other == null || other == self) continue;

            if (Vector3.SqrMagnitude(other.transform.position - p) <= r2)
                buffer.Add(other);
        }
    }
}
