using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manual takeover. <see cref="CameraSwitcher"/> sets <see cref="isActive"/> on mothers only.
/// While active, <see cref="DroneAI"/> is disabled. This branch still uses
/// <c>Time.fixedDeltaTime / 2</c> inside <c>Update</c> — framerate-wrong; fixed on vff+orca.
/// </summary>
public class DroneMover : MonoBehaviour
{
    public bool isActive = false;
    private DroneAI ai;

    void Start()
    {
        ai = GetComponent<DroneAI>();
    }

    void Update()
    {
        if (isActive)
        {
            if (ai != null) ai.enabled = false;
            HandleInput();
        }
        else
        {
            if (ai != null) ai.enabled = true;
        }

        ResolveCollisions();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Drone")) return;

        DroneAI self = GetComponent<DroneAI>();
        DroneAI otherDrone = other.GetComponent<DroneAI>();

        string selfName = gameObject.name;
        string otherName = other.gameObject.name;

        Debug.Log($"COLLISION: {selfName} <-> {otherName}");

        // 🔥 wizualizacja miejsca kolizji
        Vector3 pointA = transform.position;
        Vector3 pointB = other.transform.position;
        Vector3 mid = (pointA + pointB) * 0.5f;

        Debug.DrawLine(pointA, pointB, Color.red, 1f);
        Debug.DrawRay(mid, Vector3.up * 0.5f, Color.yellow, 1f);
    }

    void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float forward = 0f;
        if (kb.wKey.isPressed) forward += 1f;
        if (kb.sKey.isPressed) forward -= 1f;

        float strafe = 0f;
        if (kb.dKey.isPressed) strafe += 1f;
        if (kb.aKey.isPressed) strafe -= 1f;

        float vertical = 0f;
        if (kb.spaceKey.isPressed) vertical += 1f;
        if (kb.leftCtrlKey.isPressed) vertical -= 1f;

        float yaw = 0f;
        if (kb.eKey.isPressed) yaw += 1f;
        if (kb.qKey.isPressed) yaw -= 1f;

        Vector3 move =
            transform.forward * forward +
            transform.right * strafe +
            transform.up * vertical;

        Vector3 horizontalMove = (transform.forward * forward + transform.right * strafe).normalized;
        horizontalMove *= ai.droneSettings.horizontalSpeed;

        // Ruch pionowy (Y)
        Vector3 verticalMove = transform.up * vertical * ai.droneSettings.verticalSpeed;

        // 3. Aplikacja ruchu i rotacji
        // Sumujemy ruch, mnożymy przez deltę i dodajemy do pozycji
        transform.position += (horizontalMove + verticalMove) * Time.fixedDeltaTime / 2;

        // Rotacja
        transform.Rotate(Vector3.up, yaw * ai.droneSettings.yawSpeed * Time.fixedDeltaTime, Space.Self);
    }

    void ResolveCollisions()
    {
        float radius = 1.5f;
        float strength = 8f;

        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        Vector3 push = Vector3.zero;

        foreach (var h in hits)
        {
            if (!h.CompareTag("Drone")) continue;
            if (h.transform == transform) continue;

            Vector3 dir = transform.position - h.transform.position;
            float dist = dir.magnitude;

            if (dist < 0.0001f) continue;

            // ile "w środku" jesteś
            float overlap = radius - dist;

            if (overlap > 0f)
            {
                push += dir.normalized * overlap;
            }
        }

        // 🔥 miękkie odpychanie w czasie
        transform.position += push * strength * Time.deltaTime;
    }
}