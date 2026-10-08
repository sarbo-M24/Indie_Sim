# 1BK Feasibility Against the Current Architecture

**Date:** 2026-09-28 · **Author:** Claude Code, for @Sarbo
**Assesses:** `1BK — Inspiration Research & Technical Feasibility.md` (every idea in its matrix, enemy table and demo-polish tiers)
**Method:** read the scripts named below in full or in the relevant region. Nothing was run in Play mode. Where a claim is inferred rather than read, it is tagged **(inferred)**.
**Effort scale (same as the 1BK doc):** S = under a day, M = 2-4 days, L = a week or more, on top of the current code.
**Calendar:** content lock Oct 7, engine work stops Oct 15. From today that is 9 and 17 days.

---

## 1. Where the 1BK doc's assumptions differ from the code

The doc's baseline says "GameSession, GameEvents bus, ScriptableObject defs, EnemyScaling hooks". Reality:

| # | Doc assumes | Code reality | Ref |
|---|---|---|---|
| 1 | A `GameEvents` bus | **None.** Only ad-hoc statics: `PlayerConeShooter.OnGunHit`, `PlayerStompController.OnAoeHit`, `WeaponInventory.OnWeaponChanged`, per-instance `Enemy.OnDeath`. Phase 7 (event bus + unified death) is still open in the Bible. | `PlayerConeShooter.cs:900`, `PlayerStompController.cs:214`, `WeaponInventory.cs:20`, `Enemy.cs:52` |
| 2 | One enemy death path | Two duplicated `Die()` methods. Only `Enemy` and `TriangleEnemy` call `RegisterEnemyKill`. The Eye and the Boss do not. | `Enemy.cs:249`, `TriangleEnemy.cs:200` |
| 3 | Bullets carry gun modifiers | Player guns are **hitscan cones**; the visible tracer is cosmetic. `Bullet`/`BulletPool` are real and owner-agnostic (layer overrides), but only enemies and Stomp Circle use them. | `PlayerConeShooter.cs:224`, `Bullet.cs:33`, `PlayerStompController.cs:163` |
| 4 | An `EnemyScaling` hook | One method, `UpdateDifficultyForLevel`, on a spawner with **5 hard-wired roster fields** and lifetime (never-decreasing) caps. | `EnemySpawner.cs:5-15,160,390` |
| 5 | Damage can carry a source | `IDamageable.TakeDamage(int)` has no source or direction. `PlayerHealth.TakeDamage(int, Vector3)` has a position only. | `IDamageable.cs:5`, `PlayerHealth.cs:153` |
| 6 | Any enemy can sit on `Enemy` | `Enemy.TakeDamage` dereferences `playerTransform` and `enemyMovement` with no null check. A static enemy (turret, totem) throws on its first hit. | `Enemy.cs:189-193` |
| 7 | Enemy stats can be scaled at spawn | `Enemy` initialises health in `Start`; `TriangleEnemy` in `Awake`. Eye and Boss differ again. | `Enemy.cs:63`, `TriangleEnemy.cs:128` |
| 8 | `BossDefinition` carries phase lists | It holds prefab, arena variant, music, bounds. The fight is a hardcoded coroutine. | `BossDefinition.cs:9-19`, `BossEnemy.cs:319` |
| 9 | Hand-authored rooms + a generator | The generator is a free-form room graph with procedurally sized rooms. **No room prefabs, doors, or room-cleared state exist.** | `DungeonMapGenerator.cs:348-385` |

Smaller mismatches: the 1BK summary says 3 demo-safe / 13 post-demo / 6 parked; its own table has 3 / 14 / 5. "SO upgrade-persistence fix" appears already addressed (`CigData.cs:14-21` documents the invariant; run state lives on `CigInstance`).

---

## 2. Prerequisite spine (build before the ideas)

These four items each gate several ideas. Doing them once is cheaper than working around them per idea.

