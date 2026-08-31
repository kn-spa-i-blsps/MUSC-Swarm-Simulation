using UnityEngine;

/// <summary>
/// Equilateral trio. Drone <c>a</c> is the mother (<see cref="DroneAI.isAnchor"/>).
/// Children <c>b</c>/<c>c</c> are not linked to other trios.
/// </summary>
[System.Serializable]
public class DroneTrio
{
    public DroneAI a, b, c;

    public DroneTrio(GameObject prefab, float d, Vector3 offset)
    {
        a = Create(prefab, offset + new Vector3(0, 0, 0));
        a.isAnchor = true;

        b = Create(prefab, offset + new Vector3(d, 0, 0));
        // 0.87 ≈ √3/2. Later branches use 0.866 (sin 60°).
        c = Create(prefab, offset + new Vector3(d / 2f, 0, 0.87f * d));

        ConnectInside(a, b, d);
        ConnectInside(a, c, d);
        ConnectInside(b, c, d);
    }

    DroneAI Create(GameObject prefab, Vector3 pos)
    {
        GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity, null);
        return go.GetComponent<DroneAI>();
    }

    void ConnectInside(DroneAI x, DroneAI y, float d)
    {
        x.AddConnection(y, d);
        y.AddConnection(x, d);
    }
}