using System.Collections.Generic;
using UnityEngine;

namespace MuscSwarm
{
    /// <summary>
    /// Regulator formacji oparty o PID na pomiarach dystansu UWB do sasiadow,
    /// rozszerzony o Smith Predictor (model wewnetrzny dystansu kompensujacy lag UWB).
    /// </summary>
    /// <remarks>
    /// Jedna instancja per dron. Trzyma slownik stanu per sasiad (PID + Smith).
    /// Naprawione wzgledem starej wersji w DroneAI.cs:
    ///   - Smith Predictor uzywa realnego przesuniecia drona (z poprzedniej klatki),
    ///     a nie "lastAppliedForce" ktora reprezentowala tylko jeden komponent spring force.
    ///   - Korekcja modelu robi backtrack predykcji do momentu pomiaru (age),
    ///     zamiast dodawac neighborVelocity*age w niejasny sposob.
    ///   - Mission anchor spring (missionP, missionD) jest sparametryzowany przez DroneSettings,
    ///     nie zaszyty jako stale 20/2.
    /// </remarks>
    public sealed class FormationPid
    {
        const float RateLerpFactor = 0.2f;        // wygladzanie estymaty predkosci sasiada
        const float ModelCorrectionLerp = 0.15f;  // miekkosc korekcji modelu Smitha z UWB

        struct NeighborState
        {
            // --- Smith Predictor ---
            public float predictedDist;          // biezaca predykcja dystansu (po Smith)
            public float relativeRate;           // szacunkowa predkosc zmiany dystansu (jed/s)
            public float lastUwbDistance;
            public float lastUwbTime;
            public bool initialized;

            // --- PID ---
            public float integralError;
            public float lastObservedDist;       // do liczenia surowej pochodnej
            public float smoothedDerivative;
        }

        readonly Dictionary<DroneAI, NeighborState> states = new Dictionary<DroneAI, NeighborState>();

        public int TrackedNeighbors => states.Count;

        public void ClearState() => states.Clear();

        /// <summary>
        /// Liczy wektor "sily sprezynowej" (faktycznie: target velocity * dt) zbiorczo
        /// dla wszystkich polaczen + sily ducha celu misji.
        /// </summary>
        /// <param name="self">Wlasciciel regulatora.</param>
        /// <param name="missionGhost">Pozycja ducha celu misji w worldspace.</param>
        /// <param name="currentVelocity">Aktualna oszacowana predkosc wlasna (jed/s).</param>
        /// <param name="tuning">Profil dostrojenia.</param>
        /// <param name="network">Wspolna siec UWB.</param>
        /// <param name="dt">Time.fixedDeltaTime.</param>
        /// <param name="now">Time.fixedTime.</param>
        public Vector3 ComputeStepDisplacement(
            DroneAI self,
            Vector3 missionGhost,
            Vector3 currentVelocity,
            DroneSettings tuning,
            UWBTransmission network,
            float dt,
            float now)
        {
            Vector3 selfPos = self.transform.position;
            Vector3 total = Vector3.zero;

            // ---- 1. Mission anchor spring -------------------------------------------------
            // "Gumka" ciagnaca drona za duchem celu. Bez * dt - mission spring zachowuje sie
            // jako "sila per step" (zgodnie z oryginalna semantyka, gdzie PID spring mialy * dt
            // a mission spring nie - aby mission dominowal i przyklejal drona do ducha).
            Vector3 missionError = missionGhost - selfPos;
            Vector3 missionAnchor = missionError * tuning.missionP - currentVelocity * tuning.missionD;
            total += missionAnchor;

            // ---- 2. Springs po UWB do kazdego sasiada ------------------------------------
            float deadzoneUnits = tuning.deadzoneCm * 0.06f; // 1 cm = 0.06 jedn w skali 6u/m
            var connections = self.connections;

            for (int i = 0; i < connections.Count; i++)
            {
                Connection conn = connections[i];
                DroneAI target = conn.target;
                if (target == null) continue;

                if (conn.weight <= 0f) continue;

                Vector3 toTarget = target.transform.position - selfPos;
                float realDist = toTarget.magnitude;
                if (realDist < 1e-4f) continue;
                Vector3 dir = toTarget / realDist;

                NeighborState state;
                if (!states.TryGetValue(target, out state))
                {
                    // Pierwsze spotkanie z sasiadem: seeduj predykcje aktualnym realnym dystansem
                    // (zapobiega kopniakowi PID na pierwszych klatkach zanim przyjdzie UWB).
                    state = new NeighborState
                    {
                        predictedDist = realDist,
                        lastObservedDist = realDist,
                    };
                }

                // ---- 2a. Advance Smith Predictor ------------------------------------------
                // Tylko relativeRate. Matematyczny dowod:
                //   d(dist)/dt = (vTarget - vSelf) . dir       (definicja)
                //              = relativeRate                  (mierzone z UWB)
                // Sam selfMove kasuje sie z (estymowanym) targetMove - byloby double counting.
                state.predictedDist += state.relativeRate * dt;

                // ---- 2b. UWB correction (Smith feedback) ---------------------------------
                if (network != null && network.TryGetDistance(self, target, out var uwb))
                {
                    // Update rate estimate from successive samples:
                    if (state.lastUwbTime > 0f)
                    {
                        float dtUwb = uwb.timestamp - state.lastUwbTime;
                        if (dtUwb > 0f)
                        {
                            float rawRate = (uwb.distance - state.lastUwbDistance) / dtUwb;
                            state.relativeRate = Mathf.Lerp(state.relativeRate, rawRate, RateLerpFactor);
                        }
                    }
                    state.lastUwbDistance = uwb.distance;
                    state.lastUwbTime = uwb.timestamp;

                    if (!state.initialized)
                    {
                        // Pierwszy pomiar: synchronizuj model do realu.
                        state.predictedDist = uwb.distance;
                        state.initialized = true;
                    }
                    else
                    {
                        // Backtrack predykcji do momentu pomiaru (age sekund temu),
                        // licz blad i miekko sciagaj model do prawdy.
                        float age = Mathf.Max(0f, now - uwb.timestamp);
                        float predictionAtMeas = state.predictedDist - state.relativeRate * age;
                        float measError = uwb.distance - predictionAtMeas;
                        state.predictedDist += measError * ModelCorrectionLerp;
                    }
                }

                float estimatedDist = state.predictedDist;

                // ---- 2c. PID na predykcji dystansu ----------------------------------------
                float error = estimatedDist - conn.desiredDistance;
                if (Mathf.Abs(error) < deadzoneUnits) error = 0f;

                float pTerm = error * tuning.pidP;

                state.integralError += error * dt;
                float iTerm = state.integralError * tuning.pidI;

                float rawDeriv = (estimatedDist - state.lastObservedDist) / dt;
                if (Mathf.Abs(rawDeriv) < tuning.minDThreshold) rawDeriv = 0f;
                state.smoothedDerivative = Mathf.Lerp(state.smoothedDerivative, rawDeriv, tuning.derivativeSmoothing);
                float dTerm = state.smoothedDerivative * tuning.pidD;

                state.lastObservedDist = estimatedDist;

                float pidOutput = pTerm + iTerm + dTerm;

                // Wygas calkowicie gdy zarowno blad jak i ruch sasiada sa znikome.
                if (error == 0f && Mathf.Abs(rawDeriv) < tuning.settleVelocity)
                    pidOutput = 0f;

                Vector3 contribution = dir * (pidOutput * dt * conn.weight);
                total += contribution;

                states[target] = state;
            }

            return total;
        }
    }
}
