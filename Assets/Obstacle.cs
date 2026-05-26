using MuscSwarm;
using UnityEngine;

/// <summary>
/// Sferyczna przeszkoda dla roju. Dodaj ten komponent do dowolnego GameObjectu
/// (np. Cube albo Sphere) i ustaw <see cref="radius"/>. Drony beda omijac przeszkode
/// w obu trybach (PidSpring i VffOrca).
/// </summary>
/// <remarks>
/// Avoidance jest sferyczny w plaszczyznie XZ (drony leca plasko). Zaden collider
/// nie jest wymagany - sila odpychajaca jest kalkulowana czysto z position + radius.
/// </remarks>
[DisallowMultipleComponent]
public class Obstacle : MonoBehaviour
{
    [Header("Geometry")]
    [Tooltip("Promien sfery przeszkody (jednostki Unity; ~6u = 1m).")]
    [Min(0.1f)] public float radius = 2f;

    [Header("Avoidance tuning")]
    [Tooltip("Mnoznik sily odpychajacej dla tej konkretnej przeszkody (1 = neutralne, >1 = silniej, <1 = slabiej).")]
    [Range(0f, 5f)] public float repulsionStrength = 1f;

    [Header("Visual")]
    [Tooltip("Kolor gizmo (widoczny tylko w Scene View).")]
    public Color gizmoColor = new Color(1f, 0.4f, 0.1f, 0.35f);

    void OnEnable() => ObstacleRegistry.Register(this);
    void OnDisable() => ObstacleRegistry.Unregister(this);

    void OnDrawGizmos()
    {
        Color fill = gizmoColor;
        Color wire = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);

        Gizmos.color = fill;
        Gizmos.DrawSphere(transform.position, radius);
        Gizmos.color = wire;
        Gizmos.DrawWireSphere(transform.position, radius);

        // Pokaz tez strefe sensowania (gdzie drony zaczynaja juz reagowac).
        Gizmos.color = new Color(wire.r, wire.g, wire.b, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radius * 1.6f);
    }
}
