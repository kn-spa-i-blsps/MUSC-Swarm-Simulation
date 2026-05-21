// [INFO]
// W przeliczeniu na rozmiar drona (jesli mialby byc 3,5") - jeden REALNY metr to 6 jednostek w Unity

using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

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
    public int droneDebugNumber = 0;
    public bool debugForces = false;
    public bool debugPosition = false;
    public int debugInterval = 100; // Co ile milisekund wyrzucić log do konsoli
    private float lastDebugTime = 0f;
    private DroneAI lastDebuggedDrone;
    public bool updateDroneSettings = false;
    public DroneSettings globalDroneSettings = new DroneSettings();

    [Header("Advanced Wind & Noise")]
    public bool enableWind = true;
    public float windStrength = 5f;
    public float windScale = 0.1f; 
    public float windTimeSpeed = 0.5f; 
    public float vibrationStrength = 0.2f; // Drgania silników

    private CameraSwitcher cameraSwitcher;

    DroneTrioList trios;
    private List<DroneAI> allDrones = new List<DroneAI>(); // Płaska lista dla szybkiego dostępu

    void Start()
    {
        trios = new DroneTrioList(dronePrefab, trioDistance, connections, spawnPositions);
        trios.SyncDroneSettings(globalDroneSettings);

        allDrones.Clear();
        foreach (var trio in trios.dl)
        {
            allDrones.Add(trio.a);
            allDrones.Add(trio.b);
            allDrones.Add(trio.c);
        }

        SwarmRegistry.Register(allDrones);

        cameraSwitcher = GetComponent<CameraSwitcher>();
        SetupCameras();
    }

    void FixedUpdate()
    {
        // Sprawdzamy czy czas na kolejną porcję debugu
        if (Time.fixedTime >= lastDebugTime + debugInterval / 1000f)
        {
            HandleDronesDebug();
            lastDebugTime = Time.fixedTime;
        }

        trios.SyncDroneSettings(globalDroneSettings);
        /*if(updateDroneSettings)
        {
            trios.SyncDroneSettings(globalDroneSettings);
            updateDroneSettings = false;
        }*/
        // Tutaj opcjonalnie wywołaj SyncPhysicsSettings() jeśli go odkomentujesz
    }

    void HandleDronesDebug()
    {
        // 1. Sprawdzamy zakres
        if (droneDebugNumber >= 0 && droneDebugNumber < allDrones.Count)
        {
            var currentDrone = allDrones[droneDebugNumber];

            // 2. Jeśli zmienił się debugowany dron, wyłącz flagę u starego
            if (lastDebuggedDrone != null && lastDebuggedDrone != currentDrone)
            {
                lastDebuggedDrone.debugEnabled = false;
            }

            if (currentDrone == null) return;

            // 3. Aktywujemy debug u aktualnego drona
            // Robimy to co interwał, ale flaga może być ustawiona na stałe
            currentDrone.debugEnabled = (debugForces || debugPosition);
            lastDebuggedDrone = currentDrone;

            // 4. Wywołujemy logi w konsoli
            if (debugForces) currentDrone.DebugForces(droneDebugNumber);
            if (debugPosition) currentDrone.DebugPosition(droneDebugNumber);
        }
        else if (lastDebuggedDrone != null)
        {
            // Jeśli wyjdziemy poza zakres (np. wpiszesz -1), wyłącz debug u ostatniego
            lastDebuggedDrone.debugEnabled = false;
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

    void DebugTrio(int n)
    {
        var drone = trios.dl[n].a;
        Vector3 pos = drone.transform.position;
        float timeMs = Time.fixedTime * 1000f;

        // Formatowanie: czas w kolorze żółtym, pozycja z ograniczonymi miejscami po przecinku
        Debug.Log($"[<color=yellow>{timeMs:F0} ms</color>] Drone <color=cyan>#{n}</color> | " +
                $"Pos: (<b>{pos.x:F2}</b>, <b>{pos.y:F2}</b>, <b>{pos.z:F2}</b>)");
    }
}