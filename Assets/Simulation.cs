// [INFO] Skala: w przeliczeniu na rozmiar drona 3,5" jeden realny metr to ~6 jednostek Unity.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Polaczenie miedzy dwoma trio (indeksy 1-based) z zadanym dystansem miedzy kotwicami.
/// </summary>
[System.Serializable]
public struct ConnectionData
{
    [Tooltip("Indeks trio A (1-based, indeksuje listę spawnPositions).")]
    public int x;

    [Tooltip("Indeks trio B (1-based).")]
    public int y;

    [Tooltip("Zadany dystans miedzy kotwicami trio A i B (jednostki Unity).")]
    public float d;
}

/// <summary>Pozycja spawnu kotwicy trio na plaszczyznie XZ.</summary>
[System.Serializable]
public struct SpawnPoint
{
    public float x;
    public float z;
}

/// <summary>
/// Manager symulacji roju: spawnuje trio na bazie listy <see cref="spawnPositions"/>,
/// laczy je wedlug <see cref="connections"/>, wstrzykuje DroneSettings i sklada kamery.
/// </summary>
[RequireComponent(typeof(CameraSwitcher))]
public class Simulation : MonoBehaviour
{
    [Header("Spawning")]
    [Tooltip("Prefab drona (musi miec komponent DroneAI).")]
    public GameObject dronePrefab;

    [Tooltip("Dystans miedzy dronami wewnatrz pojedynczego trio (rownoboczny trojkat).")]
    [Min(0.1f)] public float trioDistance = 2f;

    [Tooltip("Pozycje spawnu kotwic trio. Liczba elementow = liczba trio.")]
    public List<SpawnPoint> spawnPositions = new List<SpawnPoint>();

    [Tooltip("Polaczenia miedzy trio (kotwica-do-kotwicy). Indeksy 1-based z listy spawnPositions.")]
    public List<ConnectionData> connections = new List<ConnectionData>();

    [Header("Drone tuning")]
    [Tooltip("Globalny profil dostrojenia (ScriptableObject). Wszystkie spawnowane drony go dostaja.")]
    public DroneSettings globalDroneSettings;

    [Header("Trails (wizualizacja toru lotu)")]
    [Tooltip("Czy spawnowac slady torow lotu (TrailRenderer) na kazdym dronie. Toggle dzialajacy w runtime.")]
    public bool enableTrails = false;

    [Tooltip("Tryb trwaly: slad nigdy nie znika ani nie blaknie. Cala trasa zostaje narysowana w powietrzu na zawsze. Wylacza Duration.")]
    public bool persistentTrails = false;

    [Tooltip("Jak dlugo (sekund) slad pozostaje widoczny zanim zniknie. Ignorowane gdy Persistent zaznaczone.")]
    [Range(0.5f, 60f)] public float trailDuration = 8f;

    [Tooltip("Szerokosc sladu u zrodla (jednostki Unity). W trybie fade konczy sie szpicem na 0, w persistent jednolitej szerokosci.")]
    [Range(0.01f, 1f)] public float trailWidth = 0.08f;

    [Tooltip("Kolor sladu kotwicy trio (dron 'a').")]
    public Color anchorTrailColor = new Color(1f, 0.35f, 0.25f, 1f);

    [Tooltip("Kolor sladu pozostalych dronow (drony 'b', 'c').")]
    public Color followerTrailColor = new Color(0.35f, 0.75f, 1f, 1f);

    [Header("Debug")]
    [Tooltip("Indeks drona do debugowania (-1 = wylacz). Wymaga debugForces lub debugPosition.")]
    public int droneDebugNumber = 0;

    [Tooltip("Loguj sily / step na konsole.")]
    public bool debugForces = false;

    [Tooltip("Loguj pozycje na konsole.")]
    public bool debugPosition = false;

    [Tooltip("Okres logowania debugu (ms).")]
    [Range(10, 2000)] public int debugInterval = 100;

    // ----- Stan runtime ------------------------------------------------------------

    DroneTrioList trios;
    readonly List<DroneAI> allDrones = new List<DroneAI>();
    CameraSwitcher cameraSwitcher;
    float lastDebugTime;
    DroneAI lastDebuggedDrone;

