# Audit: Enemy AI, Pathfinding, Procedural Generation

- **Date:** 2026-10-01
- **Branch:** `Sarbo` (working tree, including uncommitted edits to `DungeonMapGenerator.cs` and `EnemySpawner.cs`)
- **Scope:** Describes current behaviour only. It contains no fixes or proposals.
- **Sources:** C# scripts plus serialized values from `RoguelikeMode.unity`, the prefabs and `Mapparametersso.asset`. Where a prefab or scene overrides a script default, this doc uses the serialized value and gives the script default in parentheses.
- **Paths** are relative to `Indie Sim/Assets/`.

---

## 0. Summary (TL;DR)

| Topic | Current state |
|---|---|
| Pathfinding used by any enemy | **None.** Every moving enemy steers in a straight line toward or away from the player, with no wall awareness. |
| Pathfinding code present | `JumpFloodPathfinding.cs` sits on `Enemy 2` and `Enemy 3` prefabs. It computes a flow field that **no script reads**, and the spawner does not spawn those prefabs. |
| NavMesh | Not used anywhere. |
| Enemy types spawned in dungeons | Melee fodder (`Enemy 1`), ranged `RangedEnemy` (TriangleEnemy) and `CuthuluEye` (mini-boss) |
| Boss | `MiniBoss1` (BossEnemy, "pinball"), loaded in `BossArena` via `BossDefinition` |
| Dungeon generator in use | `Scripts/Dungeon Generator/DungeonMapGenerator.cs` (graph of rooms → tiles) |
| Dead/legacy generator code | `Scripts/Dungeon Generator/New dungeon Generator/*` and `new.cs`. No scene or prefab uses them. |
| Dungeon graph topology | A tree: start → main artery (with corner rooms) → end. Distributive rooms are inserted into artery segments, and dead-end leaf rooms branch off them. |
| Determinism | Room layout uses `System.Random` (unseeded at runtime). Spill, foliage, spawner placement, relics and the key use `UnityEngine.Random`. |

---

## 1. File map

### 1.1 Active (wired into scenes/prefabs)

| File | Role | Attached to |
|---|---|---|
| `Scripts/Dungeon Generator/DungeonMapGenerator.cs` | Layout, tiles, spawner placement, teleporter, relics, key | `RoguelikeMode.unity`, `BossArena.unity`, `Test Scenes/CasualMode.unity`, `_Showcase/DungeonRevealShowcase.unity`, `Prefabs/Map_Generator.prefab` |
| `Scripts/Dungeon Generator/Mapparametersso.cs` (+ `.asset`) | Layout parameters (ScriptableObject) | Referenced by the generator |
| `Scripts/Managers/RoguelikeManager.cs` | Calls generation, scales difficulty, clears the dungeon | RoguelikeMode |
| `Scripts/Enemy/EnemySpawner.cs` | Budget-based enemy spawner ("piñata"), damageable | `Prefabs/Enemies/Enemy Spawner.prefab` |
| `Scripts/Enemy/ActivateEnemySpawner.cs` (class `PlayerSpawnerActivator`) | Activates spawners near the player | `Prefabs/Temp -Player.prefab` |
| `Scripts/PlayerController/ActivateEnemies.cs` | Proximity activation list for melee enemies | `Prefabs/Temp -Player.prefab` |
| `Scripts/Enemy/Enemy.cs` | Melee enemy health, contact damage, knockback, loot | `Enemy`, `Enemy 1/2/3` prefabs |
| `Scripts/Enemy/EnemyMovement.cs` | Melee chase, spring-lunge, separation | `Enemy`, `Enemy 1/2/3` prefabs |
| `Scripts/Enemy/EnemyDeath.cs` | Corpse and cleanup | Enemy prefabs |
| `Scripts/Enemy/Ranged_Enemy/TriangleEnemy.cs` (+ Animator, Juice) | Ranged FSM enemy | `Prefabs/Enemies/RangedEnemy/RangedEnemy.prefab` |
| `Scripts/Enemy/eyeEnemey/CthuluEyeEnemy.cs` (class `CthulhuEyeEnemy`) | Kiting mini-boss that casts pentagrams | `Prefabs/Cthullu eye miniboss/CuthuluEye.prefab` |
| `Scripts/Enemy/eyeEnemey/EscapePentagram.cs`, `PrisonPentagram.cs` | Eye attacks | `EscapePent.prefab`, `PrisonPent.prefab` |
| `Scripts/Enemy/boss_type/BossEnemy.cs` (+ Animator, Juice, Bullet, BulletPool) | Boss | `Prefabs/Enemies/Boss_Prefab/MiniBoss1.prefab` (via BossDefinition), `Boss1_Steve.prefab` |
| `Scripts/Managers/BossSceneManager.cs`, `BossDefinition.cs` | Spawns the boss from data | BossArena |
| `Scripts/Enemy/JumpFloodPathfinding.cs` | Flow-field pathfinding (computed, not consumed) | `Enemy 2.prefab`, `Enemy 3.prefab` |

### 1.2 Present but not wired

