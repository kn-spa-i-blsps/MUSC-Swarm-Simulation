using UnityEngine;

/// <summary>
/// Profil dostrojenia drona - jeden ScriptableObject moze byc wspoldzielony przez wiele dronow.
/// Tworz przez Assets > Create > MuscSwarm > Drone Tuning Profile.
/// </summary>
/// <remarks>
/// Skala: ~6 jednostek Unity = 1 metr (dron 3,5"). Predkosci sa w jednostkach/sek,
/// dystansy w jednostkach Unity.
/// </remarks>
[CreateAssetMenu(fileName = "DroneTuning", menuName = "MuscSwarm/Drone Tuning Profile", order = 0)]
public class DroneSettings : ScriptableObject
{
    // ----- SPEED ---------------------------------------------------------------

    [Header("Speed limits")]
    [Tooltip("Maksymalna predkosc pozioma w jednostkach Unity na sekunde (~6u = 1m).")]
    [Range(0.1f, 10f)]
    public float horizontalSpeed = 3f;

    [Tooltip("Maksymalna predkosc pionowa (uzywana w sterowaniu manualnym).")]
    [Range(0.1f, 10f)]
    public float verticalSpeed = 3f;

    [Tooltip("Predkosc obrotu w stopniach na sekunde (sterowanie manualne).")]
    [Range(10f, 360f)]
    public float yawSpeed = 100f;

    // ----- MODE ----------------------------------------------------------------

    [Header("Movement mode")]
    [Tooltip("PidSpring  - twarda formacja podtrzymywana przez PID po UWB.\n" +
             "VffOrca    - boidsy (VFF) + reciprokalne unikanie kolizji (ORCA).")]
    public DroneMovementMode movementMode = DroneMovementMode.PidSpring;

    // ----- PID (PidSpring) -----------------------------------------------------

    [Header("PID (PidSpring mode)")]
    [Tooltip("Czlon proporcjonalny - sila reakcji na blad dystansu UWB.")]
    [Range(0f, 200f)] public float pidP = 50f;

    [Tooltip("Czlon calkujacy - korekta stalego offsetu (uwaga, zbyt duzy = niestabilnosc).")]
    [Range(0f, 1f)] public float pidI = 0.05f;

    [Tooltip("Czlon rozniczkujacy - tlumienie oscylacji.")]
    [Range(0f, 50f)] public float pidD = 5f;

    [Tooltip("Wygladzanie pochodnej (Lerp factor). Mniejsza wartosc = silniejsze tlumienie szumu.")]
    [Range(0.01f, 1f)] public float derivativeSmoothing = 0.2f;

    [Header("PID dead-zone / settle")]
    [Tooltip("Pomijaj bledy < tej wartosci (cm). Eliminuje drgania od szumu UWB.")]
    [Range(0f, 10f)] public float deadzoneCm = 5f;

    [Tooltip("Pomijaj czlon D ponizej tej zmiany dystansu (jedn/s) - dodatkowy filtr szumu.")]
    [Range(0.01f, 1f)] public float minDThreshold = 0.05f;

    [Tooltip("Wygaszaj regulator gdy blad ~0 i ruch sasiada < tej predkosci (jedn/s).")]
    [Range(0f, 0.5f)] public float settleVelocity = 0.1f;

    // ----- MISSION ANCHOR ------------------------------------------------------

    [Header("Mission anchor spring (PidSpring mode)")]
    [Tooltip("Sila 'gumki' ciagnacej drona za duchem celu misji.")]
    [Range(0f, 100f)] public float missionP = 20f;

    [Tooltip("Tlumienie ruchu wzdluz osi misji - zapobiega przestrzeleniu duch.")]
    [Range(0f, 20f)] public float missionD = 2f;

    // ----- PHYSICS -------------------------------------------------------------

    [Header("Physics")]
    [Tooltip("Wspolczynnik utrzymania ruchu w nastepnej klatce. 1 = bez strat, 0 = pelne wygaszenie.")]
    [Range(0f, 1f)] public float velocityRetention = 0.05f;

    // ----- UWB -----------------------------------------------------------------

    [Header("UWB")]
    [Tooltip("Okres pomiarow UWB w ms. Wiekszy = wiekszy lag widziany przez Smith Predictor.")]
    [Range(10, 500)] public int uwbIntervalMs = 200;

    [Tooltip("Szum pomiarowy UWB (+/- jednostki). 0.1u ~= 1.6 cm w skali 6u/m.")]
    [Range(0f, 0.5f)] public float uwbNoiseAmplitude = 0.1f;

    // ----- OBSTACLE AVOIDANCE -------------------------------------------------

    [Header("Obstacle avoidance")]
    [Tooltip("Globalny mnoznik sily odpychajacej od przeszkod (Obstacle w scenie). 0 = wylacz, ~20 = mocne.")]
    [Range(0f, 200f)] public float obstacleAvoidanceStrength = 30f;

    [Tooltip("Zasieg sensowania jako mnoznik (radius_obstacle + buffer). 1 = sila zaczyna sie na styku. 1.5 = czuje przeszkode z odleglosci 50% wiekszej niz jej promien.")]
    [Range(1f, 3f)] public float obstacleSensingMultiplier = 1.6f;

    [Tooltip("Bufor bezpieczenstwa wokol drona dodawany do promienia przeszkody (jed Unity).")]
    [Range(0f, 3f)] public float droneAvoidBuffer = 0.8f;

    // ----- SWARM ---------------------------------------------------------------

    [Header("Swarm steering (VffOrca mode)")]
    public SwarmSteeringSettings swarm = new SwarmSteeringSettings();

    // ----- MISSION -------------------------------------------------------------

    [Header("Mission")]
    [Tooltip("Asset z lista waypointow misji. Mozesz go wspoldzielic miedzy dronami.")]
    public Mission mission;
}
