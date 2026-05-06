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

    void Start()
    {
        lastLegStartPosition = transform.position;
    }

    void FixedUpdate()
    {
        Vector3 totalForce = Vector3.zero;

        Vector3 missionForce = CalculateMissionForce();
        totalForce += missionForce;

        transform.position += totalForce;
        DrawConnections();
    }

    Vector3 CalculateMissionForce()
    {
        // Sprawdzanie poprawności danych
        if (droneSettings?.mission?.targets == null || currentTargetIndex >= droneSettings.mission.targets.Count)
            return Vector3.zero;

        var currentTarget = droneSettings.mission.targets[currentTargetIndex];
        
        // 1. Wyliczamy punkt docelowy jako pozycję startową + wektor przesunięcia
        // (Zakładając, że Twoje 'target' w MissionTarget to wektor o jaki dron ma się przesunąć od startu nogi)
        Vector3 targetGlobalPos = lastLegStartPosition + currentTarget.target;

        // 2. Obliczamy całkowity czas potrzebny na ten odcinek
        float duration = Mathf.Max(0.01f, currentTarget.timeStamp);

        // 3. Wyliczamy wektor przesunięcia dla TEJ KONKRETNEJ klatki
        // Prędkość = Droga / Czas -> V = target / duration
        // Przesunięcie w klatce = V * Time.fixedDeltaTime
        Vector3 frameMove = (currentTarget.target / duration) * Time.fixedDeltaTime;

        // 4. Aktualizacja postępu czasu
        timeInCurrentLeg += Time.fixedDeltaTime;

        // 5. Sprawdzanie czy czas odcinka minął
        if (timeInCurrentLeg >= duration)
        {
            // Korekta, aby dron nie "przestrzelił" celu przez zaokrąglenia
            // (Opcjonalnie: możesz wyliczyć pozostały ruch do punktu targetGlobalPos)
            
            currentTargetIndex++;
            timeInCurrentLeg = 0f;
            lastLegStartPosition = transform.position; // Nowy punkt startu dla kolejnego wektora
        }

        return frameMove;
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