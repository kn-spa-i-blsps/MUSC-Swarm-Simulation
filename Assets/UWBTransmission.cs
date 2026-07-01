using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wspolny model sieci UWB z PRAWDZIWYM lagiem transmisji.
/// Pomiar wyslany przez nadawce trafia do sieci od razu, ale staje sie widoczny dla
/// odbiorcow (<see cref="TryGetDistance"/>) dopiero po uplywie zadanego opoznienia transmisji.
/// </summary>
/// <remarks>
/// Dwa niezalezne, sumujace sie zrodla "opoznienia" widziane przez regulator:
/// 1) <c>uwbIntervalMs</c> (w <see cref="MuscSwarm.UwbSampler"/>) - jak czesto nadawca w ogole
///    probkuje nowy pomiar.
/// 2) <c>uwbTransmissionDelayMs</c> (tutaj) - ile trwa zanim wyslany pomiar dotrze do odbiorcy.
/// Smith Predictor w <see cref="MuscSwarm.FormationPid"/> kompensuje sumaryczny wiek pomiaru
/// (od chwili faktycznego zmierzenia realnego dystansu) korzystajac z timestampu.
///
/// Oprocz kanalu par-do-par (dystanse, uzywane przez PidSpring) siec ma tez kanal
/// broadcastowy (wlasna pozycja+predkosc, uzywany opcjonalnie przez VffOrca -
/// patrz <see cref="BroadcastPose"/> / <see cref="TryGetPose"/>).
/// </remarks>
public class UWBTransmission : MonoBehaviour
{
    /// <summary>Pojedynczy pomiar dystansu miedzy para dronow, juz dostarczony (widoczny) w sieci.</summary>
    public readonly struct MeasurementData
    {
        /// <summary>Zmierzony (zaszumiony) dystans.</summary>
        public readonly float distance;

        /// <summary>Moment faktycznego wykonania pomiaru (Time.fixedTime) - PRZED transmisja.</summary>
        public readonly float timestamp;

        public MeasurementData(float distance, float timestamp)
        {
            this.distance = distance;
            this.timestamp = timestamp;
        }
    }

    /// <summary>Pomiar "w locie" - juz wyslany, jeszcze nie dostarczony.</summary>
    readonly struct PendingMeasurement
    {
        public readonly float distance;
        public readonly float sentAtTime;
        public readonly float arrivalTime;

        public PendingMeasurement(float distance, float sentAtTime, float arrivalTime)
        {
            this.distance = distance;
            this.sentAtTime = sentAtTime;
            this.arrivalTime = arrivalTime;
        }
    }

    /// <summary>Ostatnia znana (dostarczona) pozycja+predkosc nadawcy z jego wlasnego broadcastu.</summary>
    public readonly struct PoseData
    {
        public readonly Vector3 position;
        public readonly Vector3 velocity;

        /// <summary>Moment faktycznego wykonania pomiaru wlasnej pozycji (Time.fixedTime) - PRZED transmisja.</summary>
        public readonly float timestamp;

        public PoseData(Vector3 position, Vector3 velocity, float timestamp)
        {
            this.position = position;
            this.velocity = velocity;
            this.timestamp = timestamp;
        }
    }

