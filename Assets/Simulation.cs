using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct ConnectionData
{
    public int x;
    public int y;
    public float d;
}

[System.Serializable]
public struct SpawnPoint
{
    public float x;
    public float z;
}

[RequireComponent(typeof(CameraSwitcher))]
public class Simulation : MonoBehaviour
{
    [Header("General")]
    public GameObject dronePrefab;
    public int trioCount = 5;
    public float trioDistance = 2f;
    public List<ConnectionData> connections = new List<ConnectionData>();
    public List<SpawnPoint> spawnPositions = new List<SpawnPoint>();

    [Header("Drone Settings")]
    public bool updateSettings = false;
    public DroneSettings globalDroneSettings = new DroneSettings();

    [Header("Advanced Wind & Noise")]
    public bool enableWind = true;
    public float windStrength = 5f;
    public float windScale = 0.1f; 
    public float windTimeSpeed = 0.5f; 
    public float vibrationStrength = 0.2f; // Drgania silników

    [Header("Latency Control")]
    public float globalLatency = 5f;

    private CameraSwitcher cameraSwitcher;

    DroneTrioList trios;

    void Start()
    {
        trios = new DroneTrioList(dronePrefab, trioDistance, connections, spawnPositions);
        trios.SyncDroneSettings(globalDroneSettings);

        cameraSwitcher = GetComponent<CameraSwitcher>();
        SetupCameras();
    }

    void Update()
    {
        if(updateSettings)
        {
            updateSettings = false;
            trios.SyncDroneSettings(globalDroneSettings);
        }
    }

    /*void SyncPhysicsSettings()
    {
        foreach (var trio in trios)
        {
            UpdateDroneForce(trio.a);
            UpdateDroneForce(trio.b);
            UpdateDroneForce(trio.c);
        }
    }

    void UpdateDroneForce(DroneAI drone)
    {
        if (!enableWind) {
            drone.externalForce = Vector3.zero;
            return;
        }

        // Używamy pozycji XYZ do wygenerowania unikalnego wiatru dla tego drona
        float x = drone.transform.position.x * windScale;
        float z = drone.transform.position.z * windScale;
        float t = Time.time * windTimeSpeed;

        // Generujemy szum dla każdej osi oddzielnie
        float windX = (Mathf.PerlinNoise(x + t, z) - 0.5f) * 2f;
        float windZ = (Mathf.PerlinNoise(x, z + t) - 0.5f) * 2f;
        
        Vector3 windVector = new Vector3(windX, 0, windZ) * windStrength;
        
        // Dodajemy turbulencje silnika (drgania wysokiej częstotliwości)
        Vector3 motorNoise = Random.insideUnitSphere * vibrationStrength;

        drone.externalForce = windVector + motorNoise;
    }

    public void SyncDroneSettings()
    {
        foreach (var trio in trios)
        {
            if (trio == null) continue;

            ApplyToDrone(trio.a);
            ApplyToDrone(trio.b);
            ApplyToDrone(trio.c);
        }
    }

    void ConnectTrios()
    {
        ConnectAnchors(trios[0], trios[1], 9f);
        ConnectAnchors(trios[0], trios[2], 9f);
        ConnectAnchors(trios[1], trios[2], 7f);
        ConnectAnchors(trios[1], trios[3], 9f);
        ConnectAnchors(trios[2], trios[3], 7f);
        ConnectAnchors(trios[2], trios[4], 9f);
        ConnectAnchors(trios[3], trios[4], 9f);
    }

    void ConnectAnchors(DroneTrio drt1, DroneTrio drt2, float d)
    {
        drt1.a.AddConnection(drt2.a, d);
        drt2.a.AddConnection(drt1.a, d);
    }*/

    void SetupCameras()
    {
        CameraSwitcher cs = cameraSwitcher;

        foreach (var t in trios.dl)
        {
            cs.cameras.Add(t.a.GetComponentInChildren<Camera>());
            cs.drones.Add(t.a.GetComponent<DroneMover>());
        }
    }
}