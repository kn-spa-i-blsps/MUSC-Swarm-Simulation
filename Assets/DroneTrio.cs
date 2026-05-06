using UnityEngine;

[System.Serializable]
public class DroneTrio
{
    public DroneAI a, b, c;

    public DroneTrio(GameObject prefab, float d, Vector3 offset)
    {
        a = Create(prefab, offset + new Vector3(0, 0, 0));
        a.isAnchor = true;

        b = Create(prefab, offset + new Vector3(d, 0, 0));
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