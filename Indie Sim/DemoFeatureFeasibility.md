# Demo Feature Feasibility

**Date:** 2026-09-30 · **Author:** Claude Code, for @Sarbo
**Assesses:** four planned features: (1) dungeon art, foliage, fireflies, props, puddles and biomes; (2) gun upgrades and laser/plasma beams; (3) enemy AI optimisation and horde behaviour; (4) balancing.
**Method:** read the scripts named below and the relevant prefab values. Nothing was changed or run in Play mode. The lag causes come from reading the code, not from profiling. Effort figures are estimates.
**Builds on:** `1BK-FeasibilityAgainstArchitecture.md` (2026-09-28), which is not repeated here.

---

## The real constraint is the calendar

Per `DemoBeforeIGDC.md`:
- The build must be submitted for Steam review around **Oct 8–10**.
- All engine work stops on **Oct 16**.
- The store page was due for review **around now** (Sep 30 to Oct 3).

That leaves about **6–7 working days** before content lock. Most of the features below are sound and fit the code, but they're post-demo work. Each item is split into a demo-safe part and a post-demo part.

## Summary

| Feature | Demo-safe part | Effort | Post-demo part | Effort |
|---|---|---|---|---|
| 1. Environment art and biomes | Biome switch in the generator, fireflies, cosmetic puddles | Code: 1–2 days. **Art is the real cost.** | Destructible props, several full biomes | 3–5 days of code, plus art |
| 2. Guns | 2–3 new upgrades for the existing guns | About half a day each | Laser/plasma beam weapon | 3–4 days, plus balancing |
| 3. Enemy AI and lag | **Performance fixes (do these first)** and cheap tuning that makes groups feel like a horde | 1–2 days | A real flow-field horde system | About a week |
| 4. Balancing | A local log of each run, plus a spreadsheet model | 1 day | Ongoing, tied to playtesting | Never really finishes |

---

## 1. Dungeon art: floor, foliage, fireflies, props, puddles, biomes

**What exists now** (`DungeonMapGenerator.cs:187-198`):
- There are three tilemaps: floor, wall and foliage. Each is filled from a `TileBase[]` array.
- Foliage is placed at random on floor tiles (`ShouldSpawnFoliage`, `:624`).
- Walls use a fixed layout of **18 tile slots**: index 14 is the cross junction, 6–9 are T-junctions, 10–13 are corners, and so on (`GetWallTileForPosition`, `:553-621`).

**Biomes:**
- The code change is small. Move the floor, wall and foliage arrays (plus light colour and ambient effects) into a `BiomeSO`, and have the generator choose one based on dungeon level. About 1 day.
- **The art is the bottleneck.** Each biome needs all 18 wall pieces drawn to that exact slot layout, plus floor variants, and they have to suit the 1-bit look.
- Realistically, one extra biome fits the demo only if the art is already finished.

**Fireflies:**
- Use a particle system per room, or one that follows the camera. A few hours.
- Don't give every firefly its own `Light2D`. The 2D lights already work with shadow casters, which gets expensive fast. Use glowing sprites plus a few shared lights.

**Puddles and ground details:**
- Purely cosmetic puddles are another decal tilemap layer: a few hours.
- Puddles that do something (slow you down, conduct electricity) need trigger zones: 1 day or more, and they add balance work.

**Interactive props such as rock piles:**
- These are prefabs that take damage (`IDamageable`), placed on floor tiles away from spawners, keys and teleporters. About 2–3 days.
- **Catch 1:** enemies chase in a straight line with no pathfinding (see section 3), so solid props will snag them. For now, props should be non-blocking or very small.
- **Catch 2:** if props drop coins, remember that coins are also health. Every prop then changes the balance.
- Post-demo.

## 2. Guns

**What exists now:**
- All player guns are **hitscan cones**, not projectiles. The tracer is cosmetic.
- There are three types: `Standard`, `Shotgun`, `Piercer` (`WeaponData.cs:54`). The Piercer is orphaned.
- Weapon upgrades already run through the cig/Pack system. There are 12 `CigData` types, and stats are gathered in `PackStats` (crit, damage, fire rate, bounce, per slot).

**Upgrades to existing guns: easy, and fit the demo.**
- Each new upgrade needs a `CigData` subclass, a `PackStats` field, and one line in `PlayerConeShooter` to read it. About half a day each, following a proven pattern.
- Good candidates: range, magazine size, reload speed, cone width, and pierce count (this would bring back the orphaned Piercer logic).

**Laser/plasma with configurable beams: doable, but not for this demo.**
- It needs a new `Beam` weapon type:
  - a raycast or circle-cast
  - a `LineRenderer`
  - damage applied in ticks
  - `WeaponData` fields for width, length, tick rate, pierce, wall bounces and charge-up time
- Estimate: 2–3 days for one good beam. Plasma balls that actually fly: 1–2 more days, reusing the enemy `BulletPool`.

**Hidden costs of a new weapon:**
- **Ammo:** `WeaponAmmoManager` only understands magazines. Reuse the magazine as a "charge" meter, or write a heat system.
- **Upgrades:** decide how crit and bounce cigs apply to a beam.
- **Save data:** it needs a save ID in `ContentCatalog`.
- **Getting the weapon:** the player owns exactly 2 weapons (primary and secondary), so how a third is obtained is a design question.
- **Performance:** a beam ticking 20 times a second makes the per-hit costs in section 3 twenty times worse. The performance fixes come first.

