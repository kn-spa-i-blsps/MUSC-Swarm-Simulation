using UnityEngine;

/// <summary>
/// Wybor trybu sterowania pojedynczym dronem.
/// </summary>
public enum DroneMovementMode
{
    /// <summary>PID + sprezynowa formacja po UWB. Twarde trzymanie struktury.</summary>
    PidSpring = 0,

    /// <summary>Boidsy (VFF) + reciprokalne unikanie kolizji (ORCA, 2D).</summary>
    VffOrca = 1,
}

/// <summary>
/// Parametry sterowania rojowego dla trybu VffOrca. Trzymane inline w DroneSettings.
/// </summary>
[System.Serializable]
public class SwarmSteeringSettings
{
    // ----- VFF -----------------------------------------------------------------

    [Header("VFF weights (Reynolds boids)")]
    [Tooltip("Sila separacji - jak mocno dron odpycha sie od bliskich sasiadow.")]
    [Range(0f, 10f)] public float separationWeight = 2.5f;

    [Tooltip("Sila alignment - dazenie do dopasowania predkosci do sredniej sasiadow.")]
    [Range(0f, 10f)] public float alignmentWeight = 1f;

    [Tooltip("Sila cohesion - dazenie do srodka masy sasiadow.")]
    [Range(0f, 10f)] public float cohesionWeight = 1f;

    [Tooltip("Sila ciagniecia do celu misji.")]
    [Range(0f, 10f)] public float goalWeight = 2f;

    [Header("VFF radii (units)")]
    [Tooltip("Promien percepcji - sasiedzi w tym promieniu liczone do VFF.")]
    [Range(0.1f, 30f)] public float perceptionRadius = 8f;

    [Tooltip("Promien separacji - tylko sasiedzi w tym promieniu odpychaja.")]
    [Range(0.1f, 10f)] public float separationRadius = 2.5f;

    // ----- ORCA ----------------------------------------------------------------

    [Header("ORCA (XZ plane)")]
    [Tooltip("Promien kolizyjny drona dla ORCA (jednostki Unity).")]
    [Range(0.1f, 3f)] public float agentRadius = 0.6f;

    [Tooltip("Promien wyszukiwania sasiadow w ORCA (jednostki Unity).")]
    [Range(0.5f, 20f)] public float orcaNeighborRadius = 6f;

    [Tooltip("Horyzont czasowy ORCA w sekundach. Wiekszy = wczesniejsze unikanie kolizji.")]
    [Range(0.1f, 5f)] public float orcaTimeHorizon = 1.5f;

    // ----- FORMATION CONNECTIONS -------------------------------------------------

    [Header("Formation connections (kotwice trio + polaczenia miedzy-trio)")]
    [Tooltip("Sila P (proporcjonalna, jedn/s na jednostke bledu dystansu) sprezyny utrzymujacej zadane dystanse " +
             "z listy Connections (te same polaczenia co w PidSpring: trojkat trio + zadane dystanse miedzy " +
             "kotwicami roznych trio). Dziala NIEZALEZNIE od Perception Radius - polaczenie jest trzymane nawet " +
             "jesli sasiad jest daleko poza zasiegiem VFF.\n" +
             "0 = wylacz (formacja emerguje tylko z Cohesion/Separation, moze 'rozjezdzac' sie na duzych dystansach).\n" +
             "Wysoka wartosc = priorytet nad Goal Weight (dron wolniej realizuje misje, mocniej trzyma formacje). " +
             "UWAGA: bez odpowiedniego Connection Damping wysoka wartosc oscyluje (przestrzela).")]
    [Range(0f, 20f)] public float connectionWeight = 6f;

    [Tooltip("Sila D (tlumienie) sprezyny polaczenia - redukuje predkosc zbliżania/oddalania sie od sasiada, " +
             "zapobiega przestrzelaniu i oscylacji przy wysokim Connection Weight. Startuj od ~1/3 Connection Weight.")]
    [Range(0f, 10f)] public float connectionDamping = 2f;

    // ----- NEIGHBOR SENSING -----------------------------------------------------

    [Header("Neighbor sensing (realizm)")]
    [Tooltip("WLACZONE: pozycje/predkosci sasiadow pochodza z symulowanego UWB radiobroadcastu " +
             "(ten sam szum i lag transmisji co w PidSpring - patrz sekcja UWB). Realistyczne, ale " +
             "steering moze byc 'szarpany' przy duzym lagu/szumie.\n" +
             "WYLACZONE: idealna znajomosc pozycji sasiadow (ground truth) - czysty test algorytmu steeringu, " +
             "odcietego od warstwy sensorycznej.")]
    public bool useUwbNeighborSensing = true;

    // ----- DEBUG ---------------------------------------------------------------

    [Header("Debug")]
    [Tooltip("Rysuj wektory steering (preferred / safe / aktualny ruch) w Scene View.")]
    public bool drawSteeringVectors = false;
}
