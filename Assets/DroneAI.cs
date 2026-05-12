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
        public float lastDerivative;
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

        totalForce += CalculateMissionForce();
        totalForce += CalculateSpringForce();

        transform.position += StripYAxis(totalForce);
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
        if (droneSettings?.mission?.targets == null || currentTargetIndex >= droneSettings.mission.targets.Count)
            return Vector3.zero;

        var currentTarget = droneSettings.mission.targets[currentTargetIndex];
        float duration = Mathf.Max(0.01f, currentTarget.timeStamp);

        // 1. Obliczamy idealny ruch dla tej klatki
        Vector3 frameMove = (currentTarget.target / duration) * Time.fixedDeltaTime;

        // 2. APLIKUJEMY BŁĘDY (Symulacja niedoskonałości sprzętu)
        // Błąd kierunku (np. o 2 stopnie)
        Vector3 noisyMove = Quaternion.Euler(currentGyroDrift) * frameMove;
        
        // Błąd siły ciągu (np. silnik kręci się o 1% za szybko/wolno)
        float thrustError = Random.Range(0.98f, 1.02f); 
        noisyMove *= thrustError;

        // 3. AKTUALIZUJEMY ESTYMACJĘ I CZAS
        // Dron myśli, że wykonał IDEALNY ruch:
        estimatedPosition += frameMove; 
        timeInCurrentLeg += Time.fixedDeltaTime;

        // 4. LOGIKA KOŃCZENIA ETAPU
        // Dron kończy etap, bo JEGO ZEGAR i JEGO ESTYMACJA mówią, że już czas
        if (timeInCurrentLeg >= duration)
        {
            currentTargetIndex++;
            timeInCurrentLeg = 0f;
            // Klucz: Nowy etap zaczyna od miejsca, w którym MYŚLAŁ, że skończył
            lastLegStartPosition = estimatedPosition; 
        }

        // Zwracamy ZASZUMIONY ruch, który faktycznie przesunie obiekt w Unity
        return noisyMove;
    }

    Vector3 CalculateSpringForce()
    {
        Vector3 totalSpringForce = Vector3.zero;

        foreach (var c in connections)
        {
            if (c.target == null) continue;

            if (transmissionNode.TryGetDistance(this, c.target, out var data))
            {
                // 1. Kierunek (tylko kąt, tak jak chciałeś)
                Vector3 directionToTarget = (c.target.transform.position - transform.position).normalized;

                // 2. Pobieramy/Tworzymy stan PID dla tego sąsiada
                if (!pidStorage.TryGetValue(c.target, out PidState state)) state = new PidState();

                // 3. Obliczamy błąd na podstawie UWB
                float currentDist = data.distance;
                float error = currentDist - c.desiredDistance;
                float dt = Time.fixedDeltaTime;

                // --- PID CORE ---
                // P
                float pTerm = error * droneSettings.P;

                // I
                state.integral += error * dt;
                float iTerm = state.integral * droneSettings.I;

                // D (z filtrowaniem z Twojego settingsa)
                float rawDerivative = (error - state.lastError) / dt;
                state.lastDerivative = Mathf.Lerp(state.lastDerivative, rawDerivative, droneSettings.derivativeSmoothing);
                float dTerm = state.lastDerivative * droneSettings.D;

                // 4. Zapisujemy stan do pamięci na następną klatkę
                state.lastError = error;
                pidStorage[c.target] = state;

                // 5. Wynikowa siła
                float pidOutput = pTerm + iTerm + dTerm;

                // DODAJ TO: Limit siły na klatkę. 
                // Przy skali 1m = 6u, limit 0.5 - 1.0 powinien być bezpieczny
                pidOutput = Mathf.Clamp(pidOutput, -1.0f, 1.0f); 

                totalSpringForce += directionToTarget * pidOutput;
            }
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
    }

}