1. **Unified enemy death + event.** One `EnemyDied(enemy, position, killer)` signal fired from every enemy class, including Eye and Boss (fixes the Bible's "Eye not counted as kill" bug). Gates: kill hit-stop, style meter, kill refills, splitter/exploder, telemetry, elites, evolution. **~1 day.**
2. **`TakeDamage` source overload.** Add `TakeDamage(int, Vector2 sourcePos)` (keep the old one as a forwarder). Call sites: cone shooter (x4 paths), bounce hops, stomp/dash AoE, `Bullet`, pentagram, spawner. Gates: shielded enemies, cause-of-death telemetry, directional knockback. **~0.5 day.**
3. **`Enemy` null guards + common `ApplyScale(float)`.** Guard `playerTransform`/`enemyMovement`; give all enemy classes one entry point for HP/speed scaling that runs before or after init consistently. Gates: turret/totem, elites, difficulty coefficient. **~0.5 day.**
4. **Spawner roster + cap fix.** Roster as a list; caps count live enemies, not lifetime; remove the fixed level-7 boost. Gates: coefficient, any new enemy type, room modifiers. Overlaps Bible Appendix E items. **~1 day.**

---

## 3. The 22 matrix ideas

| Idea | Doc | Mine | Fit | What decides it |
|---|---|---|---|---|
| Game-feel pass | S | **S** | Good | `PauseController.BeginHitStop/EndHitStop` is ref-counted and pause-safe but only `DamageIndicator` uses it (`DamageIndicator.cs:141`). Kill hit-stop is one call once spine #1 exists. Shake exists but is buggy (see 5). Casings do not exist. |
| Run telemetry | S | **S** capture, **M** delivery | Good | `RunStats` + `OnGunHit` give kills, coins, dungeons, per-weapon damage. Missing: cause of death (needs spine #2), upgrade offered-vs-picked. Only the legacy `com.unity.modules.unityanalytics` is in the manifest, and there is no HTTP code, so remote delivery needs a service and a consent decision. Local JSON is S. |
| Palette unlocks | S | **S** | Good | `Renderer2D.asset` already has a `FullScreenPassRendererFeature` (the CRT). Unlock list = a new isolated global section (`GlobalSections.cs` pattern). **Conflicts:** Psychedelic Mode cycles blood colours; several assets are not two-tone (green corpses `EnemyDeath.cs:123`, cyan teleporter). |
| Difficulty coefficient | M | **M** | Good | Replaces `UpdateDifficultyForLevel` (`EnemySpawner.cs:390`) and the level-7 boost. Inputs exist: `CurrentDungeonLevel`, `RunElapsedSeconds`. Needs spine #3 and #4. |
| Credit spawn director | M | **L** | Poor | The pinata's identity is "kill it, get its remaining budget as coins" (`EnemySpawner.cs:265`). A global credit pool removes that. Recommendation: drive per-spawner budget/interval/stats from the coefficient instead. |
| Obstacle chunks | S | **S-M** | Fair | No templates exist to mark zones in. **(inferred)** removing tiles from `floorTiles` before `GenerateWallTiles` (`DungeonMapGenerator.cs:383`) should get autotiled walls and shadow casters free; verify the wall algorithm. Spawner placement uses "corner-type edge tiles", so pillars would create new spawner spots. |
| Isaac grid generator | M | **L** | Poor | Replaces the generator, not extends it. Touches spawner placement, teleporter (`lastMainRoomId`), relics, key, `TilemapShadowCaster2D`, gore chunks, the showcase scene's seeded `GenerateMapDataOnly`, plus 15-25 hand-built rooms. Obstacle chunks give most of the variety for a fraction. |
| Elite modifiers | S | **S** stats, **M** outline | Fair | HP/speed need spine #3. There is **no shared sprite material** in the project; the outline/invert shader is the first custom sprite shader. Hit feedback today is a per-prefab `damageSprite` swap plus animator disable (`Enemy.cs:365-398`). |
| Projectile modifiers | M | **M-L** | Fair | See finding 3. Homing, explode-on-kill, chain are hit-resolution stages (bounce already is: `ChainBounces`, `PlayerConeShooter.cs:365`). Wall-bounce and split-in-flight need real projectiles; the pooled `Bullet` has no modifier hooks. |
| Slot-bound upgrades, pick 1 of 3 | M | **Already built** | Done | `TargetSlot` (Primary/Secondary/Stomp/Dash), Mild/Regular exclusivity, 3-offer shop. Nothing to build. |
| Room modifiers | S | **S** dark / no-dash, **M** room-scoped | Fair | Light2D and the shared input gate (`RoguelikeManager.SetGameplayInputEnabled`) exist. `Room` is pure data (`DungeonMapGenerator.cs:71`); no runtime room object, no entered/cleared, no doors. Challenge rooms need a room-trigger system (M). |
| Splitter, turret, exploder | S each | **S each** | Good | Splitter: `Enemy.OnDeath` then instantiate scaled children; decide whether children count as kills (inflates *Legendary Executioner*). Exploder: burst through `BulletPool.SpawnBullet` with layer overrides (Stomp Circle pattern). Turret: needs finding 6 fixed. |
| Charger, sniper | M each | **S-M** | Good | `EnemyMovement` already has windup + lunge (`EnemyMovement.cs:92`). Add a `LineRenderer` telegraph. Sniper = `TriangleEnemy` state machine + laser. **Wall-breaking is L** (tilemap + shadow regen + flow-field grid). Skip it. |
| Railgun | S | **S-M** | Good | A Piercer type exists (orphaned pistol, `maxPierceCount`). Needs a charge state (`Fire.canceled` is already tracked as `isFiring`). |
| Grenade launcher | S | **M** | Fair | Needs a new fused projectile. `DamageAndPushEnemies` (`PlayerStompController.cs:225`) is public and reusable for the blast. |
| Flamethrower | S | **S-M** | Fair | A cone at a high fire rate is a fake DoT. But every hit runs `Instantiate(hitEffect)` + `FindObjectOfType<CustomCrosshair>()` + a damage number (`PlayerConeShooter.cs:344-357`), so it needs caching/throttling first. |
| Weapon evolution | M | **M** | Fair | `RunStats.RelicsHeld` is persisted, and `OwnedWeapons` is swappable. But relics have no effects and only Relic 1 is assigned in the scene (Bible App. E). "Max a gun" has no meaning yet; define it as "Tier-4 cig held" or similar. |
| Style meter | M | **M** | Good | Needs spine #1. Design caveat: switching weapons already refills every magazine, so "freshness rewards switching" is exploitable from day one. Reward should be coins, which are health, so it directly moves balance. |
| Melee parry | M | **M** | Good | `Bullet.Deflect(layers, velocity)` already exists (`Bullet.cs:63`). Cost is a new input action, a Controls-panel entry and a gamepad binding (X/Y/A/LT/RT are taken). |
| Meta unlocks | M | **S-M** | Good | The doc's blocker (Phase 4 SaveSystem) is done. New global section, isolated by the save-section design (`SaveSection.cs`). Achievement rewards are still unwired, and only 2 weapons (+1 orphan) exist to unlock. |
| Synergies | M | **S-M** | Good (build) / High (balance) | Pull-based `PackStats.Contribute` makes pair bonuses easy to code. Park for balance reasons, as the doc says. |
| Stat shop | S | **S** | Good (build) | The cig shop already is one. Note coins = health, so buying stats is buying risk. |
| Summoner | M | **M** | Fair | Agree with parking. Children are not spawner-budgeted. |
| L4D pacing director | M | **L** | Poor | Spawners are independent, proximity-activated, and each holds its own budget. No central place to hang it. |
| Gungeon flow graphs | L | **L** | Poor | Agree with parking; also incompatible with the current generator. |

---

## 4. Enemy archetypes

| Archetype | Verdict | Notes |
|---|---|---|
| Swarmer | **S** | `Enemy` + `EnemyMovement` prefab variant. **Perf risk (inferred):** `ApplySeparation` runs `OverlapCircleAll` + `GetComponent` per neighbour per FixedUpdate (`EnemyMovement.cs:162`), O(n²) with GC. Untested at high counts because spawners cap at 15 lifetime. |
| Rifleman | **S-M** | `TriangleEnemy` is squid-specific (rear-firing, roaming). New small script using `BulletPool`. |
| Charger | **S-M** | See matrix. No wall-breaking. |
| Exploder | **S** | Own `OnDeath` fires before `EnemyDeath.HandleDeath` (`Enemy.cs:264`), so a burst hook works today. |
| Splitter | **S** | Set child HP right after `Instantiate`; `Start` then resets current HP to the new max. |
| Sniper | **S-M** | Wall raycast + delayed hit. |
| Turret / totem | **S** after guard fix | New `IDamageable` script, radial `BulletPool` patterns. |
| Ambusher | **S-M** | Cone targeting hits any `IDamageable` on the enemy layers, so "dormant" must read as non-targetable (layer swap or a flag checked in `DetectDamageableTargetsInCone`). |
| Summoner | **M** | Parked, fine. |
| Shielded | **M** | Needs spine #2. Stomp, dash AoE, bounce and bullets all call `TakeDamage(int)` today. |
| Elite modifier | **S / M** | See matrix. |
| Boss phases | **L** | No phase system exists. A second boss as a *tuning variant* of the current one is S-M. |

Every new enemy's hit costs a flat 30 coins regardless of its `attackDamage`/`bulletDamage` (Bible App. E). Telegraph and HP are the only design levers until that is fixed.

---

## 5. Demo polish (Tier 1-3)

| Item | Status | Notes |
|---|---|---|
| Hit-stop | Partly exists | Service exists; kill hooks missing. 1 h after spine #1. Held fire survives (flag-based), dash/stomp are event-driven. |
| Hit flash (invert) | New, S-M | Today: per-prefab sprite swap. Needs the shared sprite material (see Elite). |
| Screen shake + kick | Exists, **bug** | `CameraShake.ShakeCamera` starts a new coroutine per call without stopping the old one; each old one zeroes `AmplitudeGain` when it ends (`CameraShake.cs:33-52`), cutting off newer shakes. AK at 10 shots/s hits this constantly. Replace with a decaying trauma value. ~2 h. Settings scale already applied. |
| Muzzle flash | Exists | `WeaponData.muzzleFlashEffect`. |
| Persistent corpses + splats | Mostly exists | `ChunkedGorePainter` persists blood. Corpses last 30 s (`EnemyDeath.cs:9`). **Trap:** `corpseLifetime = 0` leaks them, because dungeon clear only destroys tagged objects (`RoguelikeManager.cs:354-365`). |
| Shell casings | New, S | Top-down: a kinematic sprite with velocity and drag beats a Rigidbody2D. Cap ~200. |
| Knockback + recoil | Partly exists | Player recoil is per-gun (`playerKnockback`); enemy knockback is one constant per enemy (`Enemy.cs:9`). Scale by gun: S. |
| Custom 2-colour palette | New, S | Existing fullscreen feature. Check against Psychedelic Mode and coloured assets. |
| Dither transition | New, S-M | Scene changes route through Boot/GameManager. |
| Spawn-in telegraph | New, S | Delay `Instantiate` in `AttemptSpawn` (`EnemySpawner.cs:116`); keep budget accounting consistent. |
| Invert flash | Partly exists | `DamageIndicator` flash with `flashIntensity` setting. Cap frequency for photosensitivity. |
| Squash / stretch | Enemy exists | Enemy hit squash exists (`Enemy.cs:406`). Player dash/stomp squash: S. |
| Stomp shockwave, dash dust | Stomp exists | Dash dust S. |
| Pixel-perfect camera | New, **M with trade-off** | Zero `PixelPerfectCamera` uses in project. Conflicts with `CameraZoomOnSpeed` (continuously changes ortho size) and camera lead sub-pixel motion. |
| Do-not list | | Damage numbers default **on** in settings (`GameSettings.cs:24`). Flip default if you follow the doc. Shake and flash-intensity settings already exist. |

---

## 6. Recommended order

1. **Now to Oct 7:** spine #1-#3, then the feel pass (kill hit-stop, shake trauma fix, casings, per-gun knockback), then local run log. Palette only if time remains.
2. **Oct 7-15 (engine still open, content locked):** spine #4 and bug fixes only.
3. **Post-demo:** coefficient on spawners -> shared sprite shader -> obstacle chunks (not the Isaac grid) -> enemy wave 1 -> hit-stage modifiers -> evolution -> parry, style meter, room modifiers, meta unlocks.
4. **Parked, agree with 1BK:** synergies, stat shop, summoners, pacing director, flow graphs. Add: Isaac grid and credit director (cost and identity reasons above).

---

## 7. Manual Editor checklist (Claude only edits C#)

- [ ] Confirm `Renderer2D` FullScreenPass is what drives the CRT and can host a palette pass alongside it.
- [ ] Create the shared sprite Shader Graph material (invert / outline / flash) and assign it to one enemy prefab as a trial.
- [ ] Decide corpse policy (lifetime vs gore-baked) before touching `corpseLifetime`.
- [ ] Decide whether splitter children count as kills.
- [ ] Decide Psychedelic Mode's behaviour under a palette pass.
- [ ] Decide keep-or-drop `CameraZoomOnSpeed` before any pixel-perfect work.

## 8. Not verified

`ShopUIController`, `ChunkedGorePainter` internals, `CthuluEyeEnemy`, the body of `PlayerController`, `ActivateEnemies`, and all scene/prefab serialised values were not opened. Effort numbers are estimates on the 1BK scale, not measurements.
