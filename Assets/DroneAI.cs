using System.Collections.Generic;
using MuscSwarm;
using UnityEngine;

/// <summary>
/// Per-drone orchestrator: chooses PidSpring vs VffOrca, ticks UWB + mission ghost,
/// applies obstacle repulsion. Heavy math lives in <see cref="MuscSwarm"/>.
/// Edge weights: see <see cref="Connection.DefaultWeight"/>.
/// </summary>
[DisallowMultipleComponent]
public class DroneAI : MonoBehaviour
{
    // ----- Konfiguracja widoczna w Inspectorze ------------------------------------

    [Header("Configuration")]
    [Tooltip("Profil dostrojenia (ScriptableObject). Mozesz wspoldzielic miedzy dronami.")]
    public DroneSettings droneSettings;

    [Tooltip("Wspolny model sieci UWB. Auto-wyszukiwany w scenie jesli pusty.")]
    public UWBTransmission transmissionNode;

    [Header("Topology")]
    [Tooltip("Polaczenia z innymi dronami (lista par: sasiad + zadany dystans).")]
    public List<Connection> connections = new List<Connection>();

    [Tooltip("Czy ten dron jest kotwica trio. Kotwice ignoruja niesasiadow ktorzy nie sa kotwicami.")]
    public bool isAnchor = false;

    [Header("Debug")]
    [Tooltip("Wlacza rysowanie 'anteny' i krzyzyka pod dronem oraz logi z Simulation.")]
    public bool debugEnabled = false;

    // ----- Stan runtime ------------------------------------------------------------

    readonly MissionGhost missionGhost = new MissionGhost();
    readonly UwbSampler uwbSampler = new UwbSampler();
    readonly FormationPid formationPid = new FormationPid();
    readonly SwarmSteeringPipeline swarmSteering = new SwarmSteeringPipeline();

    Vector3 simulatedVelocity;        // aktualna predkosc (wykorzystywana przez VFF i ORCA)
    Vector3 lastFrameDisplacement;    // realne przesuniecie w ostatniej klatce (uzywane przez debug)
    Vector3 lastDebugSpringStep;      // do logowania DebugForces
    Vector3 lastDebugMissionStep;

    /// <summary>Bieżąca oszacowana predkosc (jed/s). Używane przez VFF/ORCA innych dronow.</summary>
    public Vector3 SimulatedVelocity => simulatedVelocity;

    // ----- Lifecycle ---------------------------------------------------------------

    void Start()
    {
        if (transmissionNode == null)
            transmissionNode = Object.FindFirstObjectByType<UWBTransmission>();

        if (droneSettings == null)
        {
            Debug.LogError($"[{name}] Brak DroneSettings - dron sie nie ruszy. Przeciagnij SO w Inspectorze.", this);
            enabled = false;
            return;
        }

        Vector3 startPos = transform.position;
        missionGhost.Reset(droneSettings.mission, startPos);
        uwbSampler.Reset(droneSettings);
    }

    void FixedUpdate()
    {
        if (droneSettings == null) return;

        Vector3 prevPos = transform.position;
        float dt = Time.fixedDeltaTime;
        float now = Time.fixedTime;

        // Duch misji + sampler UWB tykaja niezaleznie od trybu.
        missionGhost.Advance(dt);
        uwbSampler.Tick(dt, this, transmissionNode);

        Vector3 step;
        switch (droneSettings.movementMode)
        {
            case DroneMovementMode.VffOrca:
                step = StepVffOrca(dt);
                break;

            default:
                step = StepPidSpring(dt, now);
                break;
        }

        // Obstacle avoidance dziala w obu trybach niezaleznie - dodawane do kroku PRZED cap.
        Vector3 avoidance = ObstacleAvoidance.ComputeRepulsion(
            transform.position,
            droneSettings.droneAvoidBuffer,
            droneSettings.obstacleSensingMultiplier,
            droneSettings.obstacleAvoidanceStrength);

        if (avoidance.sqrMagnitude > 1e-6f)
        {
            step += avoidance * (dt * droneSettings.velocityRetention);
            step = ClampPlanarStep(step, droneSettings.horizontalSpeed * dt, droneSettings.verticalSpeed * dt);
        }

        transform.position += step;
        lastFrameDisplacement = step;
        simulatedVelocity = step / Mathf.Max(dt, 1e-5f);

        DrawConnections();
        DrawSteeringDebug(prevPos);
    }

    // ----- Tryb PID + Spring -------------------------------------------------------

    Vector3 StepPidSpring(float dt, float now)
    {
        Vector3 raw = formationPid.ComputeStepDisplacement(
            this,
            missionGhost.Position,
            simulatedVelocity,
            droneSettings,
            transmissionNode,
            dt,
            now);

        // Tlumienie - utrzymaj velocityRetention*100% ruchu, reszta wygas.
        // (Stara wersja robila 'totalForce - totalForce*drag' = totalForce*(1-drag); zachowujemy semantyke.)
        raw *= droneSettings.velocityRetention;

        // Caps niezalezne dla poziomu i pionu (drony fizycznie maja osobne limity).
        raw = ClampPlanarStep(raw, droneSettings.horizontalSpeed * dt, droneSettings.verticalSpeed * dt);

        lastDebugSpringStep = raw;
        lastDebugMissionStep = Vector3.zero; // mission spring jest w PID; zachowane dla zgodnosci debugu
        return raw;
    }

