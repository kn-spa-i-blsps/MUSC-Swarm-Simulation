using UnityEngine;

/// <summary>
/// Equilateral trio. Mother <c>a</c> (<see cref="DroneAI.isAnchor"/>) is the only drone
/// wired to other trios. All three pairs get bidirectional rest-length edges.
/// </summary>
[System.Serializable]
public class DroneTrio
{
    /// <summary>Trio mother — the only drone used for inter-trio connections.</summary>
    public DroneAI a;
    public DroneAI b;
    public DroneAI c;

    /// <summary>
    /// Buduje trio z prefabu. b i c ulozone wokol a w trojkacie rownobocznym o boku d.
    /// </summary>
    public DroneTrio(GameObject prefab, float d, Vector3 offset)
    {
        a = Spawn(prefab, offset);
        a.isAnchor = true;

        b = Spawn(prefab, offset + new Vector3(d, 0f, 0f));
        c = Spawn(prefab, offset + new Vector3(d * 0.5f, 0f, d * 0.866f /* sin60 */));

        BindPair(a, b, d);
        BindPair(a, c, d);
        BindPair(b, c, d);
    }

    static DroneAI Spawn(GameObject prefab, Vector3 pos)
    {
        GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity);
        return go.GetComponent<DroneAI>();
    }

    static void BindPair(DroneAI x, DroneAI y, float distance)
    {
        x.AddConnection(y, distance);
        y.AddConnection(x, distance);
    }
}
