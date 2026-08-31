using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Cycles the active follow-camera with C. The matching <see cref="DroneMover"/>
/// becomes the only manually flown drone; every other listed drone stays on AI.
/// <see cref="Simulation.SetupCameras"/> fills both lists with trio anchors only.
/// </summary>
public class CameraSwitcher : MonoBehaviour
{
    public List<Camera> cameras = new List<Camera>();
    public List<DroneMover> drones = new List<DroneMover>();

    private int currentIndex = 0;

    void Start()
    {
        Activate(0);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || cameras.Count == 0) return;

        if (kb.cKey.wasPressedThisFrame)
        {
            currentIndex = (currentIndex + 1) % cameras.Count;
            Activate(currentIndex);
        }
    }

    public void SetCameras(List<Camera> newCams)
    {
        cameras = newCams;
    }

    public void SetDrones(List<DroneMover> newDrones)
    {
        drones = newDrones;
    }

    void Activate(int index)
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            cameras[i].enabled = (i == index);

            if (i < drones.Count && drones[i] != null)
            {
                drones[i].isActive = (i == index);
            }
        }
    }
}