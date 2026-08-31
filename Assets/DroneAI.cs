using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Per-drone controller on mission-i-moduly: UWB publish, mission ghost, PD-to-ghost +
/// Smith-style range PID. Mothers skip child edges; children and mother↔mother are regulated.
/// Replaced by a thin orchestrator on vff+orca.
/// </summary>
public class DroneAI : MonoBehaviour
{
    public List<Connection> connections = new List<Connection>();
    public bool isAnchor = false;
    public DroneSettings droneSettings;

    private struct PidState 
    {
        public float integral;
        public float lastError;
        public float lastVelocity;

        // Internal range model (Smith-style). On this branch, own motion is approximated
        // by lastAppliedForce (spring step only) — FormationPid on vff+orca uses real displacement.
        public float internalModelDist;  
        public float driftCorrection;    
        public Vector3 lastAppliedForce; 
        public bool initialized;

        public float neighborVelocity;
        public float lastUwbDistance;
        public float lastUwbTimestamp;
    }
    private Dictionary<DroneAI, PidState> pidStorage = new Dictionary<DroneAI, PidState>();

    private int currentTargetIndex = 0;
    private float timeInCurrentLeg = 0f;
    private Vector3 lastLegStartPosition;
    // Ghost setpoint flown along Mission.targets. Springs chase this; CalculateMissionForce returns 0.
    private Vector3 estimatedPosition;
    // Written every tick, never applied to pose/heading on this branch.
    private Vector3 currentGyroDrift;
    private float uwbTimer = 0f;

    public UWBTransmission transmissionNode;

    private Vector3 missionForce;
    private Vector3 springForce;
    private Vector3 totalMovement;

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

        transform.position += totalMovement;

        DrawConnections();
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
                
                float realDist = Vector3.Distance(transform.position, c.target.transform.position);
                float noisyDist = realDist + Random.Range(-0.1f, 0.1f); // ±0.1 u ≈ 1.6 cm at 6 u/m

                transmissionNode.PushMeasurement(this, c.target, noisyDist);
            }
            uwbTimer = 0f;
        }
    }

    Vector3 CalculateMissionForce()
    {
        if (droneSettings?.mission?.targets == null || currentTargetIndex >= droneSettings.mission.targets.Count)
            return Vector3.zero;

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

        // Ghost moved; springs in CalculateSpringForce chase estimatedPosition.
        return Vector3.zero;
    }

    Vector3 CalculateSpringForce()
    {
        Vector3 totalSpringForce = Vector3.zero;
        float dt = Time.fixedDeltaTime;

        Vector3 missionError = estimatedPosition - transform.position;
        
        // Not on DroneSettings on this branch; vff+orca exposes missionP / missionD on the asset.
        float missionP = 20f;
        float missionD = 2f;
        
        Vector3 currentVel = totalMovement / dt;
        Vector3 missionSpring = (missionError * missionP) - (currentVel * missionD);
        totalSpringForce += missionSpring;

        foreach (var c in connections)
        {
            if (c.target == null) continue;
            // Inverse of main: mothers do not PID children; they do PID other mothers.
            if (isAnchor && !c.target.isAnchor) continue;
            if (!pidStorage.TryGetValue(c.target, out PidState state)) state = new PidState();

            Vector3 directionToTarget = (c.target.transform.position - transform.position).normalized;

            float myMovementTowardsTarget = Vector3.Dot(state.lastAppliedForce, directionToTarget);
            state.internalModelDist -= myMovementTowardsTarget;

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

        Debug.Log($"[<color=yellow>{timeMs:F0} ms</color>] <color=cyan>Drone #{droneIndex} Forces:</color>\n" +
                $"<color=green>Mission:</color> <b>{mMag:F3}</b> | " +
                $"<color={springColor}>PID/Spring:</color> <b>{sMag:F3}</b> | " +
                $"<color=orange>Actual Move:</color> <b>{tMag:F3}</b>");
    }

}