using UnityEngine;
using System.Collections.Generic;

public class DroneTrioList
{
    public List<DroneTrio> dl = new List<DroneTrio>();

    public DroneTrioList(GameObject dronePrefab, float trioDistance, List<ConnectionData> connections, List<SpawnPoint> spawnPositions)
    {
        for (int i = 0; i < spawnPositions.Count; i++)
        {
            Vector3 position = new Vector3(spawnPositions[i].x, 0f, spawnPositions[i].z);
            DroneTrio t = new DroneTrio(dronePrefab, trioDistance, position);

            dl.Add(t);
        }

        for (int i = 0; i < connections.Count; i++)
        {
            if (1 <= connections[i].x && connections[i].x <= dl.Count && 1 <= connections[i].y && connections[i].y <= dl.Count)
            ConnectTrios(dl[connections[i].x-1], dl[connections[i].y-1], connections[i].d);
        }
    }

    void ConnectTrios(DroneTrio x, DroneTrio y, float d)
    {
        x.a.AddConnection(y.a, d);
        y.a.AddConnection(x.a, d);
    }

    public void SyncDroneSettings(DroneSettings droneSettings)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            dl[i].a.droneSettings = droneSettings;
            dl[i].b.droneSettings = droneSettings;
            dl[i].c.droneSettings = droneSettings;
        }
    }
}
