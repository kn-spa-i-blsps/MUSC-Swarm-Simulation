using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CameraSwitcher))]
public class Simulation : MonoBehaviour
{
    [System.Serializable]
    public class DroneTrio
    {
        public DroneAI a, b, c;

        public DroneTrio(GameObject prefab, float d, Vector3 offset, Transform parent = null)
        {
            a = Create(prefab, offset + new Vector3(0, 0, 0), parent);
            a.isAnchor = true;

            b = Create(prefab, offset + new Vector3(d, 0, 0), parent);
            c = Create(prefab, offset + new Vector3(d / 2f, 0, 0.87f * d), parent);

            ConnectInside(a, b, d);
            ConnectInside(a, c, d);
            ConnectInside(b, c, d);
        }

        DroneAI Create(GameObject prefab, Vector3 pos, Transform parent)
        {
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity, parent);
            return go.GetComponent<DroneAI>();
        }

        void ConnectInside(DroneAI x, DroneAI y, float d)
        {
            x.AddConnection(y, d);
            y.AddConnection(x, d);
        }
    }

    [Header("General")]
    public GameObject dronePrefab;
    public int trioCount = 5;
    public float trioDistance = 2f;

    [Header("Global PID Settings")]
    public bool updatePID = false;
    public float globalP = 100f;
    public float globalI = 0.0f;
    public float globalD = 15f;
    public float derivativeSmoothing = 0.2f;
    public int globalSubSteps = 20;
    
    [Header("Global Physics")]
    public float globalMoveSpeed = 1f;
    [Range(0f, 1f)]
    public float globalDrag = 0.95f;

    [Header("Autospawn")]
    public bool autoSpawnOn = false;
    public float trioAutoSpawnSpacing = 10f;

    [Header("Advanced Wind & Noise")]
    public bool enableWind = true;
    public float windStrength = 5f;
    public float windScale = 0.1f; 
    public float windTimeSpeed = 0.5f; 
    public float vibrationStrength = 0.2f; // Drgania silników

    [Header("Latency Control")]
    public float globalLatency = 5f;

    private CameraSwitcher cameraSwitcher;

    // 🔹 TRIO SYSTEM (ręcznie kontrolowany)
    private List<DroneTrio> trios = new List<DroneTrio>();

    void Start()
    {
        cameraSwitcher = GetComponent<CameraSwitcher>();

        if(autoSpawnOn) AutoSpawnTrios();
        else SpawnTrios();
        SyncDroneSettings();
        ConnectTrios();
        SetupCameras();
    }

    void Update()
    {
        if(updatePID)
        {
            updatePID = false;
            SyncDroneSettings();
        }

        SyncPhysicsSettings();
    }

    void SpawnTrios()
    {
        Vector2[] trioPositions = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(-3, -8),
            new Vector2(3, -8),
            new Vector2(0, -16),
            new Vector2(6, -14),
        };

        trios.Clear(); // tylko czyszczenie starej symulacji

        for (int i = 0; i < trioPositions.Length; i++)
        {
            Vector3 pos = new Vector3(trioPositions[i].x, 0f, trioPositions[i].y);

            DroneTrio t = new DroneTrio(dronePrefab, trioDistance, pos, transform);

            trios.Add(t); // 🔥 TO ZOSTAJE
        }
    }

    void AutoSpawnTrios()
    {
        int rowSize = Mathf.CeilToInt(Mathf.Sqrt(trioCount));

        for (int i = 0; i < trioCount; i++)
        {
            int x = i % rowSize;
            int z = i / rowSize;

            Vector3 offset = new Vector3(x * trioAutoSpawnSpacing, 0, z * trioAutoSpawnSpacing);

            DroneTrio t = new DroneTrio(dronePrefab, trioDistance, offset, transform);
            trios.Add(t);
        }
    }

    void SyncPhysicsSettings()
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
        drone.latencyFrames = (int)globalLatency;
        drone.derivativeSmoothing = derivativeSmoothing;

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
        // Sprawdzamy czy lista nie jest pusta (np. przed startem symulacji)
        if (trios == null || trios.Count == 0) return;

        foreach (var trio in trios)
        {
            if (trio == null) continue;

            ApplyToDrone(trio.a);
            ApplyToDrone(trio.b);
            ApplyToDrone(trio.c);
        }
    }

    void ApplyToDrone(DroneAI d)
    {
        if (d == null) return;
        d.P = globalP;
        d.I = globalI;
        d.D = globalD;
        d.derivativeSmoothing = derivativeSmoothing;
        d.moveSpeed = globalMoveSpeed;
        d.drag = globalDrag;
        d.pidSubSteps = globalSubSteps;
        d.latencyFrames = (int)globalLatency;
    }

    void ConnectTrios()
    {
        // 🔹 ręczne połączenia między TRIO

        /*
        for (int i = 0; i < trios.Count - 1; i++)
        {
            ConnectAnchors(trios[i].a, trios[i + 1].a, d);
        }   
        */

        ConnectAnchors(trios[0], trios[1], 9f);
        ConnectAnchors(trios[0], trios[2], 9f);
        ConnectAnchors(trios[1], trios[2], 7f);
        ConnectAnchors(trios[1], trios[3], 9f);
        ConnectAnchors(trios[2], trios[3], 7f);
        ConnectAnchors(trios[2], trios[4], 9f);
        ConnectAnchors(trios[3], trios[4], 9f);

        // możesz dodawać dowolne grafy między trio
    }

    void ConnectAnchors(DroneTrio drt1, DroneTrio drt2, float d)
    {
        drt1.a.AddConnection(drt2.a, d);
        drt2.a.AddConnection(drt1.a, d);
    }

    void SetupCameras()
    {
        CameraSwitcher cs = cameraSwitcher;

        foreach (var t in trios)
        {
            cs.cameras.Add(t.a.GetComponentInChildren<Camera>());
            cs.drones.Add(t.a.GetComponent<DroneMover>());
        }
    }
}