# MuscSwarm refactor - notatki migracyjne

Krotki przewodnik po tym co sie zmienilo i co masz zrobic raz w Unity Editorze
po pierwszym otworzeniu projektu po refaktorze.

## Co sie zmienilo (skrocie)

- `DroneSettings` i `Mission` to teraz **ScriptableObject** (nie inline structy).
  Po jednym z assetow lezy w `Assets/MuscSwarm/Assets/`:
  - `DefaultDroneTuning.asset` (zmiapowane wartosci ze starej sceny)
  - `DefaultMission.asset` (waypoint `(600, 0, 600)` w 12s, tak jak bylo)
- `DroneAI` jest cienkim orchestratorem - cala matematyka siedzi w
  `Assets/MuscSwarm/Runtime/` w namespace `MuscSwarm`:
  - `MissionGhost` - liniowy duch celu po waypointach
  - `UwbSampler` - timer probkowania UWB z losowym jitterem
  - `FormationPid` - PID + Smith Predictor (per sasiad), z poprawkami semantyki
  - `SwarmSteeringPipeline` - VFF -> ORCA dla trybu VffOrca
- `Simulation` ma teraz **custom Inspector** z tabelaryczna lista polaczen
  i pozycji spawnu trio (zamiast nieczytelnej listy struktur).
- `DroneAI` ma **runtime panel** w Inspectorze (Play Mode): pokazuje
  predkosc, tryb, liczbe polaczen, pary UWB.

## Co musisz zrobic raz w Unity

1. **Otworz projekt w Unity.** Edytor wykryje nowe pliki i przeimportuje assety.
2. **Otworz scene** (`Assets/readytrios.unity` lub `Assets/speedydrifttest.unity`).
   Zobaczysz:
   - Na `SimulationManager` field `globalDroneSettings` bedzie `None` (Missing reference).
   - Po dodaniu drona zobaczysz `droneSettings` i `mission` jako puste.
3. **Przeciagnij `DefaultDroneTuning.asset`** z `Assets/MuscSwarm/Assets/`
   do pola `Global Drone Settings` na `Simulation`. To wszystko, ze starych
   wartosci nic sie nie traci (DefaultDroneTuning ma je juz wszystkie).
4. **`DefaultMission.asset`** jest juz wpiety w DefaultDroneTuning, wiec
   nic z misja nie musisz robic.
5. (Opcjonalnie) **Otworz prefab `Drone.prefab`** i sprawdz czy `DroneAI`
   ma puste `droneSettings` - to OK, `Simulation` go wstrzykuje po Awake.

## Bugi naprawione

- `DroneMover.HandleInput` uzywal `Time.fixedDeltaTime` w `Update()` + dzielil
  przez 2 - teraz uzywa `Time.deltaTime`. Ruch reczny bedzie ~10x szybszy
  zgodny z `horizontalSpeed`. Jesli chcesz wolniej, zmniejsz `horizontalSpeed`
  w DefaultDroneTuning.
- Smith Predictor w `FormationPid` uzywa teraz realnego przesuniecia drona
  (z poprzedniej klatki), a nie "lastAppliedForce" ktora byla tylko jednym
  komponentem siły springa.
- Korekcja modelu Smitha robi teraz backtrack predykcji do momentu pomiaru
  (matematycznie spojnie), zamiast dodawac `neighborVelocity * age`.
- `missionP` i `missionD` (gumka misji) sa teraz w DroneSettings (range slidery),
  nie zaszyte jako stale `20` i `2`.
- `currentGyroDrift` - usuniety, nigdzie nie byl uzywany do nawigacji.
- `Simulation.FixedUpdate` nie pcha juz settings co klatke - SO sa referencja,
  zmiana wartosci jest natychmiastowa dla wszystkich dronow.
- `velocityRetention` zastapil pole `drag` (semantyka: ile ruchu zachowac
  vs ile wygas). Domyslna wartosc `0.05` = identyczna jak stara `drag=0.95`.

## Tryb sterowania

W `DroneTuning` mozesz przelaczac `Movement Mode`:
- `PidSpring` (default) - twarda formacja po UWB, zachowanie sztywne
- `VffOrca` - boidsy + reciprokalne unikanie kolizji (XZ)

## Tworzenie wlasnych profili

W oknie Project: **Right-click > Create > MuscSwarm > Drone Tuning Profile**
lub **MuscSwarm > Mission**. Mozesz miec rozne profile dla roznych eksperymentow.

## Pliki w skrocie

```
Assets/
  DroneAI.cs              - orchestrator MonoBehaviour (cienki)
  DroneSettings.cs        - ScriptableObject z profilem dostrojenia
  Mission.cs              - ScriptableObject z lista waypointow
  Simulation.cs           - manager sceny (spawning + lifecycle)
  UWBTransmission.cs      - wspolny model sieci UWB (Dictionary par)
  CameraSwitcher.cs       - przelacznik kamer + aktywnego drona
  DroneMover.cs           - sterowanie reczne (FIX framerate)
  Connection.cs           - dataclass {target, desiredDistance}
  DroneTrio.cs            - 3 drony w trojkacie rownobocznym
  DroneTrioList.cs        - lista wszystkich trio + ich polaczenia
  Swarm/
    SwarmRegistry.cs        - globalny rejestr (linear scan)
    SwarmSteeringSettings.cs - inline w DroneSettings (boidsy + ORCA)
    VffSteering.cs          - Reynolds boids steering
    Orca2D.cs               - uproszczony ORCA-like (XZ plane)
  MuscSwarm/
    Runtime/
      MissionGhost.cs           - duch celu po waypointach
      UwbSampler.cs             - per-drone timer probkowania
      FormationPid.cs           - PID + Smith Predictor
      SwarmSteeringPipeline.cs  - VFF -> ORCA composition
    Editor/
      SimulationEditor.cs       - custom Inspector z tabelami
      DroneAIEditor.cs          - runtime stats panel
    Assets/
      DefaultDroneTuning.asset  - preset z wartosciami ze sceny
      DefaultMission.asset      - waypoint (600,0,600) w 12s
```
