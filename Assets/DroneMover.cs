using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sterowanie manualne dronem z klawiatury. Aktywowany przez <see cref="CameraSwitcher"/>
/// (jeden dron na raz). Gdy aktywny, wylacza <see cref="DroneAI"/> zeby nie walczyc o pozycje.
/// </summary>
/// <remarks>
/// FIX: poprzednia wersja w Update() uzywala <c>Time.fixedDeltaTime / 2</c>,
/// co powodowalo ruch zalezny od framerate'u. Teraz Update() uzywa <c>Time.deltaTime</c>.
/// </remarks>
[RequireComponent(typeof(DroneAI))]
public class DroneMover : MonoBehaviour
{
    [Header("State")]
    [Tooltip("Czy ten dron jest aktualnie sterowany przez gracza (ustawiane przez CameraSwitcher).")]
    public bool isActive = false;

    [Header("Soft collisions")]
    [Tooltip("Promien spheryczny do soft-pushback miedzy dronami (jednostki).")]
    [Range(0.1f, 5f)] public float collisionRadius = 1.5f;

    [Tooltip("Sila odpychania per overlap unit.")]
    [Range(0f, 20f)] public float collisionStrength = 8f;

    DroneAI ai;

    void Awake() => ai = GetComponent<DroneAI>();

    void Update()
    {
        if (ai != null) ai.enabled = !isActive;

        if (isActive) HandleInput();

        ResolveCollisions();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Drone")) return;
        Debug.Log($"COLLISION: {gameObject.name} <-> {other.gameObject.name}");

        Vector3 a = transform.position;
        Vector3 b = other.transform.position;
        Vector3 mid = (a + b) * 0.5f;
        Debug.DrawLine(a, b, Color.red, 1f);
        Debug.DrawRay(mid, Vector3.up * 0.5f, Color.yellow, 1f);
    }

    void HandleInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || ai == null || ai.droneSettings == null) return;

        float forward = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        float strafe  = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float vert    = (kb.spaceKey.isPressed ? 1f : 0f) - (kb.leftCtrlKey.isPressed ? 1f : 0f);
        float yaw     = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);

        DroneSettings s = ai.droneSettings;
        float dt = Time.deltaTime;

        Vector3 horizontal = (transform.forward * forward + transform.right * strafe);
        if (horizontal.sqrMagnitude > 1f) horizontal.Normalize();
        Vector3 vertical = transform.up * vert;

        transform.position += horizontal * s.horizontalSpeed * dt + vertical * s.verticalSpeed * dt;
        transform.Rotate(Vector3.up, yaw * s.yawSpeed * dt, Space.Self);
    }

    void ResolveCollisions()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, collisionRadius);
        Vector3 push = Vector3.zero;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider h = hits[i];
            if (!h.CompareTag("Drone") || h.transform == transform) continue;

            Vector3 dir = transform.position - h.transform.position;
            float dist = dir.magnitude;
            if (dist < 1e-4f) continue;

            float overlap = collisionRadius - dist;
            if (overlap > 0f) push += dir.normalized * overlap;
        }

        transform.position += push * collisionStrength * Time.deltaTime;
    }
}