    /// <summary>Broadcast pozycji "w locie" - juz wyslany, jeszcze nie dostarczony.</summary>
    readonly struct PendingPose
    {
        public readonly Vector3 position;
        public readonly Vector3 velocity;
        public readonly float sentAtTime;
        public readonly float arrivalTime;

        public PendingPose(Vector3 position, Vector3 velocity, float sentAtTime, float arrivalTime)
        {
            this.position = position;
            this.velocity = velocity;
            this.sentAtTime = sentAtTime;
            this.arrivalTime = arrivalTime;
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

    // Ostatni JUZ DOSTARCZONY pomiar widoczny dla odbiorcow.
    readonly Dictionary<DronePair, MeasurementData> delivered = new Dictionary<DronePair, MeasurementData>();

    // Pomiary "w locie" - czekajace na uplyniecie opoznienia transmisji, per para, w kolejnosci wyslania (FIFO).
    readonly Dictionary<DronePair, Queue<PendingMeasurement>> inFlight = new Dictionary<DronePair, Queue<PendingMeasurement>>();

    // Ostatni JUZ DOSTARCZONY broadcast pozycji, per nadawca (widoczny dla kazdego odbiorcy jednakowo).
    readonly Dictionary<DroneAI, PoseData> deliveredPoses = new Dictionary<DroneAI, PoseData>();

    // Broadcasty pozycji "w locie", per nadawca, w kolejnosci wyslania (FIFO).
    readonly Dictionary<DroneAI, Queue<PendingPose>> inFlightPoses = new Dictionary<DroneAI, Queue<PendingPose>>();

    /// <summary>Liczba par ktore maja juz dostarczony (czytelny) pomiar - dla diagnostyki / Inspectora.</summary>
    public int TrackedPairs => delivered.Count;

    /// <summary>Liczba pomiarow aktualnie "w powietrzu" (wyslanych, jeszcze niedostarczonych) - dla diagnostyki.</summary>
    public int InFlightCount
    {
        get
        {
            int total = 0;
            foreach (var q in inFlight.Values) total += q.Count;
            return total;
        }
    }

    /// <summary>Liczba dronow ktorych pozycja z broadcastu jest juz znana - dla diagnostyki / Inspectora.</summary>
    public int TrackedPoses => deliveredPoses.Count;

    /// <summary>Liczba broadcastow pozycji aktualnie "w powietrzu" - dla diagnostyki.</summary>
    public int InFlightPoseCount
    {
        get
        {
            int total = 0;
            foreach (var q in inFlightPoses.Values) total += q.Count;
            return total;
        }
    }

    /// <summary>
    /// Wysyla pomiar do sieci. Pomiar NIE jest natychmiast widoczny dla odbiorcow -
    /// staje sie dostepny przez <see cref="TryGetDistance"/> dopiero po uplywie
    /// <paramref name="transmissionDelaySeconds"/>.
    /// </summary>
    public void PushMeasurement(DroneAI sender, DroneAI target, float measuredDistance, float transmissionDelaySeconds)
    {
        if (sender == null || target == null) return;

        float now = Time.fixedTime;
        DronePair pair = new DronePair(sender, target);

        if (transmissionDelaySeconds <= 0f)
        {
            delivered[pair] = new MeasurementData(measuredDistance, now);
            return;
        }

        if (!inFlight.TryGetValue(pair, out Queue<PendingMeasurement> queue))
        {
            queue = new Queue<PendingMeasurement>();
            inFlight[pair] = queue;
        }
        queue.Enqueue(new PendingMeasurement(measuredDistance, now, now + transmissionDelaySeconds));
    }

    /// <summary>
    /// Probuje pobrac ostatni JUZ DOSTARCZONY pomiar dla pary dronow.
    /// Przy okazji "dorecza" wszystkie pomiary w kolejce ktorych czas transmisji juz uplynal.
    /// </summary>
    public bool TryGetDistance(DroneAI a, DroneAI b, out MeasurementData data)
    {
        DronePair pair = new DronePair(a, b);
        DeliverArrived(pair);
        return delivered.TryGetValue(pair, out data);
    }

    /// <summary>Przesuwa z kolejki "w locie" do "dostarczonych" wszystkie pomiary ktore juz doleciały.</summary>
    void DeliverArrived(DronePair pair)
    {
        if (!inFlight.TryGetValue(pair, out Queue<PendingMeasurement> queue) || queue.Count == 0) return;

        float now = Time.fixedTime;
        while (queue.Count > 0 && queue.Peek().arrivalTime <= now)
        {
            PendingMeasurement arrived = queue.Dequeue();
            // Timestamp = moment FAKTYCZNEGO pomiaru (sprzed transmisji) - Smith Predictor
            // liczy wiek wzgledem tego momentu, wiec transmisja poprawnie dolicza sie do lagu.
            delivered[pair] = new MeasurementData(arrived.distance, arrived.sentAtTime);
        }
    }

    /// <summary>
    /// Nadawca rozglasza (broadcastuje) swoja aktualna (zaszumiona) pozycje+predkosc do wszystkich
    /// potencjalnych odbiorcow. Widoczne przez <see cref="TryGetPose"/> dopiero po uplywie
    /// <paramref name="transmissionDelaySeconds"/> - tak samo jak w kanale par-do-par.
    /// </summary>
    public void BroadcastPose(DroneAI sender, Vector3 position, Vector3 velocity, float transmissionDelaySeconds)
    {
        if (sender == null) return;

        float now = Time.fixedTime;

        if (transmissionDelaySeconds <= 0f)
        {
            deliveredPoses[sender] = new PoseData(position, velocity, now);
            return;
        }

        if (!inFlightPoses.TryGetValue(sender, out Queue<PendingPose> queue))
        {
            queue = new Queue<PendingPose>();
            inFlightPoses[sender] = queue;
        }
        queue.Enqueue(new PendingPose(position, velocity, now, now + transmissionDelaySeconds));
    }

    /// <summary>
    /// Probuje pobrac ostatni JUZ DOSTARCZONY broadcast pozycji danego nadawcy.
    /// Przy okazji dorecza wszystkie broadcasty ktorych czas transmisji juz uplynal.
    /// </summary>
    public bool TryGetPose(DroneAI sender, out PoseData data)
    {
        if (sender == null)
        {
            data = default;
            return false;
        }

        DeliverArrivedPoses(sender);
        return deliveredPoses.TryGetValue(sender, out data);
    }

    void DeliverArrivedPoses(DroneAI sender)
    {
        if (!inFlightPoses.TryGetValue(sender, out Queue<PendingPose> queue) || queue.Count == 0) return;

        float now = Time.fixedTime;
        while (queue.Count > 0 && queue.Peek().arrivalTime <= now)
        {
            PendingPose arrived = queue.Dequeue();
            deliveredPoses[sender] = new PoseData(arrived.position, arrived.velocity, arrived.sentAtTime);
        }
    }

    /// <summary>Czysci caly stan sieci (dostarczone i w locie, oba kanaly) - np. przy restarcie sceny.</summary>
    public void Clear()
    {
        delivered.Clear();
        inFlight.Clear();
        deliveredPoses.Clear();
        inFlightPoses.Clear();
    }

    /// <summary>Diagnostyczne wypisanie stanu sieci.</summary>
    public void PrintNetworkStatus()
    {
        foreach (var entry in delivered)
        {
            Debug.Log($"Relation {entry.Key.GetHashCode()} | dist={entry.Value.distance:F2} | t={entry.Value.timestamp:F3}s");
        }
    }
}