    public IReadOnlyList<DroneAI> AllDrones => allDrones;
    public int TrioCount => trios?.dl.Count ?? 0;

    // ----- Lifecycle ---------------------------------------------------------------

    void Start()
    {
        if (dronePrefab == null)
        {
            Debug.LogError("[Simulation] Brak dronePrefab - nic nie zostanie zaspawnowane.", this);
            enabled = false;
            return;
        }

        if (globalDroneSettings == null)
        {
            Debug.LogError("[Simulation] Brak globalDroneSettings (ScriptableObject) - przeciagnij asset w Inspectorze.", this);
            enabled = false;
            return;
        }

        trios = new DroneTrioList(dronePrefab, trioDistance, connections, spawnPositions);
        trios.SyncDroneSettings(globalDroneSettings);

        allDrones.Clear();
        foreach (DroneTrio trio in trios.dl)
        {
            allDrones.Add(trio.a);
            allDrones.Add(trio.b);
            allDrones.Add(trio.c);
        }
        SwarmRegistry.Register(allDrones);

        cameraSwitcher = GetComponent<CameraSwitcher>();
        SetupCameras();
        ApplyTrails();
    }

    void FixedUpdate()
    {
        if (Time.fixedTime < lastDebugTime + debugInterval / 1000f) return;
        lastDebugTime = Time.fixedTime;
        HandleDronesDebug();
    }

    void OnValidate()
    {
        // Pozwala togglowac slady i zmieniac kolor/dl. zycia/szerokosc w trakcie gry
        // bez restartu - przepina kazdy istniejacy TrailRenderer na biezace ustawienia.
        if (!Application.isPlaying) return;
        if (allDrones.Count == 0) return;
        ApplyTrails();
    }

    // ----- Debug -------------------------------------------------------------------

    void HandleDronesDebug()
    {
        if (droneDebugNumber < 0 || droneDebugNumber >= allDrones.Count)
        {
            if (lastDebuggedDrone != null)
                lastDebuggedDrone.debugEnabled = false;
            return;
        }

        DroneAI current = allDrones[droneDebugNumber];
        if (current == null) return;

        if (lastDebuggedDrone != null && lastDebuggedDrone != current)
            lastDebuggedDrone.debugEnabled = false;

        current.debugEnabled = debugForces || debugPosition;
        lastDebuggedDrone = current;

        if (debugForces) current.DebugForces(droneDebugNumber);
        if (debugPosition) current.DebugPosition(droneDebugNumber);
    }

    void SetupCameras()
    {
        if (cameraSwitcher == null) return;

        cameraSwitcher.cameras.Clear();
        cameraSwitcher.drones.Clear();
        foreach (DroneTrio t in trios.dl)
        {
            Camera cam = t.a.GetComponentInChildren<Camera>();
            DroneMover mover = t.a.GetComponent<DroneMover>();
            if (cam != null) cameraSwitcher.cameras.Add(cam);
            if (mover != null) cameraSwitcher.drones.Add(mover);
        }
    }

    // ----- Trails ------------------------------------------------------------------

    /// <summary>
    /// Dopina (lub aktualizuje) komponent <see cref="MuscSwarm.DroneTrail"/> na kazdym dronie.
    /// Wywolane raz z Start() i z OnValidate() w trakcie gry zeby togglowac live.
    /// </summary>
    void ApplyTrails()
    {
        foreach (DroneAI d in allDrones)
        {
            if (d == null) continue;

            MuscSwarm.DroneTrail t = d.GetComponent<MuscSwarm.DroneTrail>();
            if (enableTrails)
            {
                if (t == null) t = d.gameObject.AddComponent<MuscSwarm.DroneTrail>();
                Color c = d.isAnchor ? anchorTrailColor : followerTrailColor;
                t.Configure(c, trailDuration, trailWidth, persistentTrails);
                t.SetEmitting(true);
            }
            else if (t != null)
            {
                t.SetEmitting(false);
                t.ClearTrail();
            }
        }
    }
}
