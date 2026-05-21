using UnityEngine;
using System.Collections.Generic;

public class DroneAI : MonoBehaviour
{
    public List<Connection> connections = new List<Connection>();
    public bool isAnchor = false;
    public DroneSettings droneSettings;

    // PID
    private struct PidState 
    {
        public float integral;
        public float lastError;
        public float lastVelocity;

        // Smith Predictor
        public float internalModelDist;  
        public float driftCorrection;    
        public Vector3 lastAppliedForce; 
        public bool initialized;

        // Estymacja prędkości sąsiada
        public float neighborVelocity;   // Prędkość zbliżania/oddalania się celu
        public float lastUwbDistance;
        public float lastUwbTimestamp;
    }
    private Dictionary<DroneAI, PidState> pidStorage = new Dictionary<DroneAI, PidState>();

    // Zmienne pomocnicze misji
    private int currentTargetIndex = 0;
    private float timeInCurrentLeg = 0f;
    private Vector3 lastLegStartPosition;
    private Vector3 estimatedPosition;
    private Vector3 currentGyroDrift; // Skumulowany błąd kątowy
    private float uwbTimer = 0f;

    public UWBTransmission transmissionNode;

    private Vector3 missionForce;
    private Vector3 springForce;
    private Vector3 totalMovement;

    private Vector3 simulatedVelocity;
    private Vector3 vffPreferredVelocity;
    private Vector3 vffSafeVelocity;
    private readonly List<DroneAI> neighborBuffer = new List<DroneAI>();
    private readonly List<Orca2D.AgentState> orcaAgentBuffer = new List<Orca2D.AgentState>();

    public Vector3 SimulatedVelocity => simulatedVelocity;
    public bool debugEnabled = false;

    void Start()
    {
        if (transmissionNode == null)
        {
            transmissionNode = Object.FindFirstObjectByType<UWBTransmission>();
        }

        estimatedPosition = transform.position; 
        lastLegStartPosition = estimatedPosition;

        if (droneSettings != null)
        {
            float interval = droneSettings.uwbIntervalMs / 1000f;
            uwbTimer = Random.Range(0f, interval);
        }
    }

    void FixedUpdate()
    {
        if (droneSettings != null && droneSettings.swarm.movementMode == DroneMovementMode.VffOrca)
        {
            FixedUpdateVffOrca();
            return;
        }

        FixedUpdatePidSpring();
    }

    void FixedUpdatePidSpring()
    {
        UpdateDrift();
        UpdateUWB();

        Vector3 totalForce = Vector3.zero;

        missionForce = CalculateMissionForce();
        springForce = CalculateSpringForce();

        totalForce += missionForce;
        totalForce += springForce;

        totalMovement = totalForce - totalForce * droneSettings.drag;

        float maxDistancePerFrame = droneSettings.horizontalSpeed * Time.fixedDeltaTime;
        if (totalMovement.magnitude > maxDistancePerFrame)
        {
            totalMovement = totalMovement.normalized * maxDistancePerFrame;
        }

        Vector3 prevPos = transform.position;
        transform.position += totalMovement;
        simulatedVelocity = totalMovement / Time.fixedDeltaTime;

        DrawConnections();
        DrawSteeringDebug(prevPos);
    }

    void FixedUpdateVffOrca()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 prevPos = transform.position;

        AdvanceMissionGhost();
        UpdateUWB();

        SwarmSteeringSettings swarm = droneSettings.swarm;
        SwarmRegistry.GetNeighborsInRadius(this, swarm.perceptionRadius, neighborBuffer);

        vffPreferredVelocity = VffSteering.ComputePreferredVelocity(
            this,
            neighborBuffer,
            estimatedPosition,
            swarm,
            droneSettings.horizontalSpeed);

        Orca2D.FillAgentStates(this, SwarmRegistry.All, swarm.orcaNeighborRadius, orcaAgentBuffer);

