# MUSC Swarm Simulation

Unity 6 simulation of a **MUSC-style drone swarm**: equilateral **trios** (mother + two children) held by range springs and chained into a larger formation. Motion is on the **XZ** plane.

This README is the map of **all four remotes**. Class names and files **differ by branch** — do not copy a type from one branch onto another and expect it to compile.

| | |
| --- | --- |
| **Repo** | [kn-spa-i-blsps/MUSC-Swarm-Simulation](https://github.com/kn-spa-i-blsps/MUSC-Swarm-Simulation) |
| **Editor** | Unity `6000.2.7f2` |
| **Pipeline** | URP |
| **Input** | New Input System (`Keyboard.current`) |
| **Remotes** | `main`, `mission-i-moduly`, `vff+orca`, `dyn-dist-change` (nothing else) |

```bash
git branch -a          # confirm
git checkout <branch>  # switch
```

English comments are on the C# entry points of **each** branch (same content as this README). `vff+orca` / `dyn-dist-change` already had long Polish remarks on UWB, ORCA, and Smith — those were left; public `<summary>` tags on the orchestrator files are English.

---

## Start here (new to this repo)

You do **not** need to read every section on day one. Do this:

1. **Pick a branch.** Day-to-day research is **`dyn-dist-change`** (missions, UWB delay, VFF+ORCA, edge weights). **`main` is the old monolith** — useful to understand history, not the place to add features. `git checkout dyn-dist-change`
2. **Open Unity `6000.2.7f2`.** Wait for import. Project uses URP and the **new Input System** (keyboard via `Keyboard.current`).
3. **Open a scene** — `readytrios` or `SwarmVffOrcaDemo` (those files are not on `main`). On `Simulation`, if `globalDroneSettings` is missing, drag `Assets/MuscSwarm/Assets/DefaultDroneTuning.asset`. Scene needs a `UWBTransmission` object (or `DroneAI` will find one).
4. **Play.** **C** cycles the mother camera / manual drone. WASD + Space/Ctrl + QE to fly that mother; AI on the others stays on.
5. **Then read** “Who pulls on whom” below. That table is the one thing that makes the rest of the code make sense.

If you only have **`main`** checked out: scene is `SampleScene`, four scripts under `Assets/`, no Mission/UWB classes. Tick **Update PID** after changing gains.

**Do not** copy a class from one branch onto another. File lists and types diverged on purpose.

### Glossary

| Term | Meaning |
| --- | --- |
| **Trio** | Three drones in an equilateral triangle. |
| **Mother / anchor** | Drone `a` of a trio (`isAnchor`). Only mothers are linked to other trios. |
| **Children** | Drones `b` and `c`. Hold the triangle; not in the inter-trio graph. |
| **Connection** | Directed edge: neighbour + rest length (`desiredDistance`). On `dyn-dist-change` also `weight`. |
| **Ghost** | Invisible point that flies the mission polyline; springs chase it. Not a visible drone. |
| **UWB** | Simulated range radio: in-process dictionary, optional delay. Not a real radio stack. |
| **Smith predictor** | Internal model of range that compensates for stale UWB samples. |
| **PidSpring vs VffOrca** | Hard formation on UWB springs vs flocking + collision avoidance (then connection springs again). |

**Where to look in code (feature branches):** `Simulation` spawns · `DroneAI` chooses the mode · `FormationPid` / `SwarmSteeringPipeline` move · `UWBTransmission` is the radio · `Connection` is the graph.

**Where to look on `main`:** `Simulation` + `DroneAI` only. PID, delay queues, and wind all live in those two files.

---

## Scale

On **`mission-i-moduly` and later**, the comment in `Simulation.cs` is the contract: a **3.5″ drone**, **~6 Unity units = 1 real metre**. Speeds in `DroneSettings` are units/second.

`0.1` units of UWB noise ≈ **1.6 cm**. Dead-zone centimetres are converted with `cm * 0.06` (because `1 cm = 0.06` units at 6 u/m).

**`main` has no scale helper.** Inspector distances are raw Unity units.

---

## Branch graph

History splits after `92da574` (IDE files gitignored):

```
83d3c1b / 92da574
    ├─ main                          monolith + leftover junk-file commits
    └─ mission-i-moduly              rewrite: modules, Mission, UWB dict, Smith-ish PID
            └─ vff+orca              ScriptableObject tuning, VFF+ORCA, real radio delay
                    └─ dyn-dist-change   Connection.weight + runtime rest-length API
```

| Branch | What you actually get |
| --- | --- |
| **`main`** | Four scripts. Hardcoded 5-trio spawn. Cubic-P PID. Delay = **physics frames** in per-edge queues. Perlin wind. No Mission, no `UWBTransmission`. |
| **`mission-i-moduly`** | `DroneTrio` / `DroneTrioList`, inspector `SpawnPoint` + `ConnectionData` (1-based). Shared UWB **dictionary, no radio delay**. Mission ghost. Spring + PID with an internal range model. `DroneSettings` is a **serializable class**, not a ScriptableObject. Wind code is **commented out**. `currentGyroDrift` is updated and **never applied**. |
| **`vff+orca`** | Thin `DroneAI`. Math in `Assets/MuscSwarm/Runtime/` and `Assets/Swarm/`. `DroneSettings` / `Mission` are ScriptableObjects. Two modes: `PidSpring` and `VffOrca`. `uwbIntervalMs` + `uwbTransmissionDelayMs` (in-flight queues). Pose broadcast for VFF/ORCA. Obstacles. Custom inspectors. See `Assets/MuscSwarm/REFACTOR_NOTES.md`. |
| **`dyn-dist-change`** | Same as `vff+orca`, plus `Connection.weight` and `SetDistanceBetween` (triangle inequality). Mother→child weight defaults to **0**. |

Tip of the research line: **`dyn-dist-change`**. `main` is the older controller.

---

## Who pulls on whom (this is the important difference)

Every branch **wires** the same topology: intra-trio edges on all three pairs, inter-trio edges **only mother ↔ mother**.

What the **controller does** with those edges is inverted between `main` and the modular line:

| | **`main`** | **`mission-i-moduly` and `vff+orca`** | **`dyn-dist-change`** |
| --- | --- | --- | --- |
| Mother → child | **Sampled and PID’d** | **Skipped** (`if (self.isAnchor && !target.isAnchor)`) | **Skipped** (`weight == 0`) |
| Child → mother / child → child | Sampled and PID’d | PID / spring | Scaled by `weight` (default 1) |
| Mother → mother | **Not sampled** (no PID force) | **Regulated** (that is the super-formation) | Regulated if `weight > 0` (default 1) |
| Lone mother (no other-mother edge) | **Frozen** (early return) | Moves via mission ghost + remaining springs | Same as vff |

On `main`, `ConnectTrios()` still adds mother–mother edges. Those edges **only** disable the freeze check. They also sit in `connections.Count`, so mothers **divide PID force by extra unused edges** (`totalForce / connections.Count`).

On `vff+orca`, `ConnectionSpring` (VffOrca mode) uses the **same** mother-skips-children `isAnchor` test. Bearing is **ground truth**; range may come from delayed UWB if `useUwbNeighborSensing` is on. On `dyn-dist-change` that skip is `weight <= 0` instead of `isAnchor`.

---

## Trio geometry

All branches: **a** at the trio origin (`isAnchor`), **b** at `(d, 0, 0)`, **c** at the third vertex of an equilateral triangle of side `d` on XZ.

- **`main` / `mission-i-moduly`:** `c = (d/2, 0, 0.87·d)` — `0.87` is a rounded `√3/2`.
- **`vff+orca` / `dyn-dist-change`:** `c = (d/2, 0, 0.866·d)` with an explicit `sin 60°` comment.

Each pair gets **two** directed `Connection`s (A→B and B→A) with the same `desiredDistance`.

---

## `main` (monolith)

```
Simulation ──spawn──► DroneTrio × 5
    │                     ├─ a (anchor) ──► CameraSwitcher + DroneMover
    │                     ├─ b
    │                     └─ c
    └── SyncDroneSettings / wind / latencyFrames
              │
         DroneAI: per-edge FIFO (range error + bearing) → cubic-P PID → XZ
```

| File | Role |
| --- | --- |
| `Assets/Simulation.cs` | Spawn, hardcoded graph, push P/I/D/wind/latency, register cameras. |
| `Assets/DroneAI.cs` | Delayed-range PID, cubic P, sub-steps, Y lock. `Connection` **includes** PID state and queues. |
| `Assets/DroneMover.cs` | WASD / Space / Ctrl / QE. Soft `OverlapSphere`. Disables `DroneAI` while `isActive`. |
| `Assets/CameraSwitcher.cs` | **C** cycles cameras; one `DroneMover.isActive`. Anchors only. |

`Assets/TutorialInfo/` is the Unity template, not the swarm.

### Spawn and graph on `main`

`SpawnTrios()` uses five hardcoded XZ points. `ConnectTrios()` then:

```
        0
       / \
      9   9
     /     \
    1 ---7--- 2
     \       / \
      9     7   9
       \   /     \
        3 ------- 4
              9
```

7 and 9 are **design rest lengths**, not measured spawn spacing.

`AutoSpawnTrios()` grids `trioCount` trios with `trioAutoSpawnSpacing`, then **still** calls the five-edge `ConnectTrios()`. If `trioCount != 5` this indexes wrongly or describes the wrong graph. Leave auto-spawn off unless you rewrite `ConnectTrios()`.

### `DroneAI.FixedUpdate` on `main`

1. Draw debug lines (including unsampled mother–mother edges — cyan/red by **true** range error).
2. If `isAnchor` and there is **no** other-anchor neighbour → return (frozen mother).
3. **Sensor:** enqueue true `(range − desired, unit bearing)`. Skip if **both** ends are anchors. Trim to `latencyFrames + 1`.
4. **PID** only after `errorBuffer.Count > latencyFrames`. Peek is the **oldest** sample (delay ≈ `latencyFrames` ticks).
5. **`pidSubSteps` inner steps**, same peeked sample. Predicted error: `delayedError − dot(v, delayedBearing) · Δt_sub · stepIndex`.
6. Output: `P·|e|³·sign(e) + I·∫e + D·smoothed(−closingSpeed)`. Cubic P is intentional.
7. If predicted range `< 0.5·desired`, subtract quadratic repulsion (gain 400). Independent of `DroneMover` overlap.
8. `acc = totalForce / connections.Count + externalForce`, zero Y, `drag^(1/subSteps)`, clamp Y to 0.

`globalLatency` is cast to **int frames**, not milliseconds. Default FixedUpdate 50 Hz → `5` ≈ 100 ms.

Wind (every `Update` via `SyncPhysicsSettings`): spatial Perlin on XZ plus `Random.insideUnitSphere * vibrationStrength`. Tick **Update PID** in the Inspector to copy gains; wind/latency copy every frame.

### Keys on `main`

**C** next camera · **WASD** strafe · **Space / Left Ctrl** up/down · **Q / E** yaw.

---

## `mission-i-moduly`

`Simulation` builds `DroneTrioList(prefab, trioDistance, connections, spawnPositions)`.

- `SpawnPoint` `{x, z}` — mother position.
- `ConnectionData` `{x, y, d}` — **1-based** trio indices, rest length between **mothers**.

**Index check bug** (this branch only):

```csharp
1 <= cd.x && cd.x <= dl.Count && 1 <= cd.x && cd.y <= dl.Count
```

The third test repeats `x`, so **`y < 1` is not rejected**. `vff+orca` replaced this with a proper range check and a warning.

`UWBTransmission` is an in-process dict keyed by sorted instance IDs. `PushMeasurement` **overwrites immediately** (`Time.fixedTime`). No in-flight delay, no pose channel. Noise on push is hardcoded `Random.Range(-0.1f, 0.1f)`.

Each `DroneAI.FixedUpdate`:

1. `UpdateDrift` — random walk into `currentGyroDrift` (**unused** afterwards).
2. `UpdateUWB` — every `uwbIntervalMs`, push noisy ranges for all connections.
3. `CalculateMissionForce` — advances ghost `estimatedPosition` along `Mission.targets` (each `target` is a **relative** segment, `timeStamp` is duration in seconds), **returns zero**.
4. `CalculateSpringForce` — PD to the ghost (**hardcoded** `missionP = 20`, `missionD = 2`) + per-neighbour PID on a Smith-style predicted range.
5. `totalMovement = totalForce - totalForce * drag`, cap by `horizontalSpeed * dt`.

Smith on this branch updates the internal range with **`lastAppliedForce` along current LOS** (only the spring component from the previous step). `vff+orca` `FormationPid` replaced that with **last frame’s real displacement**. Neighbour range-rate is a lerp of UWB Δd/Δt. Model error uses `internalModelDist + neighborVelocity * age`.

Commit message on the branch: mission ghost and springs were **not fully synchronised**.

`DroneSettings` lives as an **inline object** on `Simulation.globalDroneSettings`. `FixedUpdate` still copies that reference onto every drone every physics tick (the one-shot `updateDroneSettings` flag is commented out). On `vff+orca` the asset is a shared reference, so that per-tick copy goes away.

---

## `vff+orca`

`DroneAI` is an orchestrator. Shared tuning: `Assets/MuscSwarm/Assets/DefaultDroneTuning.asset`. After first import, drag that asset onto `Simulation` if the reference is missing (`REFACTOR_NOTES.md`).

`velocityRetention` replaced `drag`: **`0.05` retention ≡ old `drag = 0.95`**. Do not paste old drag numbers into the new field.

### Runtime (`MuscSwarm`)

| Class | Job |
| --- | --- |
| `MissionGhost` | Linear ghost along ScriptableObject waypoints (same semantics as mission-branch ghost). |
| `UwbSampler` | Per-drone timer + random phase. Pushes **ranges** (all `connections`) and **pose** (self position + `SimulatedVelocity`) into `UWBTransmission`, both with `uwbTransmissionDelayMs`. |
| `FormationPid` | PidSpring mode. Per-neighbour PID + Smith. Uses **real last displacement**. Backtracks the model to **measurement time**. `missionP` / `missionD` from `DroneSettings`. Skips mother→child. |
| `SwarmSteeringPipeline` | VffOrca: discover by **true** radius → `NeighborSample` (truth or delayed pose) → VFF preferred vel → ORCA-like safe vel → `ConnectionSpring` P+D correction (not a VFF vote). |
| `ConnectionSpring` | Holds `connections` rest lengths **regardless of VFF perception radius**. GT bearing; optional UWB range. Skips mother→child. |
| `ObstacleAvoidance` / `ObstacleRegistry` | Extra planar repulsion from scene `Obstacle`s; applied in both modes before the speed cap. |
| `NeighborSample` | Resolved neighbour pose/velocity (ground truth or delayed UWB). |
| `DroneTrail` | Optional `TrailRenderer` path viz, attached by `Simulation` or by hand. |
| `MuscSwarm/Editor/*` | Custom inspectors: connection/spawn tables, runtime `DroneAI` stats, tuning/mission assets. |

### `Assets/Swarm/`

- **`VffSteering`** — Reynolds: separation (`1/dist` inside `separationRadius`), alignment, cohesion, goal = mission ghost. Output is a **velocity** of length `horizontalSpeed`.
- **`Orca2D`** — **not** full ORCA (no LP on half-planes). `result += 0.5 * u` per neighbour, clamp max speed.
- **`SwarmRegistry`** — linear neighbour scan.
- **`SwarmSteeringSettings`** — radii, VFF weights, ORCA horizon, `connectionWeight` / `connectionDamping`, `useUwbNeighborSensing`.

### UWB delay (commit `b448336`)

Two clocks **add**:

1. `uwbIntervalMs` — how often the sender measures.
2. `uwbTransmissionDelayMs` — how long a packet sits in `inFlight` before `TryGetDistance` / `TryGetPose` can see it.

Timestamp stored on delivery is **send time** (before the wait), so Smith’s “age” includes the radio wait.

`useUwbNeighborSensing == false` → VFF/ORCA cheat with live `transform.position`.

### Scenes on this branch

`readytrios`, `speedydrifttest`, `SwarmVffOrcaDemo` (not present on `main`).

`DroneMover` uses `Time.deltaTime` (the old `fixedDeltaTime/2` in `Update` bug is fixed here). Collision radius/strength are Inspector fields. `CameraSwitcher.switchKey` defaults to **C** but is no longer hardcoded.

---

## `dyn-dist-change`

Diff vs `vff+orca` is small: `Connection.cs`, `AddConnection` sets `weight = Connection.DefaultWeight(owner, target)`, PID/spring multiply by `weight`.

| Default `weight` | |
| --- | --- |
| Mother → child | `0` (mother does not fight the triangle) |
| Child → mother, child → child, mother → mother | `1` |

`SetDistanceBetween(a, b, d, force)` writes both directions. Unless `force`, it rejects `d` that break the **triangle inequality** against shared neighbours. Edges with **weight 0 both ways** are ignored in that check.

`SetWeightBetween` / `SetWeightsBetween` for symmetric or asymmetric weights.

---

## Dependency map

```
Keyboard (Input System)              Physics tags / OverlapSphere (main + movers)
        │
        ▼
 CameraSwitcher ◄── Simulation ──► drone prefab
                        │                │
                        │           DroneMover ──► enables/disables DroneAI
                        │                │
                        │                ├── connections[]     formation graph
                        │                ├── externalForce     main: wind only
                        │                └── droneSettings     mission+ only
                        │
 mission+ :  UWBTransmission  ◄── UpdateUWB / UwbSampler
                  ▲
                  └── FormationPid / ConnectionSpring / SwarmSteeringPipeline

 vff+ :  VffSteering ──► Orca2D ──► ConnectionSpring
              ▲
              └── NeighborSample (SwarmRegistry + optional delayed pose)
```

Manifest packages the swarm actually uses: **Input System**, **URP**, **Physics**. `mission-i-moduly` `Simulation.cs` imports Visual Scripting; it is unused.

---

## How to run

**`main`:** open `Assets/Scenes/SampleScene.unity`. Assign `Drone.prefab`. Prefab needs tag `Drone`, a collider (trigger for logs), child `Camera` on the mother. Play. Tick **Update PID** after changing gains.

**Feature branches:** open Unity, wait for import, assign `DefaultDroneTuning.asset` on `Simulation` if missing, open `readytrios` / `SwarmVffOrcaDemo`. Need a `UWBTransmission` in the scene (`DroneAI` will `FindFirstObjectByType` if the field is empty).

---

## Pitfalls

- **`main` latency is frames.** Changing Fixed Timestep changes real delay. Modular branches use milliseconds.
- **`main` cubic P.** `globalP` is not a linear gain.
- **`main` auto-spawn + hardcoded graph.**
- **`main` two collision systems:** predicted-range kick and `DroneMover.ResolveCollisions`.
- **`mission-i-moduly` `DroneMover`** uses `Time.fixedDeltaTime / 2` inside `Update` (framerate-wrong). Fixed on `vff+orca`.
- **`mission-i-moduly` `ConnectionData.y < 1`** not validated.
- **`mission-i-moduly` Smith** uses `lastAppliedForce`, not real motion.
- **Do not copy `drag` into `velocityRetention`.**
- **ORCA here is a sketch**, not Berg/Snape/Manocha.
- **UWB is not a radio stack** — one dictionary (plus in-flight queues on vff+).

---

## What this is not

Not PX4/ROS/firmware. Not networked multiplayer. Not full ORCA.
)
