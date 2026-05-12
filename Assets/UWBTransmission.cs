using UnityEngine;
using System.Collections.Generic;

public class UWBTransmission : MonoBehaviour
{
    // Struktura przechowująca dane o pojedynczym pomiarze między dwoma dronami
    public struct MeasurementData
    {
        public float distance;
        public float timestamp; // Czas symulacji (Time.fixedTime)

        public MeasurementData(float distance, float timestamp)
        {
            this.distance = distance;
            this.timestamp = timestamp;
        }
    }

    // Klucz do słownika, który reprezentuje parę dronów (niezależnie od kolejności)
    private struct DronePair
    {
        public int idA;
        public int idB;

        public DronePair(DroneAI a, DroneAI b)
        {
            // Sortujemy ID, aby para (0,1) i (1,0) była traktowana jako ten sam klucz
            int id1 = a.GetInstanceID();
            int id2 = b.GetInstanceID();
            idA = Mathf.Min(id1, id2);
            idB = Mathf.Max(id1, id2);
        }

        // Standardowe metody dla structa używanego jako klucz w Dictionary
        public override bool Equals(object obj) => obj is DronePair other && idA == other.idA && idB == other.idB;
        public override int GetHashCode() => System.HashCode.Combine(idA, idB);
    }

    // "Tablica" przechowująca najświeższe dane o dystansach w roju
    private Dictionary<DronePair, MeasurementData> networkState = new Dictionary<DronePair, MeasurementData>();

    /// <summary>
    /// Dron wywołuje tę funkcję, aby wysłać swój pomiar do "sieci"
    /// </summary>
    public void PushMeasurement(DroneAI sender, DroneAI target, float measuredDistance)
    {
        DronePair pair = new DronePair(sender, target);
        
        // Zawsze nadpisujemy – słownik przechowa najświeższą informację
        // Używamy Time.fixedTime, bo ustaliliśmy, że to Twój "zegar prawdy"
        networkState[pair] = new MeasurementData(measuredDistance, Time.fixedTime);
        
        // Opcjonalny debug:
        // Debug.Log($"[Network] Update {sender.name}-{target.name}: {measuredDistance:F2}u at {Time.fixedTime:F2}s");
    }

    /// <summary>
    /// Pozwala sprawdzić, jaki jest ostatni znany dystans między dwiema jednostkami
    /// </summary>
    public bool TryGetDistance(DroneAI a, DroneAI b, out MeasurementData data)
    {
        return networkState.TryGetValue(new DronePair(a, b), out data);
    }

    // Przykład wizualizacji stanu sieci w konsoli (możesz wywołać np. klawiszem)
    public void PrintNetworkStatus()
    {
        foreach (var entry in networkState)
        {
            Debug.Log($"Relacja {entry.Key.idA} <-> {entry.Key.idB} | Dystans: {entry.Value.distance:F2} | Czas: {entry.Value.timestamp:F3}");
        }
    }
}