| File | State |
|---|---|
| `Scripts/Dungeon Generator/New dungeon Generator/PathGenerator.cs`, `RoomGenerator.cs`, `DungeonManager.cs`, `RoomConfig.cs` | No scene or prefab references them. This is an alternative generator: a winding path of rectangular rooms painted straight onto a Tilemap. |
| `Scripts/Dungeon Generator/New dungeon Generator/CorridorUtils.cs` | The corridor-drawing body is commented out, so the method does nothing. |
| `Scripts/Dungeon Generator/new.cs` | The whole file is commented out. It held an older `RoomType`/`RoomConnection` definition. |
| `Scripts/Enemy/eyeEnemey/Pentagram.cs` | A combined Escape/Prison component on `Pentagrams.prefab`. The Eye does not reference it; it uses the separate Escape/Prison prefabs. |

---

## 2. Procedural generation

### 2.1 Call flow

```
RoguelikeManager.GenerateNewDungeon()                       RoguelikeManager.cs:199
  └─ DungeonMapGenerator.GenerateNewMap(dungeonSize)        DungeonMapGenerator.cs:272
       dungeonSize = BASE_DUNGEON_SIZE(5) + dungeonSizeIncrement
       ├─ rng = new System.Random()            (unseeded)
       ├─ EnemySpawner.ResetCthulhuEyeTracking()   (clears static set)
       ├─ reset room-ID counters, ResetKeySpawnStatus()
       ├─ params = mapParametersSO.ToMapParameters(); params.nodeCount = dungeonSize
       ├─ GenerateDungeon(params)                           :353
       │    ├─ CreateIsolatedStartRoom                      :396
       │    ├─ GenerateMainArtery                           :1230
       │    ├─ ConnectRooms(start, firstMain)
       │    ├─ mark last artery room END_ROOM (endRoomId = lastMainRoomId)
       │    ├─ ValidateStartToEndDistance                   :421
       │    ├─ InsertDistributiveNodesAndSproutLeaves       :1264
       │    ├─ GenerateFloorTiles (rooms + corridors)       :1333
       │    └─ GenerateWallTiles                            :1437
       ├─ PrintRoomDebugInfo
       ├─ SpawnAllEnemySpawners                             :807
       ├─ PaintTiles                                        :446
       └─ SpawnLevelObjects → SpawnTeleporter (next frame), SpawnRelics
```

Other entry points:
- `GenerateNewMap()` with no arguments (ContextMenu / `generateOnStart`) uses `mapParametersSO.nodeCount` (asset value **7**). In RoguelikeMode, `generateOnStart = false`, so the manager path above is the one that runs.
- `GenerateMapDataOnly(seed, nodeCount)` (`:303`) is used by `_Showcase/DungeonRevealShowcase.cs`. It is seeded and builds data only: no spawning, painting or key logic. It does **not** call `ResetKeySpawnStatus`.

### 2.2 Data model

- `Room { uniqueId, worldPosition (Vector2 centre), type, size (Vector2Int), connections }`
- `RoomConnection { connectedRoomId, ConnectionType ARTERY_PATH | VEIN_PATH }`
- `MapData { rooms (Dictionary<int,Room>), floorTiles, wallTiles (HashSet<Vector2Int>), startRoomId, endRoomId, lastMainRoomId }`. `endRoomId` and `lastMainRoomId` always hold the same value.
- Room-ID ranges come from `RoomIDCategories`: main/start/end 0–99, leaf 100–199, distributive 200–299, corner 300–399. Start and end draw from the main counter.

### 2.3 Parameters in use

Values come from `Mapparametersso.asset`, the asset referenced by `RoguelikeMode.unity`.

| Param | Value | Notes |
|---|---|---|
| nodeCount | 7 in the asset; **overridden at runtime to 5 + increment** | The override counts main-artery rooms only. The start room is extra, and corner rooms are inserted on top. |
| mainRoomSpacing | 6 | Centre-to-centre step |
| chanceForLTurn | 0.3 | Per step, except the last |
| mainArteryPositionJitter | 2 | ±2 on x and y |
| distributiveNodeChancePerSegment | 0.858 | |
| min/maxLeafNodesPerDistributive | 1 / 3 | Inclusive |
| leafBranchLength / leafNodePositionJitter | 6 / 1.5 | |
| Room base sizes | start 6×6, end 8×8 (unused, see §2.9), main 7×7, distributive 5×5, leaf 5×5, corner 4×4 | |
| roomSizeVariationPercentage | 0.331 | Each axis ±33% independently, minimum 2 |
| roomRotationChance | 0.5 | Swaps width and height on non-square rooms |
| minRoomDistance | **0** | Padding used by the overlap test |
| maxRepositionAttempts / repositionSearchRadius | 30 / 5 | |
| startRoomToArteryDistance | 12 | |
| minStartToEndDistance | 40 | |
| corridorWidth | 4 | |

Size growth over a run: `RoguelikeManager.ContinueDungeon` increments `dungeonSizeIncrement` with a 50% chance (`SIZE_INCREASE_CHANCE`) on each dungeon after the first. This adds one more main-artery room.

