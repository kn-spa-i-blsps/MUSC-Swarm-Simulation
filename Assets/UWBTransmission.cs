using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wspolny model sieci UWB. Trzyma najswiezszy zmierzony dystans miedzy kazda para dronow
/// oraz timestamp pomiaru (do Smith Predictor age compensation).
/// </summary>
/// <remarks>
/// To NIE jest sieć z transportowym lagiem - push/get sa natychmiastowe.
/// Caly "lag" wynika z faktu, ze drony probkuja swoje pomiary tylko co
/// <c>uwbIntervalMs</c>. Smith Predictor w <see cref="MuscSwarm.FormationPid"/>
/// kompensuje wiek pomiaru korzystajac z timestampu.
/// </remarks>
public class UWBTransmission : MonoBehaviour
{
    /// <summary>Pojedynczy pomiar dystansu miedzy para dronow.</summary>
    public readonly struct MeasurementData
    {
        public readonly float distance;
        public readonly float timestamp; // Time.fixedTime

        public MeasurementData(float distance, float timestamp)
        {
            this.distance = distance;
            this.timestamp = timestamp;
        }
    }

    /// <summary>Niekierunkowy klucz pary dronow (Drone A &lt;-&gt; Drone B == Drone B &lt;-&gt; Drone A).</summary>
    readonly struct DronePair : System.IEquatable<DronePair>
    {
        readonly int idA;
        readonly int idB;

        public DronePair(DroneAI a, DroneAI b)
        {
            int id1 = a.GetInstanceID();
            int id2 = b.GetInstanceID();
            idA = Mathf.Min(id1, id2);
            idB = Mathf.Max(id1, id2);
        }

        public bool Equals(DronePair other) => idA == other.idA && idB == other.idB;
        public override bool Equals(object obj) => obj is DronePair other && Equals(other);
        public override int GetHashCode() => System.HashCode.Combine(idA, idB);
    }

    readonly Dictionary<DronePair, MeasurementData> network = new Dictionary<DronePair, MeasurementData>();

    /// <summary>Liczba aktualnie sledzonych par dronow (dla diagnostyki / Inspectora).</summary>
    public int TrackedPairs => network.Count;

    /// <summary>Wpycha najnowszy pomiar dystansu do sieci. Nadpisuje poprzedni dla tej samej pary.</summary>
    public void PushMeasurement(DroneAI sender, DroneAI target, float measuredDistance)
    {
        if (sender == null || target == null) return;
        network[new DronePair(sender, target)] = new MeasurementData(measuredDistance, Time.fixedTime);
    }

    /// <summary>Probuje pobrac ostatni znany pomiar dla pary dronow.</summary>
    public bool TryGetDistance(DroneAI a, DroneAI b, out MeasurementData data)
    {
        return network.TryGetValue(new DronePair(a, b), out data);
    }

    /// <summary>Czysci caly stan sieci (np. przy restarcie sceny).</summary>
    public void Clear() => network.Clear();

    /// <summary>Diagnostyczne wypisanie stanu sieci.</summary>
    public void PrintNetworkStatus()
    {
        foreach (var entry in network)
        {
            Debug.Log($"Relation {entry.Key.GetHashCode()} | dist={entry.Value.distance:F2} | t={entry.Value.timestamp:F3}s");
        }
    }
}