## 3. Enemy AI: lag and horde behaviour

### Likely causes of the lag (not profiled, ranked by suspicion)

1. **`Debug.Log` on hot paths.** Every enemy logs on spawn, on every hit (`Enemy.cs:195`), on death and on coin drop. `TriangleEnemy` (`:176`, `:202`), `EnemyKillTracker` (`:47`) and `ActivateEnemies` do the same. Logging still costs in a build, because it writes the log file with a stack trace. With the AK plus bounce upgrades, that's dozens of log lines a second. Top suspect.
2. **A debug overlay that ships.** `showDebugGizmos: 1` is set on `Temp -Player.prefab`. `ActivateEnemies.OnGUI` draws a label and allocates a new `GUIStyle` every GUI event, including in builds.
3. **Expensive work on every hit** in `PlayerConeShooter.ApplyHitDamage` (`:344`):
   - `FindObjectOfType<CustomCrosshair>()` searches the whole scene.
   - The hit effect is created with `Instantiate`.
   - The damage number popup is also created with `Instantiate` (`Enemy.cs:141`).
4. **Separation between enemies** (`EnemyMovement.ApplySeparation`, `:162`). Each enemy runs `OverlapCircleAll` plus `GetComponent` every physics step. The cost grows with the square of the enemy count, and it allocates memory each time.
5. **No object pooling.** Enemies, coins, blood and corpses are all created and destroyed individually.

**Fix:** strip or gate the logs, turn the overlay off, cache the crosshair, pool damage numbers and hit effects, and switch separation to the allocation-free overlap call. About 1–1.5 days, low risk, and it pays off for everything else here.

**Do this first:** a 30-minute Unity Profiler session on a development build will confirm which of these actually matters.

### Horde behaviour

**How it works now:**
- Every enemy runs straight at the player and lunges when close (`EnemyMovement.FixedUpdate`, `SpringAttack`).
- There's no pathfinding. `JumpFloodPathfinding.cs` isn't used in any scene or prefab. It's also broken: it measures straight-line distance and ignores walls, so it would never route enemies around them.
- `ActivateEnemies` only wakes the **30 nearest enemies within 10 units** (the prefab's values). Everything else stands still.

**Cheap horde feel for the demo (about 1 day of tuning, mostly in the Inspector):**
- Bigger `spawnGroupSize` for fodder enemies.
- An "attack token" limit so only a few enemies lunge at once while the rest circle around.
- Slight speed variation between enemies.
- A higher active cap, but only once the performance fixes are in.

**The real horde system (post-demo, about a week):**
- One flow field (a grid-based breadth-first search that recalculates when the player moves to a new cell) steers every enemy around walls.
- A spatial-hash separation check replaces the physics queries.
- One manager updates all enemies in a single loop, instead of each enemy running its own physics-step code.
- This scales to hundreds of enemies. It touches every enemy type and the lunge feel, so it isn't something to start 8 days before a build lock.

## 4. Balancing: what AI can and can't do

**What AI can help with:**
- **Build the model.** A sheet with time-to-kill per gun per enemy, DPS with each upgrade, and money in versus money out per level. `GameData.xlsx` could hold this. Because **coins are both health and currency**, the economy *is* the difficulty curve, and a spreadsheet makes that visible.
- **Add a local run log.** One JSON file per run: time per room, hits taken, coins earned and spent, upgrades offered versus bought, and cause of death. About 1 day. Claude can then read batches of these logs and flag outliers, such as an upgrade that's always bought or a room where people always die.
- **Suggest curves and write the scaling code** once targets are picked.

**What AI can't do:** tell you what feels good. That only comes from watching people play.

**A method that works without balancing experience:**
1. **Pick 3–4 anchor numbers** and derive everything else from them. For example: a basic enemy dies to 2 pistol shots on level 1, the player survives about 5 hits, and a room takes 30–45 seconds.
2. **Change one variable per playtest**, and write down what changed.
3. **Tune toward outcomes, not stats.** For example: median run length, or the percentage of players who reach the boss.
4. **Every feature added multiplies the number of things to balance.** That's the strongest reason to keep the demo scope small.

## Recommended order before the Oct 7–10 build lock

1. Profile, then apply the performance fixes (1–1.5 days).
2. Horde tuning: group sizes, attack tokens, active cap (about 1 day).
3. 2–3 new upgrades for the existing guns (1–1.5 days).
4. Fireflies and cosmetic puddles, plus the `BiomeSO` switch *only if* a second tileset is already drawn (1–2 days).
5. The run log, so the IGDC playtests produce balance data instead of just impressions (1 day).

**Post-demo:** beam and plasma weapons, destructible props, the flow-field horde system, and the extra biomes.

These estimates add up to roughly 5–7 days. That uses up the whole window, and it has to share that window with the store page and build review.

## Manual Editor checklist (Claude only edits C#)

- [ ] Run the Unity Profiler on a development build in a busy room, and note the top CPU costs before any fix.
- [ ] Set `showDebugGizmos` to off on `Temp -Player.prefab`. It's on in two components there; one is `ActivateEnemies`, and the other wasn't checked.
- [ ] Decide whether a second biome's tileset (all 18 wall slots plus floor variants) can be drawn before content lock.
- [ ] After the performance fixes, tune `spawnGroupSize`, `maxActiveEnemies` and `activationRadius` in the Inspector.
- [ ] Pick the 3–4 balance anchor numbers before tuning anything else.