### 2.4 Layout algorithm

1. **Start room** is placed at `(0,0)` with type `START_ROOM`. It runs no overlap check, because it is the first room.
2. **Main artery** (`:1230`):
   - The first main room goes at `start.x + start.width/2 + 12`, heading `+x`.
   - Each later step (`i = 1..nodeCount-1`) can take an L-turn with probability 0.3, except on the last step. An L-turn steps 6 units plus jitter, places an `ARTERY_CORNER_ROOM`, then rotates the heading ±90° at random.
   - Each step then advances 6 units plus jitter and places a `MAIN_ARTERY_ROOM`.
   - Rooms are chained with `ARTERY_PATH` connections.
   - Turns are random perpendiculars, so two turns can reverse the heading. The artery can then fold back toward earlier rooms or the start room.
3. **End room** is the last entry in `mainPathIds`, retyped to `END_ROOM`. The teleporter goes here (`Teleporter.SpawnInRoom` uses the rounded room centre).
4. **Start–end distance check** (`:421`): if the straight-line distance between the start and end centres is under 40, the end room is pushed out along the same direction to exactly 40, then passed through `FindValidRoomPosition`. No other room moves. The corridor from the previous artery room to the end room stretches to fit.
5. **Distributive and leaf rooms** (`:1264`):
   - For each artery segment `i` in `[0, Count-2)`, which skips the final segment into the end room, the code rolls 0.858.
   - On success it places a `DISTRIBUTIVE_NODE_ROOM` at the segment midpoint, through `FindValidRoomPosition`. It removes the direct `i ↔ i+1` connection and rewires as `i ↔ dist ↔ i+1`.
   - It then sprouts 1–3 `LEAF_NODE_ROOM`s. They sit at angles spaced evenly around the full circle (`j·2π/n + rand[0, π/4]`), at distance 6 ± 1.5 jitter, and connect with `VEIN_PATH`.
   - Leaves have exactly one connection, so they are dead ends.
   - Leaf angles cover all 360°, so a leaf can point back toward the artery or other rooms.
6. **Graph shape:** an acyclic tree. Corridors are drawn geometrically without collision checks (§2.5), so a corridor can cut through unrelated rooms or other corridors. That creates walkable adjacency the graph does not record.

### 2.5 Overlap resolution: `FindValidRoomPosition` (`:723`)

- Test: an AABB of `size + 2·minRoomDistance` (0 here) against every existing room's AABB (`Bounds.Intersects`).
- On overlap it tries 8 compass directions, starting at angle 0 (east) and stepping 45° counter-clockwise. The radius is `5·attempt` for attempts 1–30, so up to 150 units out. It returns the first position that passes.
- If all 240 candidates fail, it returns the original **overlapping** position.
- Main rooms are about 5–9 wide at a 6-unit spacing (±2 jitter). The desired spot for the next artery room therefore often overlaps the previous room, and the room gets displaced by a multiple of 5 in the first free compass direction.
- Distributive rooms are aimed at the exact midpoint between two artery rooms, which by construction usually overlaps one of them. They are typically displaced off the artery line.
- With `minRoomDistance = 0`, rooms may share edges, and their floors merge into one larger floor area.

### 2.6 Room sizing (`GetRoomSizeForType`, `:667`)

- Size is `base ± round(base · 0.331 · U(-1,1))` per axis, with a minimum of 2. Rotation then swaps the axes with 50% chance when width ≠ height.
- The tile footprint is `[RoundToInt(cx − w/2), RoundToInt(cx + w/2))` per axis (`AddRoomTiles`, `:1359`). Centres are arbitrary floats from jitter and repositioning, and `Mathf.RoundToInt` rounds half to even. The painted footprint can differ from `size` by ±1 tile on an axis.

### 2.7 Corridors (`AddCorridorTiles`, `:1375`)

- Each connection gets one corridor, deduplicated by a `"min-max"` ID key.
- It runs between the **rounded room centres**, not between doors or edges.
- The line uses a Bresenham-style stepper (`GetLineTiles`, `:1406`). Each step moves x **or** y, never both, which gives a 4-connected staircase. The final endpoint tile is not added; it lies inside the room anyway.
- Width comes from the dominant axis of the whole segment. A horizontal-dominant segment extends each line tile vertically by offsets `-2..+1` (width 4, asymmetric). A vertical-dominant segment extends horizontally by the same offsets.
- Diagonal segments (common for leaf branches and displaced rooms) keep the 4-tile band on one axis only. Their thickness measured perpendicular to travel is therefore below 4: about 2.8 tiles at 45°.
- Corridors carve floor through anything in their way, including other rooms.

### 2.8 Walls and painting

