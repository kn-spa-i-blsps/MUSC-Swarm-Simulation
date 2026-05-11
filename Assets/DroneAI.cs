using UnityEngine;
using System.Collections.Generic;

public class DroneAI : MonoBehaviour
{
    public List<Connection> connections = new List<Connection>();
    public bool isAnchor = false;
    public DroneSettings droneSettings;

    // Zmienne pomocnicze misji
    private int currentTargetIndex = 0;
    private float timeInCurrentLeg = 0f;
    private Vector3 lastLegStartPosition;
    private Vector3 estimatedPosition;
    private Vector3 currentGyroDrift; // Skumulowany błąd kątowy

    void Start()
    {
        estimatedPosition = transform.position; 
        lastLegStartPosition = estimatedPosition;
    }

    void FixedUpdate()
    {
        UpdateDrift();


        Vector3 totalForce = Vector3.zero;

        Vector3 missionForce = CalculateMissionForce();
        totalForce += missionForce;

        transform.position += StripYAxis(totalForce);
        DrawConnections();
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