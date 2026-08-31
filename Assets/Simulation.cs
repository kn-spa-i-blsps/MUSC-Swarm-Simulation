using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Scene bootstrap for the MUSC swarm on branch <c>main</c>.
/// Instantiates drone trios, wires the formation graph, pushes global PID/physics
/// onto every <see cref="DroneAI"/>, and registers each trio's anchor with
/// <see cref="CameraSwitcher"/>. Runtime motion lives in DroneAI / DroneMover — this
/// class does not steer drones itself.
/// </summary>
/// <remarks>
/// Scale used on later branches: ~6 Unity units = 1 real metre (3.5" drone).
/// This file still treats distances as raw Unity units.
/// </remarks>
[RequireComponent(typeof(CameraSwitcher))]
public class Simulation : MonoBehaviour
{
    /// <summary>
    /// One rigid equilateral triangle of three drones. Drone <c>a</c> is the
    /// formation anchor (only it is linked to other trios in <see cref="ConnectTrios"/>).
    /// Internal edges are bidirectional springs of length <paramref name="d"/>.
    /// </summary>
    [System.Serializable]
    public class DroneTrio
    {
        public DroneAI a, b, c;

        public DroneTrio(GameObject prefab, float d, Vector3 offset, Transform parent = null)
        {
            a = Create(prefab, offset + new Vector3(0, 0, 0), parent);
            a.isAnchor = true;

            b = Create(prefab, offset + new Vector3(d, 0, 0), parent);
            // 0.87 ≈ √3/2: third vertex of an equilateral triangle of side d on XZ.
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

        // Formation springs are undirected: both drones must hold the same desiredDistance.
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
    public float vibrationStrength = 0.2f; // high-frequency motor shake added to externalForce

    [Header("Latency Control")]
    // Copied to DroneAI.latencyFrames: UWB sample delay in physics frames, not milliseconds.
    public float globalLatency = 5f;

    private CameraSwitcher cameraSwitcher;

    private List<DroneTrio> trios = new List<DroneTrio>();

    void Start()
    {
        cameraSwitcher = GetComponent<CameraSwitcher>();

        if(autoSpawnOn) AutoSpawnTrios();
        else SpawnTrios();
        SyncDroneSettings();
        // Inter-trio edges are hardcoded for five trios (see ConnectTrios). Auto-spawn
        // still calls this, so trioCount must stay 5 or the graph will throw / be wrong.
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

        // Wind / latency are live every frame; PID gains only refresh when updatePID is ticked.
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

        trios.Clear();

        for (int i = 0; i < trioPositions.Length; i++)
        {
            Vector3 pos = new Vector3(trioPositions[i].x, 0f, trioPositions[i].y);

            DroneTrio t = new DroneTrio(dronePrefab, trioDistance, pos, transform);

            trios.Add(t);
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

        // Spatial Perlin so neighbouring drones share similar wind, not identical gusts.
        float x = drone.transform.position.x * windScale;
        float z = drone.transform.position.z * windScale;
        float t = Time.time * windTimeSpeed;

        float windX = (Mathf.PerlinNoise(x + t, z) - 0.5f) * 2f;
        float windZ = (Mathf.PerlinNoise(x, z + t) - 0.5f) * 2f;
        
        Vector3 windVector = new Vector3(windX, 0, windZ) * windStrength;
        
        Vector3 motorNoise = Random.insideUnitSphere * vibrationStrength;

        drone.externalForce = windVector + motorNoise;
    }

    public void SyncDroneSettings()
    {
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

    /// <summary>
    /// Wires mother↔mother edges for the five hardcoded spawn points in
    /// <see cref="SpawnTrios"/>. Rest lengths 7 vs 9 are design values, not measured
    /// spawn spacing. On this branch those edges do not produce PID force (see
    /// <see cref="DroneAI"/>); they only affect the lone-anchor freeze check.
    /// </summary>
    void ConnectTrios()
    {
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
    }

    void ConnectAnchors(DroneTrio drt1, DroneTrio drt2, float d)
    {
        drt1.a.AddConnection(drt2.a, d);
        drt2.a.AddConnection(drt1.a, d);
    }

    // Only anchors get a follow-cam / manual takeover. Children stay on DroneAI.
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