- **Walls** (`:1437`): every non-floor tile that is 8-adjacent to a floor tile becomes a wall. Walls are always 1 tile thick.
- **Wall autotile** (`:553`): this needs 18 `wallTiles` entries. It picks by the count of **cardinal** wall neighbours (4 = cross, 3 = T, 2 = corner or straight, 1 = cap, 0 = isolated). Floor neighbours only decide which way a straight wall faces. Diagonal neighbours are not considered.
- **Floor painting** (`PaintTiles`, `:446`):
  - Floor is painted under every wall tile, plus a random "spill" of 1–3 tiles outward from each wall's open cardinal sides.
  - The first spill tile is always placed; each further tile continues with 70% chance.
  - Spill is cosmetic and lies outside the walls.
  - Spill uses `UnityEngine.Random`.
- **Foliage:** disabled in RoguelikeMode (`spawnFoliageInRooms = false`, `spawnFoliageInCorridors = false`, `foliageTiles = []`).
- **Physics:** the `Walls` tilemap is on layer 8 (`Walls`) with `TilemapCollider2D`, `CompositeCollider2D` and `Rigidbody2D`. This is the only collision geometry the AI interacts with.
- **Before painting:** `ChunkedGorePainter.ClearAllChunks()` and `TilemapShadowCaster2D.ClearShadows()` run. After painting, `RegenerateShadows()` runs.

### 2.9 Room-type details and quirks (as implemented)

- The end room is created as `MAIN_ARTERY_ROOM` and only retyped afterwards. It therefore gets **main-artery sizing (7×7 base)**, and `baseEndRoomSize` is never used.
- `GetRoomSizeForType` logs on every rotation, and generation prints a debug line per room. Both are a lot of logging per map.
- **Key** (`CreateRoom`, `:711`): spawned in the first leaf room created, during layout generation. It is disabled in RoguelikeMode (`shouldKeyBeSpawned = false`).
- **Relics** (`:1532`):
  - Each leaf room that is not the key room gets one with 25% chance. RoguelikeMode assigns 1 relic prefab (`roomsPerRelicType = 1`).
  - The position is uniform within ±30% of the room size around the centre.
  - Relics are parented to the generator, and neither `GenerateNewMap` nor `RoguelikeManager.ClearCurrentDungeon` destroys them.
- **Teleporter:** all existing `Teleporter` objects are destroyed, and a new one spawns one frame later in the end room.

### 2.10 Spawner placement (generator side)

`SpawnAllEnemySpawners` (`:807`) runs for every room and every spawner prefab:

- **Prefab list:** RoguelikeMode has `spawnerPrefabs = []`, so the fallback `enemySpawnerPrefab = Enemy Spawner.prefab` is the only one used.
- **Placement settings** are read from the prefab. `Enemy Spawner.prefab` has no serialized values for the new placement fields, so the script defaults apply: `placement = Corner`, `roomTypes = AllButStartAndEnd`, `perRoomMin/Max = 1/3`, `wallClearance = 1`, `centerJitter = 0`, `minDistanceFromOtherSpawners = 5`.
- **Room filter:** start and end rooms are excluded, so the teleporter room has no spawners. Main, distributive, leaf and corner rooms are eligible.
- **Count per room:** `UnityEngine.Random.Range(1, 4)`, so 1–3.
- **Corner candidates:** tiles on the room's rectangular edge (from the §2.6 bounds) that are floor and have **exactly 2 cardinal wall neighbours**. Corridor openings, merged or overlapping rooms, and corridors crossing a room all remove such tiles. A room with no candidates logs a warning and gets no spawner.
- **Spacing:** candidates are shuffled, and any candidate within 5 units of *any* spawner already placed in this dungeon is skipped. In a 5×5 room the corners are about 4 apart, so typically only one corner spawner fits.
- **Teleporter clearance:** applied only to the end room, which the room filter already excludes.
- **`ConfigureSpawnerForRoom`** (`:1039`) writes the following to each instance:
  - `spawnRadius` and `maxEnemies`: `EnemySpawner` never reads either (see §4.1).
  - `spawnInterval = U(1, 3)`.
  - `SetDungeonLevel(mapParametersSO.nodeCount)`. This is the **asset value 7**, not the runtime dungeon size or the current level.
- Instances are parented under a `GameObject.Find("EnemySpawners")` container (created if missing) and renamed `EnemySpawner_Room_{id}_{type}`.
- **Spawner respawn system** (`RequestSpawnerRespawn`, `:1091`): disabled in the scene (`enableSpawnerRespawn = false`), and no code calls it.

### 2.11 Between dungeons

`RoguelikeManager.CompleteDungeon → ClearCurrentDungeon` (`:352`) destroys all objects tagged `Enemy`, `EnemySpawner` and `Coin`. It does not touch:
- Corpses (untagged GameObjects created by `EnemyDeath`)
- Relics
- The `EnemySpawners` container object

After the shop, `ContinueDungeon` does the following in order:
1. Increments `currentLevel`.
2. Rolls the size increase.
3. Moves the player to `(0,0)`, which is the start room centre.
4. Calls `GenerateNewDungeon()`.
5. Calls `UpdateSpawnerDifficulty()`, which runs `UpdateDifficultyForLevel(currentLevel)` on every spawner.

---

## 3. Activation systems (who decides when AI runs)

Three independent proximity systems exist. Only one of them gates melee enemies.

