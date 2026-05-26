# Misje demonstracyjne (Demo Missions)

Wszystkie misje **konczacy sie w punkcie startowym (0,0,0)** - duch (ghost) wraca dokladnie tam skad wystartowal, wiec nadaja sie do petlowania (mozna je dlugo trzymac na scenie i sprawdzac jak roj sobie radzi).

Sa zaprojektowane pod `DemoDroneTuning` (`Assets/MuscSwarm/Assets/DemoDroneTuning.asset`) z kreconymi limitami:
- `horizontalSpeed = 20` u/s
- `verticalSpeed = 15` u/s
- `movementMode = VffOrca` (czyli to czego uzywasz)

Kazda misja ma `referenceTuning` ustawione na DemoDroneTuning, wiec inspector misji od razu pokazuje czy mieszczisz sie w limitach. Jak chcesz uzyc swojego wlasnego profilu drona - po prostu podmien `referenceTuning` w inspektorze misji albo wymuus inny `DroneSettings` w `Simulation`.

## Spis tras

| Plik | Ksztalt | Wymiary | Czas | Komentarz |
|---|---|---|---|---|
| `MissionDemo_Square.asset` | Kwadrat w XZ | 40x40 u | 24 s | Najprostszy test: 4 ostre zakrety o 90°. Pokazuje czy formacja sie nie rozjezdza w narozach. |
| `MissionDemo_Triangle.asset` | Trojkat rownoboczny w XZ | bok 40 u | 18 s | 3 ostre zakrety po 120°. Krotszy niz kwadrat, dobry sanity-check. |
| `MissionDemo_Octagon.asset` | Aproksymacja okregu (8 cieciw) | R=25 u | 24 s | Plynne zakreca - test ciaglego prowadzenia. Drony powinny rysowac gladka petle. |
| `MissionDemo_VerticalWave.asset` | Sinusoida pionowa | dlugosc 40 u, amplituda 15 u | 32 s | Test pionowych manewrow: zigzag gora-dol idac na wschod, potem wraca. Wymaga `verticalSpeed >= 4` u/s. |
| `MissionDemo_Spiral.asset` | Helisa wstepujaca + zstepujaca | R=20 u, wysokosc 40 u | 32 s | Najbardziej widowiskowa: dwa pelne obroty z jednoczesnym wznoszeniem (po 5 u/segment), potem opadaniem. Drony rysuja podwojny lej. |

## Jak uruchomic

1. W scenie znajdz GameObject z komponentem `Simulation`.
2. Otworz pole **`Global Drone Settings`** w inspektorze Simulation.
3. Przeciagnij `DemoDroneTuning.asset` zamiast `DefaultDroneTuning`.
4. W tym samym Simulation otworz pole **`Default Mission`** i przeciagnij wybrana misje demo (np. `MissionDemo_Spiral.asset`).
5. Play. Wszystkie trzy drony wystartuja z tej samej misji.

## Dlaczego suma waypointow = 0?

Misja w tym projekcie to lista **przesuniec wzgledem poprzedniego punktu** (nie pozycji absolutnych). Wiec zeby duch wrocil do startu, suma wszystkich wektorow `target` musi byc (0,0,0). Wszystkie misje demo to spelniaja - sprawdzilem na piechote.

## Modyfikacje

- Chcesz wieksza skale? Pomnoz wszystkie `target` przez ten sam czynnik. Suma dalej bedzie 0.
- Chcesz szybciej? Zmniejsz `timeStamp` (uwaga: za szybko = ghost ucieka dronom, kontroluj ostrzezenia w MissionEditor).
- Chcesz lekkie obrocenie? Pomnoz xz przez macierz rotacji - lazy way: zamien (x,z) → (z,-x) zeby obrocic o 90°.

## Test z przeszkodami

Wszystkie misje da sie polaczyc z `Obstacle` (component dodajesz do GameObjectu z radiusem) - drony beda omijaly. Polecam `MissionDemo_Spiral` z slupkiem (Obstacle, radius=4) postawionym w (10, 0, 0) - widac jak roj sie wykrzywia obchodzac.