    /// <summary>Cap kroku: oddzielnie XZ na horizontalMax, oddzielnie Y na verticalMax.</summary>
    static Vector3 ClampPlanarStep(Vector3 step, float horizontalMax, float verticalMax)
    {
        Vector2 horizontal = new Vector2(step.x, step.z);
        if (horizontal.sqrMagnitude > horizontalMax * horizontalMax)
            horizontal = horizontal.normalized * horizontalMax;

        float vertical = Mathf.Clamp(step.y, -verticalMax, verticalMax);

        return new Vector3(horizontal.x, vertical, horizontal.y);
    }

    // ----- Tryb VFF + ORCA ---------------------------------------------------------

    Vector3 StepVffOrca(float dt)
    {
        Vector3 safe = swarmSteering.ComputeSafeVelocity(
            this,
            missionGhost.Position,
            simulatedVelocity,
            droneSettings,
            transmissionNode);

        // ORCA/VFF dzialaja w plaszczyznie XZ - Y musimy dolozyc niezaleznie.
        // Prosta proporcjonalna kontrola wysokosci: dazymy do Y ducha celu.
        float verticalError = missionGhost.Position.y - transform.position.y;
        float verticalVel = Mathf.Clamp(
            verticalError * droneSettings.missionP,
            -droneSettings.verticalSpeed,
            droneSettings.verticalSpeed);
        safe.y = verticalVel;

        // Smooth blend miedzy aktualnym a nowym safe velocity (drag-like inertia).
        float keep = Mathf.Clamp01(1f - droneSettings.velocityRetention);
        Vector3 blended = Vector3.Lerp(simulatedVelocity, safe, keep);

        // Rotacja w kierunku ruchu (kosmetyka).
        if (blended.sqrMagnitude > 0.05f)
        {
            Vector3 face = blended; face.y = 0f;
            if (face.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 4f * dt);
        }

        Vector3 step = blended * dt;
        lastDebugSpringStep = step;
        lastDebugMissionStep = Vector3.zero;
        return step;
    }

    // ----- Public API uzywane przez inne klasy -------------------------------------

    /// <summary>Dodaje polaczenie do sasiada z zadanym dystansem (uzywane przy budowie trio/sieci).</summary>
    public void AddConnection(DroneAI other, float distance)
    {
        connections.Add(new Connection
        {
            target = other,
            desiredDistance = distance,
            weight = Connection.DefaultWeight(this, other),
        });
    }

    /// <summary>Zerowanie osi Y wektora (uzywane przez stare API). Zostawione dla zgodnosci.</summary>
    public Vector3 StripYAxis(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // ----- Debug helpers (wywolywane przez Simulation) -----------------------------

    public void DebugPosition(int droneIndex)
    {
        float timeMs = Time.fixedTime * 1000f;
        Vector3 p = transform.position;
        Debug.Log($"[<color=yellow>{timeMs:F0} ms</color>] Drone <color=cyan>#{droneIndex}</color> | " +
                  $"Pos: (<b>{p.x:F2}</b>, <b>{p.y:F2}</b>, <b>{p.z:F2}</b>)");
    }

    public void DebugForces(int droneIndex)
    {
        float timeMs = Time.fixedTime * 1000f;
        float moveMag = lastFrameDisplacement.magnitude / Mathf.Max(Time.fixedDeltaTime, 1e-5f);

        if (droneSettings != null && droneSettings.movementMode == DroneMovementMode.VffOrca)
        {
            Debug.Log($"[<color=yellow>{timeMs:F0} ms</color>] <color=cyan>Drone #{droneIndex} VFF/ORCA:</color> " +
                      $"<color=green>pref:</color> <b>{swarmSteering.PreferredVelocity.magnitude:F3}</b> | " +
                      $"<color=yellow>safe:</color> <b>{swarmSteering.SafeVelocity.magnitude:F3}</b> | " +
                      $"<color=orange>v:</color> <b>{moveMag:F3}</b>");
            return;
        }

        Debug.Log($"[<color=yellow>{timeMs:F0} ms</color>] <color=cyan>Drone #{droneIndex} PID:</color> " +
                  $"<color=green>step:</color> <b>{lastDebugSpringStep.magnitude:F3}</b> | " +
                  $"<color=orange>v:</color> <b>{moveMag:F3}</b> | " +
                  $"<color=#888888>tracked:</color> {formationPid.TrackedNeighbors}");
    }

    // ----- Rysowanie ---------------------------------------------------------------

    void DrawConnections()
    {
        for (int i = 0; i < connections.Count; i++)
        {
            Connection c = connections[i];
            if (c.target == null) continue;
            float dist = Vector3.Distance(transform.position, c.target.transform.position);
            float relErr = Mathf.Abs(dist - c.desiredDistance) / Mathf.Max(c.desiredDistance * 0.5f, 0.01f);
            Color col = Color.Lerp(Color.cyan, Color.red, Mathf.Clamp01(relErr));
            Debug.DrawLine(transform.position, c.target.transform.position, col);
        }

        if (debugEnabled)
        {
            Debug.DrawLine(transform.position, transform.position + Vector3.up * 2f, Color.cyan);
            Debug.DrawRay(transform.position + Vector3.left * 0.5f, Vector3.right, Color.cyan);
            Debug.DrawRay(transform.position + Vector3.back * 0.5f, Vector3.forward, Color.cyan);
        }
    }

    void DrawSteeringDebug(Vector3 prevPos)
    {
        if (droneSettings == null || !droneSettings.swarm.drawSteeringVectors) return;

        Vector3 p = transform.position;
        Debug.DrawRay(p, swarmSteering.PreferredVelocity, Color.green, Time.fixedDeltaTime);
        Debug.DrawRay(p, swarmSteering.SafeVelocity, Color.yellow, Time.fixedDeltaTime);
        Debug.DrawLine(prevPos, p, Color.cyan, Time.fixedDeltaTime);
    }
}