| System | Location | Values (serialized) | Applies to |
|---|---|---|---|
| `PlayerSpawnerActivator` | On player (`ActivateEnemySpawner.cs`) | range **7**, scan every 0.5 s | `EnemySpawner`s. Activation is one-way; spawners never deactivate. |
| `ActivateEnemies` (singleton) | On player | radius **10**, check every **0.1 s**, max **30** active | Only `EnemyMovement` (melee) reads it |
| `CthulhuEyeEnemy` internal | On the Eye | radius 20, check 0.5 s | Only that Eye |
| (none) | — | — | `TriangleEnemy` runs as soon as it exists |

`ActivateEnemies` details:
- `Physics2D.OverlapCircleAll` with no layer mask, filtered by tag `Enemy`.
- Results are sorted by distance, and the closest 30 are active.
- Any collider tagged `Enemy` takes a slot, including Ranged enemies and the Eye, even though they ignore the system.
- An enemy beyond 10 units, or outside the closest 30, is removed from the set.
- `OnGUI` draws an "Active Enemies" label while `showDebugGizmos` is true (true on the player prefab).

---

## 4. Enemy AI

### 4.1 EnemySpawner ("piñata"), `Scripts/Enemy/EnemySpawner.cs`

**Serialized values** (`Enemy Spawner.prefab`):
- Layer `Enemies`, tag `EnemySpawner`, `maxHealth` 300, `startingBudget` 400. The prefab's `spawnInterval` of 0.7 is overwritten by the generator.
- `requiresActivation = true`, `zoneOffset = 1`, `wallLayer = Walls`, `coinRewardMultiplier = 0.01`.

**Roster:**

| Slot | Prefab | Cost | Weight | maxAllowed (lifetime, per spawner) | Group size |
|---|---|---|---|---|---|
| fodderLevel1 | `Enemy 1` | 10 | 2 | 12 | 8 |
| fodderLevel2 | — (empty) | | | | |
| fodderLevel3 | — (empty) | | | | |
| rangedEnemy | `RangedEnemy` | 50 | 1 | 2 | 1 |
| cthulhuEye | `CuthuluEye` | 100 | 1 | 1 | 1 |

**Effective budget and interval:**
- The generator calls `SetDungeonLevel(7)`, which adds 350 to the budget and subtracts 1.05 s from the interval (floor 0.5).
- After every generation, `RoguelikeManager` calls `UpdateDifficultyForLevel(currentLevel)` again in the same frame, before the spawner's `Start` (`RoguelikeManager.cs:128/159/403/443`). At level 1 this does nothing. From level 2 on it adds a further `50·level` and subtracts `0.15·level` s (floor 0.5).
- `Start` then sets `currentBudget = startingBudget`.
- Result:
  - **Budget:** 750 at level 1, and `750 + 50·level` from level 2 on.
  - **Interval:** `max(0.5, U(1,3) − 1.05)`, then reduced by `0.15·level` from level 2 on.

**Spawn loop** (`Update`):
- While activated and `currentBudget > 0`, it calls `AttemptSpawn` whenever `Time.time >= nextSpawnTime`. The first spawn happens immediately on activation.
- **`PickEnemyByWeight`** picks among slots that have a prefab, are affordable, are under `maxAllowed`, and (for the Eye only) have no Eye already spawned within 25 units. "Within 25 units" means near any spawner position recorded in the static `globalCthulhuLocations`, which is cleared on each `GenerateNewMap`.
- On a pick, the group size is the smallest of the slot's group size, what the budget affords, and the remaining cap.
- **`GetValidSpawnPosition`:**
  - Builds 4 candidate points at ±1 unit up, down, left and right of the spawner.
  - A point is valid when an `OverlapBox` of `localScale·0.95` (0.95 × 0.95) hits nothing on `Walls`.
  - It returns a random valid point, or the spawner's own position when none is valid. In that case that member of the group is skipped and nothing is charged.
  - Corner spawners have walls on 2 sides, so usually 2 points are valid. A group of 8 fodder is therefore instantiated on 2 to 4 identical points.
- `nextSpawnTime` advances only when a slot was picked.

**Lifetime cap versus budget:**
- With the current data, the most a single spawner can ever spend is 12·10 + 2·50 + 1·100 = **320**, which is below the minimum budget of 750.
- So the `currentBudget <= 0 → Die()` path never triggers. Once the caps are hit, `PickEnemyByWeight` returns null and allocates a list **every frame** for as long as the spawner lives.
- A spawner ends only when the player shoots it (300 HP). On death it drops `round(remainingBudget · 0.01)` coins (about 4–5), flashes, turns grey, disables its collider and stays in the scene until the dungeon is cleared.

**Lifetime output per spawner:** at most 12 melee, 2 ranged and 1 Eye. Spawners in the same 25-unit area share one Eye between them.

**Legacy fields:** `maxEnemies`, `spawnRadius` and `SetDungeonGenerator()` are written or called but have no effect.

### 4.2 Melee fodder: `Enemy.cs` + `EnemyMovement.cs` (prefab `Enemy 1`)

