using UnityEngine;

/// <summary>
/// Directed formation edge. <see cref="weight"/> scales PID / ConnectionSpring on this
/// owner only (asymmetric). Defaults: mother→child 0, everything else 1.
/// </summary>
[System.Serializable]
public class Connection
{
    [Tooltip("Sasiad - drugi dron w parze.")]
    public DroneAI target;

    [Tooltip("Zadana odleglosc do sasiada (jednostki Unity).")]
    [Min(0.01f)] public float desiredDistance;

    [Tooltip("Waga [0..1]: jak bardzo wlasciciel polaczenia dba o utrzymanie dystansu.")]
    [Range(0f, 1f)] public float weight = 1f;

    /// <summary>
    /// Default owner→target weight when an edge is created.
    /// Mother→child is 0 so the mother does not fight the triangle; children hold it.
    /// </summary>
    /// <remarks>
    /// Matka-matka: 1. Matka-dziecko w trojce: 0. Dziecko-matka w trojce: 1.
    /// </remarks>
    public static float DefaultWeight(DroneAI owner, DroneAI target)
    {
        if (owner == null || target == null) return 1f;
        if (owner.isAnchor && !target.isAnchor) return 0f;
        return 1f;
    }

    public void SetDesiredDistance(float distance)
    {
        desiredDistance = distance;
    }

    public void SetWeight(float newWeight)
    {
        weight = Mathf.Clamp01(newWeight);
    }

    /// <summary>Szuka polaczenia owner -> target na liscie connections.</summary>
    public static Connection Find(DroneAI owner, DroneAI target)
    {
        if (owner == null || target == null) return null;

        var list = owner.connections;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].target == target) return list[i];
        }

        return null;
    }

    /// <summary>Ustawia zadany dystans na polaczeniu from -> to.</summary>
    public static bool TrySetDesiredDistance(DroneAI from, DroneAI to, float distance)
    {
        Connection c = Find(from, to);
        if (c == null) return false;
        c.SetDesiredDistance(distance);
        return true;
    }

    // ----- Walidacja geometryczna (nierownosc trojkata) ------------------------------

    /// <summary>
    /// Sprawdza czy zadany dystans a-b jest geometrycznie osiagalny wzgledem wspolnych
    /// sasiadow (nierownosc trojkata). Dla kazdego drona c polaczonego aktywnie z a i b musi
    /// zachodzic: |d(a,c) - d(b,c)| &lt;= distance &lt;= d(a,c) + d(b,c).
    /// Krawedzie z waga 0 w obu kierunkach sa ignorowane (nie sa egzekwowane przez regulator).
    /// </summary>
    public static bool IsDistanceFeasible(DroneAI a, DroneAI b, float distance)
    {
        if (a == null || b == null) return false;

        var list = a.connections;
        for (int i = 0; i < list.Count; i++)
        {
            DroneAI c = list[i].target;
            if (c == null || c == b) continue;

            if (!TryGetEnforcedDistance(a, c, out float dAC)) continue;
            if (!TryGetEnforcedDistance(b, c, out float dBC)) continue;

            float min = Mathf.Abs(dAC - dBC);
            float max = dAC + dBC;
            if (distance < min || distance > max)
            {
                Debug.LogWarning(
                    $"[Connection] Dystans {distance:F2} miedzy '{a.name}' i '{b.name}' niemozliwy: " +
                    $"wspolny sasiad '{c.name}' wymusza zakres [{min:F2} .. {max:F2}] " +
                    $"(d({a.name},{c.name})={dAC:F2}, d({b.name},{c.name})={dBC:F2}).");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Zwraca zadany dystans krawedzi x-y jesli jest ona egzekwowana
    /// (istnieje wpis w ktoryms kierunku z waga &gt; 0).
    /// </summary>
    static bool TryGetEnforcedDistance(DroneAI x, DroneAI y, out float distance)
    {
        distance = 0f;
        Connection xy = Find(x, y);
        Connection yx = Find(y, x);
        if (xy == null && yx == null) return false;

        bool enforced = (xy != null && xy.weight > 0f) || (yx != null && yx.weight > 0f);
        if (!enforced) return false;

        distance = xy != null ? xy.desiredDistance : yx.desiredDistance;
        return true;
    }

    /// <summary>Ustawia wage polaczenia from -> to.</summary>
    public static bool TrySetWeight(DroneAI from, DroneAI to, float newWeight)
    {
        Connection c = Find(from, to);
        if (c == null) return false;
        c.SetWeight(newWeight);
        return true;
    }

    /// <summary>
    /// Ustawia zadany dystans w obu kierunkach miedzy dwoma dronami.
    /// Odrzuca zmiane (zwraca false + warning w konsoli), jesli dystans lamie nierownosc
    /// trojkata wzgledem wspolnych sasiadow. <paramref name="force"/> pomija walidacje.
    /// </summary>
    public static bool SetDistanceBetween(DroneAI a, DroneAI b, float distance, bool force = false)
    {
        if (!force && !IsDistanceFeasible(a, b, distance)) return false;

        bool okA = TrySetDesiredDistance(a, b, distance);
        bool okB = TrySetDesiredDistance(b, a, distance);
        return okA && okB;
    }

    /// <summary>Ustawia te sama wage w obu kierunkach miedzy dwoma dronami.</summary>
    public static bool SetWeightBetween(DroneAI a, DroneAI b, float weight)
    {
        bool okA = TrySetWeight(a, b, weight);
        bool okB = TrySetWeight(b, a, weight);
        return okA && okB;
    }

    /// <summary>Ustawia wagi osobno dla kazdego kierunku (np. matka->dziecko != dziecko->matka).</summary>
    public static bool SetWeightsBetween(DroneAI a, DroneAI b, float weightAtoB, float weightBtoA)
    {
        bool okA = TrySetWeight(a, b, weightAtoB);
        bool okB = TrySetWeight(b, a, weightBtoA);
        return okA && okB;
    }
}
