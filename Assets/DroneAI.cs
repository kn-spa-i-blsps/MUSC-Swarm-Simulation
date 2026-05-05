using UnityEngine;
using System.Collections.Generic;

public class DroneAI : MonoBehaviour
{
    [System.Serializable]
    public class Connection
    {
        public DroneAI target;
        public float desiredDistance;
        public float previousError = 0f;
        public float integral = 0f;
        public float smoothedDerivative = 0f;
        public bool initialized = false;
        
        public Queue<float> errorBuffer = new Queue<float>();
        public Queue<Vector3> directionBuffer = new Queue<Vector3>();
    }

    public bool isAnchor = false;
    public List<Connection> connections = new List<Connection>();

    [Header("PID Settings")]
    public float P = 50f;
    public float I = 0.05f;
    public float D = 5f;
    [Range(0.01f, 1f)]
    public float derivativeSmoothing = 0.2f;

    [Header("Physics")]
    public float moveSpeed = 1f;
    public float drag = 0.95f;
    public int latencyFrames = 5;

    [Header("Super Legit - SubStepping")]
    public int pidSubSteps = 20; // Ile razy szybciej ma działać PID niż reszta symulacji

    [HideInInspector] public Vector3 externalForce;
    private Vector3 velocity = Vector3.zero;

    public void AddConnection(DroneAI other, float distance)
    {
        connections.Add(new Connection { target = other, desiredDistance = distance });
    }

    void FixedUpdate()
    {
        DrawConnections();
        //if (isAnchor && connections.FindAll(c => c.target.isAnchor).Count == 0) return;
        if (connections.Count == 0) return;

        // --- 1. LOGIKA SENSORA (RADIO UWB) ---
        foreach (var c in connections)
        {
            if (c.target == null) continue;
            if (!c.target.isAnchor && isAnchor) continue;

            // Prawdziwy wektor do sąsiada (to, co widzi świat, ale nie dron)
            Vector3 realVec = c.target.transform.position - transform.position;
            
            // Dron zapamiętuje DYSTANS i KIERUNEK w momencie próbkowania
            // W prawdziwym UWB kierunek brałoby się z fazy sygnału lub kilku anten (AoA)
            float realDist = realVec.magnitude;
            Vector3 directionAtSample = realVec.normalized;

            // Pakujemy to do bufora - dron dowie się o tym dopiero po latencyFrames
            c.errorBuffer.Enqueue(realDist - c.desiredDistance);
            
            // DODAJEMY: Bufor kierunku, żeby dron nie wiedział "na żywo" w którą stronę pchać
            // (Wymaga dodania Queue<Vector3> directionBuffer w klasie Connection)
            c.directionBuffer.Enqueue(directionAtSample);

            if (c.errorBuffer.Count > latencyFrames + 1) {
                c.errorBuffer.Dequeue();
                c.directionBuffer.Dequeue();
            }
        }
        
        // --- 2. SUB-STEPPING PID (100% Blind Dead Reckoning) ---
        float subStepDeltaTime = Time.fixedDeltaTime / pidSubSteps;
        
        for (int step = 0; step < pidSubSteps; step++)
        {
            Vector3 totalForce = Vector3.zero;

            foreach (var c in connections)
            {
                if (c.target == null || c.errorBuffer.Count <= latencyFrames) continue;

                // POBIERAMY DANE Z LAGIEM (Kierunek też jest stary!)
                float delayedError = c.errorBuffer.Peek(); 
                Vector3 delayedDir = c.directionBuffer.Peek(); 

                // Prędkość zbliżania się (rzut naszej prędkości na STARY kierunek)
                float closingSpeed = Vector3.Dot(velocity, delayedDir);

                // Dead Reckoning: "Mój stary błąd minus to, co sam przeleciałem"
                float legitPredictedError = delayedError - (closingSpeed * subStepDeltaTime * step);

                if (!c.initialized) {
                    c.previousError = legitPredictedError;
                    c.initialized = true;
                }

                // PID
                c.integral = Mathf.Clamp(c.integral + (legitPredictedError * subStepDeltaTime), -5f, 5f);
                float rawDerivative = -closingSpeed; 
                c.smoothedDerivative = Mathf.Lerp(c.smoothedDerivative, rawDerivative, derivativeSmoothing);
                c.previousError = legitPredictedError;

                // Siła P z potęgą (o którą pytałeś)
                float pFactor = Mathf.Pow(Mathf.Abs(legitPredictedError), 3f) * Mathf.Sign(legitPredictedError);
                float pidOutput = (P * pFactor) + (I * c.integral) + (D * c.smoothedDerivative);

                // REPUZJA (Też na bazie Dead Reckoning!)
                // Jeśli dron "myśli", że jest blisko (na podstawie starego info + ruchu), to odbija
                float estimatedDist = (delayedError + c.desiredDistance) - (closingSpeed * subStepDeltaTime * step);
                if (estimatedDist < c.desiredDistance * 0.5f) {
                    float rep = Mathf.Pow(1f - (estimatedDist / (c.desiredDistance * 0.5f)), 2) * 400f;
                    pidOutput -= rep;
                }

                totalForce += delayedDir * pidOutput;
            }

            // Fizyka (Blokada Y wewnątrz sub-stepu)
            Vector3 finalAcc = (totalForce / connections.Count) + externalForce;
            finalAcc.y = 0; 
            
            velocity += finalAcc * subStepDeltaTime;
            velocity *= Mathf.Pow(drag, 1f / pidSubSteps); 
            velocity.y = 0;

            transform.position += velocity * moveSpeed * subStepDeltaTime;
            transform.position = new Vector3(transform.position.x, 0, transform.position.z);
        }
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