**Serialized values:**
- `Enemy`: HP 50, `knockbackStrength` 25, contact `attackDamage` 25, contact cooldown 1.5 s.
- `EnemyMovement`: `speed` 4, `attackTriggerDistance` 2, `windupDistance` 0.2, `lungeForce` 20, lunge cooldown 1.5 s, `lungeDrag` 5, `separationRadius` 1, `separationForce` 3.
- Rigidbody2D: dynamic, mass 2, linear damping 0, gravity 0. Layer `Enemies`, tag `Enemy`.

**Behaviour per `FixedUpdate`** (`EnemyMovement.cs:57`):
1. **Gate:** `isActivated = ActivateEnemies.Instance.IsEnemyActivated(gameObject)`. If the enemy is not activated, the method returns **without touching velocity**. With damping 0, an enemy that drops out of activation while moving keeps its last velocity until a collision stops it. A freshly spawned enemy has zero velocity and idles.
2. **Push:** skipped for 3 frames after `NotifyBulldozed()` (called by the player controller during bulldoze).
3. **Chase:** `direction = normalize(player − self)`, straight line, no line-of-sight check, no pathfinding and no wall avoidance. When the player is behind a wall, the enemy pushes into the wall collider and slides along it under physics.
4. **Attack trigger:** if the enemy is within 2 units and off cooldown, it runs `SpringAttack`:
   - **Wind-up:** 0.4 s, moving back 0.2 via `MovePosition` with a sprite squash.
   - **Lunge:** `AddForce(dir·20, Impulse)`, about 10 u/s at mass 2. The direction is re-sampled at lunge time.
   - **Deceleration:** 0.6 s of velocity lerped toward zero at `fixedDt·5` per step.
   - Cooldown 1.5 s.
   - Being bulldozed aborts the attack.
5. **Otherwise:**
   - `ApplySeparation()` calls `AddForce(away · 3 · closeness)` per nearby `EnemyMovement`, found with an unmasked `OverlapCircleAll` and a `GetComponent` per hit.
   - Then velocity is set to `direction · 4`.
   - Separation goes through the force accumulator while the chase velocity is assigned outright every step. Its net contribution is therefore at most about F/m·dt = 3/2·0.02 ≈ **0.03 u/s per neighbour**, assuming the default 0.02 s fixed step, against a chase speed of 4.
   - The whole transform is rotated toward the player (`Slerp`, `rotationSpeed` 5).

**Damage:**
- **Dealt:** contact damage comes from `Enemy.OnCollisionStay2D` while touching the `Player` tag, on the 1.5 s cooldown. The lunge does no damage of its own; it only causes contact.
- **Taken** (`Enemy.TakeDamage`):
  - Sprite swap flash (disables the Animator) and a squish scale coroutine.
  - Knockback impulse of 25 directed **away from the player position**, not from the projectile. It goes through `EnemyMovement.ApplyKnockback`, which calls `StopAllCoroutines`, cancelling any lunge.
  - `playerTransform` and `enemyMovement` are used with no null check here.
- **Death:**
  - `EnemyKillTracker.RegisterEnemyKill()`, which calls `GameSession.Save()` on every kill.
  - Blood splatter and 1–3 coins.
  - `OnDeath`, then `EnemyDeath.HandleDeath()`: a corpse sprite with a 30 s lifetime, all scripts disabled, and the enemy destroyed after 0.1 s.

**Other melee prefabs:** `Enemy`, `Enemy 2` and `Enemy 3` exist with the same scripts. `Enemy 2` and `Enemy 3` also carry `JumpFloodPathfinding`. None of the three is referenced by `Enemy Spawner.prefab`.

### 4.3 Ranged: `TriangleEnemy.cs` (prefab `RangedEnemy`)

**Serialized values:**
- HP 150. Bullets: damage 10, speed 4, lifetime 3 s (default), `fireRate` 1 s, `bulletSpread` 10°.
- `wallLayers = Walls`, `playerLayer = Default`. The player root is on `Default`.
- `minRange` 2 (default 4), `maxRange` 4 (default 9), `moveSpeed` 3, `roamTurnSpeed` 90°/s, `wanderInterval` 2 ± 0.5 s, `attackTurnSpeed` 320°/s, `aimTolerance` 4°.
- Rigidbody2D: continuous collision, rotation not frozen.

**Activation:** not gated. The FSM runs from spawn whenever a `Player`-tagged object exists. It does count toward the `ActivateEnemies` cap (§3).

**FSM** (state logic in `Update`, movement in `FixedUpdate`):

| State | Movement | Rotation | Exit |
|---|---|---|---|
| Roaming | `velocity = heading · 3` | Head (+Y) turns toward the heading at 90°/s | Line of sight → Turning |
| Turning | velocity 0 | Butt (−Y) spins toward the player at 320°/s | LOS lost → Roaming; aligned within 4° → Shooting |
| Shooting | velocity 0 | Keeps tracking at 320°/s | LOS lost → Roaming |

