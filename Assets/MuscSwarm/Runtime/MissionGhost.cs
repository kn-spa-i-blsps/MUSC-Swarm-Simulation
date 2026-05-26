using UnityEngine;

namespace MuscSwarm
{
    /// <summary>
    /// "Duch celu" misji - liniowa interpolacja po waypointach misji.
    /// Pojedyncza instancja per dron. Update co FixedUpdate.
    /// </summary>
    public sealed class MissionGhost
    {
        Mission mission;
        Vector3 segmentStart;
        Vector3 ghostPosition;
        int currentTargetIndex;
        float timeInCurrentLeg;

        /// <summary>Czy duch ma jeszcze segmenty do wykonania.</summary>
        public bool HasMoreSegments => mission != null
            && mission.targets != null
            && currentTargetIndex < mission.targets.Count;

        /// <summary>Aktualna pozycja ducha celu (worldspace).</summary>
        public Vector3 Position => ghostPosition;

        public int CurrentTargetIndex => currentTargetIndex;
        public float TimeInCurrentLeg => timeInCurrentLeg;

        /// <summary>
        /// Reset z poczatkowa pozycja drona. Wywolaj raz w Start().
        /// </summary>
        public void Reset(Mission newMission, Vector3 startPosition)
        {
            mission = newMission;
            ghostPosition = startPosition;
            segmentStart = startPosition;
            currentTargetIndex = 0;
            timeInCurrentLeg = 0f;
        }

        /// <summary>
        /// Przesuwa ducha o krok deltaTime wzdluz biezacego segmentu.
        /// Przechodzi do nastepnego waypointa po przekroczeniu czasu segmentu.
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (!HasMoreSegments) return;

            MissionTarget waypoint = mission.targets[currentTargetIndex];
            float duration = Mathf.Max(0.01f, waypoint.timeStamp);

            // Liniowa predkosc na tym segmencie:
            Vector3 stepPerSecond = waypoint.target / duration;
            ghostPosition += stepPerSecond * deltaTime;
            timeInCurrentLeg += deltaTime;

            if (timeInCurrentLeg >= duration)
            {
                // Snap do dokladnego konca segmentu - eliminuje drift od akumulacji floatow.
                ghostPosition = segmentStart + waypoint.target;
                segmentStart = ghostPosition;
                currentTargetIndex++;
                timeInCurrentLeg = 0f;
            }
        }
    }
}
