using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// One mission segment: <see cref="target"/> is a relative displacement from the
/// start of the leg, <see cref="timeStamp"/> is duration in seconds (not a clock time).
/// </summary>
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