- **Roaming heading:**
  - Every 2 ± 0.5 s the heading takes a random turn of ±90°.
  - Each frame, the distance band adjusts it. Closer than 2: steer the heading away from the player at 200°/s. Farther than 4: steer it toward the player at 200°/s.
  - The band only acts in Roaming.
  - Walls are not considered; the enemy runs into walls and physics resolves the contact.
- **Line of sight** (`HasSightLine`, runs every frame):
  - A raycast against `Walls` up to the player's distance; any hit means no LOS.
  - Then a raycast against `Default` up to distance + 0.5, which must hit something.
  - **No maximum range**, so LOS is purely geometric.
- **Re-entering Roaming** sets the heading to `facing + 90°`, the head direction. While shooting, the butt faced the player, so the enemy initially roams **away** from the player.
- **Firing:**
  - 2 bullets from `NosePoint` at ±5° around −Y, drawn from the shared `BulletPool`. The pool is found by tag `BulletPool`, then by type.
  - The first shot comes 0.08 s after entering Shooting.
  - `GetComponent<TriangleEnemyJuice>().OnFired()` runs on each shot with no null check.
- **Damage taken:** subtracts HP only. There is no knockback and no flash in this script.
- **Death:** kill tracker, optional particle, then `EnemyDeath`. No coin drop.

### 4.4 Mini-boss: `CthulhuEyeEnemy` (prefab `CuthuluEye`)

**Serialized values:**
- HP 300, `activationRadius` 20, `activationDelay` 2 s, `moveSpeed` 2, `preferredDistance` 5 ± 1, `attackCooldown` 5 s, `escapePentagramChance` 0.5.
- Root tag `Enemy`, layer `Enemies`.

**Behaviour:**
- **Activation:** a coroutine checks every 0.5 s. Inside 20 units the Eye becomes active and may attack after 2 s. Outside 20 units it deactivates and stops.
- **Movement** (`FixedUpdate`): velocity is set directly. Closer than 4: retreat straight away from the player. Farther than 6: approach straight. Otherwise: stand still. There is no pathfinding, LOS check or wall awareness.
- **Attack:** every 5 s it instantiates a pentagram **at the player's current position**, then runs a white-to-red colour fade over `cooldown − 1` s.
  - **Escape pentagram** (`EscapePent.prefab`):
    - Scales in over 1.5 s.
    - At the end, a player within radius 2 takes 20 damage through `PlayerHealth.TakeDamage`.
    - The pentagram is destroyed 0.3 s later.
  - **Prison pentagram** (`PrisonPent.prefab`):
    - Scales in over 0.8 s.
    - If the player is still inside radius 2, they are trapped for 3 s: each frame where they pass 0.9·r, `player.transform.position` is set to 0.85·r and outward velocity is removed.
    - If the player left during the warning, the pentagram is destroyed with no effect.
- **Damage taken:** squash and colour flash. `TakeDamage` has no dead guard.
- **Death:** `Destroy(gameObject)` only. There is no kill-tracker registration, no coins and no `EnemyDeath`. `IsDead()` returns `currentHealth <= 0`.

### 4.5 Boss: `BossEnemy` (prefab `MiniBoss1`, spawned by `BossSceneManager` from `BossDefinition`)

**Serialized values (`MiniBoss1`):**
- HP 4000, shield 1000, contact damage 10 on a 1.5 s cooldown.
- `moveSpeed` 6, `maxSpeed` 20, +1 speed per bounce.
- `wallStickDuration` 5 s.
- Bullets: `angleBetweenBullets` 20°, `timeBetweenWaves` 0.3 s, `timeBetweenAttacks` 1 s, speed 8, damage 5.
- `bulletPool` is referenced inside the prefab.

**Behaviour ("pinball"):**
- **Movement:** constant `velocity = moveDirection · currentSpeed`. The initial direction points at the player.
- **While shielded, on hitting a wall** (`Walls` mask):
  - Reflect off the averaged contact normal.
  - If the reflection is shallow (`dot(reflect, normal) < 0.35`), lerp 40% toward the normal.
  - Teleport-nudge 0.2 along the normal.
  - Speed +1, up to 20.
- **Shield broken:**
  - The next wall hit makes the boss stick: it is placed 0.5 off the contact point and goes kinematic one physics step later.
  - For 5 s it alternates radial waves: offset 0°, then offset 10° after 0.3 s, then a 1 s pause, repeating.
  - Each wave fires every 20° (18 directions) except directions with a wall within 1 unit.
  - Afterwards it un-sticks, resets speed to 6, re-aims at the player and **fully restores the shield**.
- **Damage windows:** HP only takes damage while the shield is down. That is the travel time from shield break to the next wall, plus the 5 s stick.
- **Watchdog:** when not stuck, if the boss moved less than 1 unit over 1 s, it relaunches in a random direction at base speed.
- **Targeting:** the boss only re-reads the player position at spawn and when leaving a wall. Otherwise its path is pure reflection, with no pathfinding.
- **Unused members:**
  - `minTeleporterUses`, `maxTeleporterUses`, `maxSpawnTimeSeconds`
  - `bulletsPerWave` (the wave count derives from the angle step instead)
  - `GetBestFiringDirection` / `FindLargestGapCenter` (nothing calls them)
