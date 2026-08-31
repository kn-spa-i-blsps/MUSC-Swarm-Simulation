using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Per-drone formation controller on branch <c>main</c>.
/// Each edge in <see cref="connections"/> is a 1D spring on UWB range: the drone
/// samples true range+bearing, delays them by <see cref="latencyFrames"/>, then
/// runs a cubic-P PID plus dead-reckoning inside <see cref="pidSubSteps"/> physics
/// substeps. Motion is locked to the XZ plane.
/// </summary>
/// <remarks>
/// Later branches replace this file with a thin orchestrator (MissionGhost, UwbSampler,
/// FormationPid / VFF+ORCA). Do not mix those types into this class.
/// </remarks>
public class DroneAI : MonoBehaviour
{
    /// <summary>
    /// One directed spring to a neighbour. PID state and the UWB delay queues live here
    /// because each edge has its own error history.
    /// </summary>
    [System.Serializable]
    public class Connection
    {
        public DroneAI target;
        public float desiredDistance;
        public float previousError = 0f;
        public float integral = 0f;
        public float smoothedDerivative = 0f;
        public bool initialized = false;
        // FIFO of (range error, unit bearing) sampled in FixedUpdate. Control peeks the
        // oldest pair once Count > latencyFrames — that is the entire radio-delay model.
        public Queue<float> errorBuffer = new Queue<float>();
        public Queue<Vector3> directionBuffer = new Queue<Vector3>();
    }

    // Trio drone "a". A mother with zero edges to other anchors returns before PID
    // (frozen datum). That is independent of whether inter-trio edges actually apply force.
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
    // PID integrates this many times per FixedUpdate so the controller can run "faster"
    // than the physics tick while still using only delayed UWB samples.
    public int pidSubSteps = 20;

    [HideInInspector] public Vector3 externalForce;
    private Vector3 velocity = Vector3.zero;

    public void AddConnection(DroneAI other, float distance)
    {
        connections.Add(new Connection { target = other, desiredDistance = distance });
    }

    void FixedUpdate()
    {
        DrawConnections();
        // If this mother has no other-anchor neighbours, do not integrate: children still
        // spring toward her, she stays the origin of the triangle. ConnectTrios adds
        // mother–mother edges for the 5-trio layout, which *disables* this freeze even
        // though those edges are not sampled below.
        if (isAnchor && connections.FindAll(c => c.target.isAnchor).Count == 0) return;
        if (connections.Count == 0) return;

        // --- 1. Sensor (ideal UWB): enqueue ground-truth range error and bearing.
        // Control in section 2 never reads live positions — only these delayed queues.
        // Opposite of later branches: here mother↔mother is skipped (no inter-trio PID).
        // Mother still samples her own children; children sample everyone they are wired to.
        foreach (var c in connections)
        {
            if (c.target == null) continue;
            if (c.target.isAnchor && isAnchor) continue;

            Vector3 realVec = c.target.transform.position - transform.position;
            
            float realDist = realVec.magnitude;
            Vector3 directionAtSample = realVec.normalized;

            c.errorBuffer.Enqueue(realDist - c.desiredDistance);
            c.directionBuffer.Enqueue(directionAtSample);

            if (c.errorBuffer.Count > latencyFrames + 1) {
                c.errorBuffer.Dequeue();
                c.directionBuffer.Dequeue();
            }
        }

        // --- 2. Blind sub-step PID: same delayed sample for every inner step.
        // Dead reckoning subtracts own motion along the *old* bearing so the error
        // does not stay frozen for latencyFrames.
        float subStepDeltaTime = Time.fixedDeltaTime / pidSubSteps;
        
        for (int step = 0; step < pidSubSteps; step++)
        {
            Vector3 totalForce = Vector3.zero;

            foreach (var c in connections)
            {
                if (c.target == null || c.errorBuffer.Count <= latencyFrames) continue;

                float delayedError = c.errorBuffer.Peek(); 
                Vector3 delayedDir = c.directionBuffer.Peek(); 

                float closingSpeed = Vector3.Dot(velocity, delayedDir);

                float legitPredictedError = delayedError - (closingSpeed * subStepDeltaTime * step);

                if (!c.initialized) {
                    c.previousError = legitPredictedError;
                    c.initialized = true;
                }

                c.integral = Mathf.Clamp(c.integral + (legitPredictedError * subStepDeltaTime), -5f, 5f);
                float rawDerivative = -closingSpeed; 
                c.smoothedDerivative = Mathf.Lerp(c.smoothedDerivative, rawDerivative, derivativeSmoothing);
                c.previousError = legitPredictedError;

                // |error|^3 * sign: small range errors barely push; large ones dominate.
                float pFactor = Mathf.Pow(Mathf.Abs(legitPredictedError), 3f) * Mathf.Sign(legitPredictedError);
                float pidOutput = (P * pFactor) + (I * c.integral) + (D * c.smoothedDerivative);

                // Soft collision using the same predicted range (not Physics.OverlapSphere).
                float estimatedDist = (delayedError + c.desiredDistance) - (closingSpeed * subStepDeltaTime * step);
                if (estimatedDist < c.desiredDistance * 0.5f) {
                    float rep = Mathf.Pow(1f - (estimatedDist / (c.desiredDistance * 0.5f)), 2) * 400f;
                    pidOutput -= rep;
                }

                totalForce += delayedDir * pidOutput;
            }

            // Denominator is the full list, including mother↔mother edges that never
            // contributed force — those extra edges dilute the child springs.
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