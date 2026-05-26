using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Przelacznik kamer i aktywnego drona do sterowania manualnego.
/// Po nacisnieciu <c>C</c> przechodzi do kolejnej kamery (cykliczne) i aktywuje
/// odpowiadajacego <see cref="DroneMover"/>.
/// </summary>
public class CameraSwitcher : MonoBehaviour
{
    [Header("Wired by Simulation at Start()")]
    [Tooltip("Lista kamer w roju (auto-wypelniana przez Simulation).")]
    public List<Camera> cameras = new List<Camera>();

    [Tooltip("Lista DroneMover odpowiadajacych kamerom (auto-wypelniana przez Simulation).")]
    public List<DroneMover> drones = new List<DroneMover>();

    [Header("Input")]
    [Tooltip("Klawisz przelaczania kamery.")]
    public Key switchKey = Key.C;

    int currentIndex;

    void Start() => Activate(0);

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || cameras.Count == 0) return;

        if (kb[switchKey].wasPressedThisFrame)
        {
            currentIndex = (currentIndex + 1) % cameras.Count;
            Activate(currentIndex);
        }
    }

    public void Activate(int index)
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i] != null) cameras[i].enabled = (i == index);
            if (i < drones.Count && drones[i] != null) drones[i].isActive = (i == index);
        }
    }
}