- **Death:** stops coroutines, goes kinematic, drops 10–20 coins, fires `OnDeath`, and is destroyed after 2 s. `BossSceneManager.OnBossDefeated` → `GameManager.CompleteRun()`.

---

## 5. Pathfinding: `JumpFloodPathfinding.cs`

### 5.1 Wiring

- The component is on `Enemy 2.prefab` and `Enemy 3.prefab`, so every instance carries one. The spawner does not spawn these prefabs.
- `Awake` is a singleton: the first instance becomes `Instance`, and later ones call `Destroy(this)`. `Instance` is never cleared in `OnDestroy`. If the owning enemy dies, `Instance` refers to a destroyed object, which Unity treats as null.
- **No script calls `GetNextStep` or `GetFlowDirection`.** `EnemyMovement` does not reference the class. The flow field is computed and discarded.

### 5.2 Serialized settings (`Enemy 2`)

- Grid 30×30, `cellSize` 1, `gridOrigin` (0,0), so it covers world (0,0)–(29,29), roughly the start room and the first stretch of corridor.
- `obstacleLayer = Walls`, `updateInterval` 0.5 s, `jumpFloodIterations` 4.

### 5.3 Algorithm as written

1. **`BuildObstacleGrid`:** every update, one `Physics2D.OverlapCircle(r = 0.3)` per cell (900 calls).
2. **`CalculateFlowField(player)`:**
   - Sets every distance to `MaxValue` and the goal cell to 0.
   - Runs `iterations + 1` passes of `JumpFloodPass`. Each pass clones the full distance array.
3. **`JumpFloodPass`:**
   - For every non-obstacle cell, loops over 9 neighbour offsets but computes `distance(cell, goal)`, the cell's own **straight-line distance to the goal**, independent of the neighbour.
   - So after the first pass, every free cell holds its Euclidean distance to the goal. Obstacle cells stay at `MaxValue`, and walls never lengthen distances.
   - It is not a jump-flood seed propagation, and it is not a BFS/Dijkstra over walkable cells.
4. **`GenerateFlowField`:** each free cell points toward its free 8-neighbour with the lowest value. This is greedy descent on Euclidean distance. Inside a concave wall pocket facing the goal no neighbour is lower, so the vector is zero. Diagonal moves may cut wall corners because only the destination cell is checked.
5. **`GetNextStep`:** outside the grid, or on a zero vector, it returns the goal position itself. Otherwise it returns `pos + flow · cellSize`.
6. **Coordinates:** `WorldToGrid` uses `RoundToInt`, so cell *(x, y)* is centred on world *(x, y)*. Floor tiles are centred on *(x + 0.5, y + 0.5)*. The grid is therefore offset half a tile from the tilemap.

### 5.4 Net effect

There is no pathfinding in play. Melee, Eye and boss movement is direct-line. Ranged movement is a random wander bounded by distance. All wall interaction is left to Rigidbody2D collisions against the wall `CompositeCollider2D`.

---

## 6. Cross-cutting observations (factual)

1. **Tag-based globals:**
   - The player is found by `FindGameObjectWithTag("Player")`, once per enemy in `Start`/`Awake`.
   - Spawner and enemy cleanup goes by tag.
   - The bullet pool is found by tag `BulletPool`.
2. **Unmasked physics queries:** the `OverlapCircleAll` calls in `ActivateEnemies`, `PlayerSpawnerActivator` and melee separation use no layer mask, so every collider in range is returned and then filtered by tag or `GetComponent`.
3. **Logging volume:** `Debug.Log` runs on each enemy init, each activation and deactivation in `ActivateEnemies` (every 0.1 s scan), each damage event, each spawner placement, each room and each room rotation.
4. **Randomness sources:**
   - Layout uses `System.Random`, unseeded in `GenerateNewMap` and seeded in `GenerateMapDataOnly`.
   - Spawner counts and candidate shuffles, spill tiles, foliage, relics, key position and every enemy behaviour roll use `UnityEngine.Random`.
   - The same seed therefore reproduces the room graph and tiles, but not spawner placement.
5. **Inconsistent death and kill bookkeeping across enemy types:**

   | Type | Kill tracker | Coins | `EnemyDeath` |
   |---|---|---|---|
   | Melee | ✓ | 1–3 | ✓ |
   | Ranged | ✓ | — | ✓ |
   | Eye | — | — | — (Destroy) |
   | Spawner | — | about 4–5 | — (stays grey) |
   | Boss | — | 10–20 | — |

6. **Inconsistent activation:** one shared activation list for melee, self-activation for the Eye, none for Ranged, and one-way activation for spawners (§3).
7. **Difficulty inputs:**
   - Spawner budget and interval scale with the asset's `nodeCount` (constant 7) plus `currentLevel` (via `RoguelikeManager`).
   - Dungeon size scales with a 50% coin-flip per level.
   - Enemy stats (HP, speed, damage) do not scale.