        Vector2 pos2 = new Vector2(transform.position.x, transform.position.z);
        Vector2 vel2 = new Vector2(simulatedVelocity.x, simulatedVelocity.z);
        Vector2 pref2 = new Vector2(vffPreferredVelocity.x, vffPreferredVelocity.z);

        Vector2 safe2 = Orca2D.ComputeSafeVelocity(
            pos2,
            vel2,
            swarm.agentRadius,
            orcaAgentBuffer,
            swarm.orcaTimeHorizon,
            pref2,
            droneSettings.horizontalSpeed);

        vffSafeVelocity = new Vector3(safe2.x, 0f, safe2.y);
        simulatedVelocity = Vector3.Lerp(simulatedVelocity, vffSafeVelocity, 1f - droneSettings.drag);
        transform.position += simulatedVelocity * dt;

        if (simulatedVelocity.sqrMagnitude > 0.05f)
        {
            Vector3 face = simulatedVelocity.normalized;
            face.y = 0f;
            if (face.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 4f * dt);
        }

        missionForce = Vector3.zero;
        springForce = Vector3.zero;
        totalMovement = simulatedVelocity * dt;

        DrawConnections();
        DrawSteeringDebug(prevPos);
    }

    void AdvanceMissionGhost()
    {
        if (droneSettings?.mission?.targets == null || currentTargetIndex >= droneSettings.mission.targets.Count)
            return;

        var currentTarget = droneSettings.mission.targets[currentTargetIndex];
        float duration = Mathf.Max(0.01f, currentTarget.timeStamp);
        Vector3 step = (currentTarget.target / duration) * Time.fixedDeltaTime;

        estimatedPosition += step;
        timeInCurrentLeg += Time.fixedDeltaTime;

        if (timeInCurrentLeg >= duration)
        {
            estimatedPosition = lastLegStartPosition + currentTarget.target;
            currentTargetIndex++;
            timeInCurrentLeg = 0f;
            lastLegStartPosition = estimatedPosition;
        }
    }

    void DrawSteeringDebug(Vector3 prevPos)
    {
        if (droneSettings == null || !droneSettings.swarm.drawSteeringVectors)
            return;

        Vector3 p = transform.position;
        Debug.DrawRay(p, vffPreferredVelocity, Color.green, Time.fixedDeltaTime);
        Debug.DrawRay(p, vffSafeVelocity, Color.yellow, Time.fixedDeltaTime);
        Debug.DrawLine(prevPos, p, Color.cyan, Time.fixedDeltaTime);
    }

    void UpdateUWB()
    {
        if (transmissionNode == null || droneSettings == null) return;

        uwbTimer += Time.fixedDeltaTime;
        float interval = droneSettings.uwbIntervalMs / 1000f;

        if (uwbTimer >= interval)
        {
            foreach (var c in connections)
            {
                if (c.target == null) continue;
                
                // Realny dystans zaszumiony błędem pomiarowym (np. 2cm)
                float realDist = Vector3.Distance(transform.position, c.target.transform.position);
                float noisyDist = realDist + Random.Range(-0.1f, 0.1f); // Błąd 0.1 jednostki (ok. 1.6cm w Twojej skali)

                transmissionNode.PushMeasurement(this, c.target, noisyDist);
            }
            uwbTimer = 0f;
        }
    }

    Vector3 CalculateMissionForce()
    {
        AdvanceMissionGhost();
        return Vector3.zero;
    }

    Vector3 CalculateSpringForce()
    {
        Vector3 totalSpringForce = Vector3.zero;
        float dt = Time.fixedDeltaTime;

        // --- 1. SIŁA OD WIRTUALNEJ KOTWICY (MISJA) ---
        // To jest ta "gumka", która ciągnie drona za celem misji
        Vector3 missionError = estimatedPosition - transform.position;
        
        // Używamy osobnych wzmocnień dla misji, żeby leciał "szybko, ale nie na full pizdę"
        float missionP = 20f;  // Jak mocno gonić ducha
        float missionD = 2f;  // Tłumienie, żeby nie przestrzelił
        
        Vector3 currentVel = totalMovement / dt;
        Vector3 missionSpring = (missionError * missionP) - (currentVel * missionD);
        totalSpringForce += missionSpring;

        foreach (var c in connections)
        {
            if (c.target == null) continue;
            if (isAnchor && !c.target.isAnchor) continue;
            if (!pidStorage.TryGetValue(c.target, out PidState state)) state = new PidState();

            Vector3 directionToTarget = (c.target.transform.position - transform.position).normalized;

            // --- SMITH PREDICTOR Z ESTYMACJĄ PRĘDKOŚCI ---
            
            // 1. Aktualizacja modelu o NASZ ruch
            float myMovementTowardsTarget = Vector3.Dot(state.lastAppliedForce, directionToTarget);
            state.internalModelDist -= myMovementTowardsTarget;

            // 2. Aktualizacja modelu o ruch SĄSIADA (przewidywany)
            state.internalModelDist += state.neighborVelocity * dt;

            // 3. Korekta modelu z UWB
            if (transmissionNode.TryGetDistance(this, c.target, out var data))
            {
                if (state.lastUwbTimestamp > 0)
                {
                    float timeDelta = data.timestamp - state.lastUwbTimestamp;
                    if (timeDelta > 0)
                    {
                        float distDelta = data.distance - state.lastUwbDistance;
                        float rawVel = distDelta / timeDelta;
                        state.neighborVelocity = Mathf.Lerp(state.neighborVelocity, rawVel, 0.2f);
                    }
                }
                state.lastUwbDistance = data.distance;
                state.lastUwbTimestamp = data.timestamp;

                float age = Time.fixedTime - data.timestamp;
                float expectedDist = state.internalModelDist + (state.neighborVelocity * age);
                
                // OBLICZENIE MODEL ERROR (poprawka)
                float modelError = data.distance - expectedDist;

                state.driftCorrection = Mathf.Lerp(state.driftCorrection, modelError, 0.15f);

                if (!state.initialized)
                {
                    state.internalModelDist = data.distance;
                    state.initialized = true;
                }
            }

            // 4. Obliczamy "odlagowany" dystans
            float estimatedDist = state.internalModelDist + state.driftCorrection;
            float error = estimatedDist - c.desiredDistance;

            // --- DODANIE DEADZONE ---
            // Jeśli błąd jest mniejszy niż np. 1cm (0.01), ignorujemy go, aby uniknąć drgań
            if (Mathf.Abs(error) < droneSettings.maxDistanceErrorInCentimeters * 0.06f) 
            {
                error = 0f;
            }

            // --- PID ---
            float pTerm = error * droneSettings.P;

            // Całka (I) nie będzie rosła wewnątrz Deadzone, co zapobiega "pływaniu" drona
            state.integral += error * dt;
            float iTerm = state.integral * droneSettings.I;

            // D obliczamy przed wyzerowaniem błędu, żeby zachować tłumienie (Damping)
            float currentVelocity = (estimatedDist - state.lastError) / dt;

            // Jeśli prędkość jest minimalna (szum), wyzeruj ją dla członu D
            if (Mathf.Abs(currentVelocity) < droneSettings.minDThreshold) 
            {
                currentVelocity = 0f;
            }

            state.lastVelocity = Mathf.Lerp(state.lastVelocity, currentVelocity, droneSettings.derivativeSmoothing);
            float dTerm = state.lastVelocity * droneSettings.D;

            state.lastError = estimatedDist;
            
            float pidOutput = pTerm + iTerm + dTerm;
            
            // Jeśli error jest zero, a prędkość drona jest minimalna, możemy wygasić siłę całkowicie
            if (error == 0f && currentVelocity < droneSettings.minVelocity)
            {
                pidOutput = 0f;
            }

            Vector3 force = directionToTarget * (pidOutput * dt);

            state.lastAppliedForce = force; 
            pidStorage[c.target] = state;
            totalSpringForce += force;
        }

        return totalSpringForce;
    }

    void UpdateDrift()
    {
        // Co klatkę żyroskop "pływa" o mały ułamek stopnia
        currentGyroDrift.x += Random.Range(-droneSettings.driftSpeed, droneSettings.driftSpeed);
        currentGyroDrift.y += Random.Range(-droneSettings.driftSpeed, droneSettings.driftSpeed);
        currentGyroDrift.z += Random.Range(-droneSettings.driftSpeed, droneSettings.driftSpeed);
    }
    
    public Vector3 StripYAxis(Vector3 force)
    {
        return new Vector3(force.x, 0f, force.z);
    }

    public void AddConnection(DroneAI other, float distance)
    {
        connections.Add(new Connection { target = other, desiredDistance = distance });
    }

    void DrawConnections()
    {
        foreach (var c in connections)
        {
            if (c.target == null) continue;
            float dist = Vector3.Distance(transform.position, c.target.transform.position);
            float error = Mathf.Abs(dist - c.desiredDistance);
            Color col = Color.Lerp(Color.cyan, Color.red, error / (c.desiredDistance * 0.5f));
            Debug.DrawLine(transform.position, c.target.transform.position, col);
        }

        if (debugEnabled)
        {
            // Rysuje pionową linię "anteny" nad dronem, żebyś widział go z daleka
            Debug.DrawLine(transform.position, transform.position + Vector3.up * 2f, Color.cyan);
            // Mały krzyżyk na ziemi
            Debug.DrawRay(transform.position + Vector3.left * 0.5f, Vector3.right, Color.cyan);
            Debug.DrawRay(transform.position + Vector3.back * 0.5f, Vector3.forward, Color.cyan);
        }
    }

    public void DebugPosition(int droneIndex)
    {
        float timeMs = Time.fixedTime * 1000f;
        Vector3 pos = transform.position;

        Debug.Log($"[<color=yellow>{timeMs:F0} ms</color>] Drone <color=cyan>#{droneIndex}</color> | " +
                $"Pos: (<b>{pos.x:F2}</b>, <b>{pos.y:F2}</b>, <b>{pos.z:F2}</b>)");
    }

    public void DebugForces(int droneIndex)
    {
        float timeMs = Time.fixedTime * 1000f;

        // Obliczamy magnitudo (siłę wypadkową), żeby log był czytelny
        float mMag = missionForce.magnitude;
        float sMag = springForce.magnitude;
        float tMag = totalMovement.magnitude;

        // Kolorowanie siły PID: jeśli jest bardzo duża, wyświetl na czerwono (ostrzeżenie przed oscylacjami)
        string springColor = sMag > (droneSettings.horizontalSpeed * 0.5f) ? "#FF4444" : "#44FF44";

        if (droneSettings != null && droneSettings.swarm.movementMode == DroneMovementMode.VffOrca)
        {
            Debug.Log($"[<color=yellow>{timeMs:F0} ms</color>] <color=cyan>Drone #{droneIndex} VFF/ORCA:</color>\n" +
                    $"<color=green>VFF pref:</color> <b>{vffPreferredVelocity.magnitude:F3}</b> | " +
                    $"<color=yellow>ORCA safe:</color> <b>{vffSafeVelocity.magnitude:F3}</b> | " +
                    $"<color=orange>Move:</color> <b>{tMag:F3}</b>");
            return;
        }

        Debug.Log($"[<color=yellow>{timeMs:F0} ms</color>] <color=cyan>Drone #{droneIndex} Forces:</color>\n" +
                $"<color=green>Mission:</color> <b>{mMag:F3}</b> | " +
                $"<color={springColor}>PID/Spring:</color> <b>{sMag:F3}</b> | " +
                $"<color=orange>Actual Move:</color> <b>{tMag:F3}</b>");
    }

}