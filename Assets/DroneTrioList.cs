using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zbior wszystkich trio w roju + ich miedzytrio polaczenia. Tworzy je z prefabu
/// i list <see cref="SpawnPoint"/> oraz <see cref="ConnectionData"/> dostarczonych przez <see cref="Simulation"/>.
/// </summary>
public class DroneTrioList
{
    /// <summary>Wszystkie trio w roju.</summary>
    public readonly List<DroneTrio> dl = new List<DroneTrio>();

    public DroneTrioList(
        GameObject dronePrefab,
        float trioDistance,
        List<ConnectionData> connections,
        List<SpawnPoint> spawnPositions)
    {
        for (int i = 0; i < spawnPositions.Count; i++)
        {
            Vector3 pos = new Vector3(spawnPositions[i].x, 0f, spawnPositions[i].z);
            dl.Add(new DroneTrio(dronePrefab, trioDistance, pos));
        }

        for (int i = 0; i < connections.Count; i++)
        {
            ConnectionData cd = connections[i];
            // Walidacja indeksow (1-based). Pomijaj wpisy spoza zakresu, ale loguj zeby user widzial.
            if (cd.x < 1 || cd.x > dl.Count || cd.y < 1 || cd.y > dl.Count)
            {
                Debug.LogWarning($"[DroneTrioList] Connection[{i}] indices ({cd.x},{cd.y}) out of range [1..{dl.Count}], pomijam.");
                continue;
            }
            ConnectTrios(dl[cd.x - 1], dl[cd.y - 1], cd.d);
        }
    }

    static void ConnectTrios(DroneTrio x, DroneTrio y, float distance)
    {
        // Lacz kotwice z kotwica.
        x.a.AddConnection(y.a, distance);
        y.a.AddConnection(x.a, distance);
    }

    /// <summary>Wstrzyk profilu dostrojenia do wszystkich dronow we wszystkich trio.</summary>
    public void SyncDroneSettings(DroneSettings settings)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DroneTrio t = dl[i];
            t.a.droneSettings = settings;
            t.b.droneSettings = settings;
            t.c.droneSettings = settings;
        }
    }
}
