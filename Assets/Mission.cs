using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class MissionTarget
{
    public Vector3 target;
    public float timeStamp;
}

[System.Serializable]
public class Mission
{
    public List<MissionTarget> targets = new List<MissionTarget>();
}
