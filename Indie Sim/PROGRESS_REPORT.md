# Architecture Refactor — Progress Report

**Purpose:** session handoff context for Claude Code. Read this before resuming work, alongside `architecture-refactor-plan-v3.md` (Assets/Scripts) and `AUDIT.md` (project root), which remain the source of truth for what each phase is supposed to do.

**Status as of this report:** Phases 1–6 of 8 implemented and committed. **Phase 7 is deferred post-demo** — per `DemoBeforeIGDC.md`, the Upgrade System took its place in the active work sequence starting 2026-09-17. For current work, see `UpgradeSystemPlan.md` (execution plan + running status) and `UpgradeSystemSpec.md` (design) instead of the Phase 7 section below, which is preserved as-is for whenever Phase 7 is picked back up post-demo. **Controller support / input work (2026-09-28)** is tracked in the Status block at the top of `SettingsAudioInputPlan.md`. **Tutorial scene + settings additions (2026-10-07)** — see "Latest session" below; Play-mode tested and committed (2026-10-08).

**Branch:** `Sarbo`.

**Commits:** `Phase 1 Done` → `Phase 2 Done` → `Phase 3 Done` → `Phase 4 and 5 Done` → `Phase 4 and 5 Bug fixes` → `Phase 6 done`. (Later work is in its own commits — see `git log`.)

---

## Latest session (2026-10-07) — Tutorial, tutorial spawners, settings sliders

**Status:** checklist below passed in Play mode and committed (2026-10-08). Follow-up fix: Continue on a run quit mid-tutorial now returns to the tutorial (`GameManager.ContinueRunInSlot` → `IsRunInTutorial`), not dungeon 1.

### 1. First-run tutorial (`Tutorial.unity`)
- **When it plays:** the first new run on this machine goes to `Tutorial` instead of `RoguelikeMode`. Finishing it sets `PersistentStats.TutorialCompleted`, saved to `profile.json` as the new global section `global.tutorial` (`GlobalTutorialSection`). That flag covers every slot, so later new runs in any slot skip straight to the dungeon. Old profiles without the section load as "not done".
- **Routing:** `GameManager.StartNewRunInSlot` → `LoadRunStart()`, which picks the tutorial or the dungeon. `GameManager.FinishTutorial()` sets the flag, writes the profile, re-saves the run at `LevelStart` (so tutorial coins survive a quit during dungeon 1), then `LoadGame()`.
- **Scene contents:** gameplay objects copied from `RoguelikeMode` (player, Cinemachine camera, HUD canvas, volumes, lights, managers, bullet pools, Pack Manager), plus an empty `Grid` with Ground/Walls/Foilage tilemaps that the user has painted 4 rooms into. No `Main Camera` (Boot's persistent camera is used). HUD Timer, the old `TutorialPanel` popup and `Goal Txt` are switched off in this scene's copy. Added to Build Settings.
- **Scripts** (`Assets/Scripts/Tutorial/`):
  - `TutorialDirector`: owns the hint bubble, the current zone, finishing the tutorial, and death handling.
  - `TutorialHintZone`: a trigger box per room with a hint, a required action (Move / Fire / Dash / Stomp / Reload / SwitchWeapon / None), complete text, replay complete text, disable/enable-on-complete objects, and spawners.
  - `TutorialText`: turns `{Move}`, `{Fire}`, `{Dash}`, `{Stomp}`, `{Reload}`, `{SwitchWeapon}` and `{Pause}` in hint text into the player's current key, or a pad glyph name on gamepad, and follows rebinds.
  - `Editor/TutorialMenu`: menu items **Tools ▸ Tutorial ▸ Add Hint Zone** and **Replay Tutorial On Next New Run**. The second clears the flag, either live or by editing `profile.json`.
- **Exit:** the room-4 `Teleporter`, with the new `Teleporter.finishesTutorial` checkbox on and Requires Key off. Room 4's zone switches the teleporter on once that room is complete. `MusicManager.teleporterTransform` points at it.
- **Hint UI:** `Tutorial Canvas ▸ Hint Banner` holds the user's `chatbox` bubble (rotated 180°, tail at the top right), the cat portrait and `Hint Text`. The text fits inside the bubble's body: it wraps, auto-sizes between 22 and 50, and falls back to an ellipsis. Text is near-black; key names are red.
- **Death in the tutorial:** `TutorialDirector` registers a `GameManager` run-end interceptor. On death it skips the death screen and the run end, shows "Ouch! Let's try that again." for 1.5 s, then calls `GameManager.RestartTutorial()`. That starts a fresh run without bumping `TotalRuns` (`GameSession.StartNewRun(countAsRun:false)`), saves it, and reloads the tutorial. `RetryRun` and `GiveUpRun` also route through `LoadRunStart()`, so they return to the tutorial while it's unfinished.
- **Replay from the main menu:** a new **TUTORIAL** button (`Main menu.unity`, wired to `MainMenu.PlayTutorial` → `GameManager.ReplayTutorial`), shown only once `TutorialCompleted` is set. While `GameManager.IsTutorialReplay` is set, nothing writes to a slot:
  - the exit goes to the main menu;
  - death and Give Up restart the tutorial;
  - quit goes to the menu.
  
  The flag clears on any `LoadMenu` or `LoadGame`. Room 4 shows its Replay Complete Text: "Nice refresher! … head back to the menu."

### 2. Tutorial enemy spawners
- `EnemySpawner` gains **`activateByProximity`**. It defaults to on, so the level spawners are unchanged. When it's off, `PlayerSpawnerActivator` ignores the spawner and only `ActivateSpawner()` wakes it. `EnemySpawner` also gains **`IsCleared()`**, true once the spawner is dead and every enemy it released is dead.
- New prefab variants of `Enemy Spawner` in `Prefabs/Enemies/Tutorial/`. Both spawn fodder only (no ranged enemies or eye), have proximity activation off, and have per-room counts of 0 so dungeon placement never picks them.
  - **Fodder:** 4 enemies at once, 120 HP.
  - **Swarm:** 7 enemies (4, then 3 after 0.5 s), 150 HP.
- **In the scene:** Room 2 has one Fodder spawner. Room 4 has two Swarm spawners 1.4 units apart, 14 enemies in total, which stays under `ActivateEnemies.maxActiveEnemies` (15).
- **Zone behaviour:** a zone wakes its spawners when the player walks in. With **Require Clear** on, the room only completes when the action has been used and the room is cleared.
- **Room hints:**
  - Room 2: "Aim and hold {Fire} to shoot the enemies".
  - Room 4: "They're swarming! {Stomp} to blast them all at once".
- **Coins** from tutorial enemies carry into the run on a first playthrough, and are dropped on replay.
- The main level spawner is **untouched**. Rework and scaling are the next task.

### 3. Settings — Gameplay tab
- **"Flash Intensity" renamed to "Damage Flash Intensity".** The enum is now `FloatSetting.DamageFlashIntensity`; the saved field is still `flashIntensity`, so players' existing values carry over. What it does: it only scales the red damage vignette on hit, from strength 0.3 up to 0.5. It does **not** affect the player sprite blinking (fully on/off, about 8 times a second), hitstop or camera shake, which is why it seemed to do nothing. **Open offer:** have it also scale the sprite blink (the real photosensitivity concern) and make the vignette at 100% bolder.
- **New "Psychedelic Blood Intensity" slider** (`GameSettings.psychedelicIntensity`, default 1), placed under the Psychedelic Mode toggle. `PsychedelicBloodController` blends from the normal blood red (`ChunkedGorePainter.bloodColor`) toward the palette by this amount. 0 looks exactly like normal blood. It only does anything while Psychedelic Mode is on.
- The Gameplay tab's row spacing went from 12 to 7 so six rows fit in the 470 px body.

### Files
- **New:**
  - `Scripts/Tutorial/` (`TutorialDirector`, `TutorialHintZone`, `TutorialText`, `Editor/TutorialMenu`)
  - `Scenes/Tutorial.unity`
  - `Prefabs/Enemies/Tutorial/` (2 prefab variants)
- **Changed:**
  - `GameManager`, `GameSession`, `PersistentStats`, `GlobalSections`, `SaveBootstrap`
  - `Teleporter`, `EnemySpawner`, `ActivateEnemySpawner` (`PlayerSpawnerActivator`)
  - `MainMenu`, `GameSettings`, `SettingBindings`, `DamageIndicator`, `PsychedelicBloodController`
  - `Main menu.unity`, `Settings Panel.prefab`, `EditorBuildSettings.asset`

### Test checklist (passed 2026-10-08)
1. **First run:** run **Tools ▸ Save ▸ Delete All Saves** (or **Tools ▸ Tutorial ▸ Replay Tutorial On Next New Run**), then Boot → Start → pick a slot. The tutorial opens, the TUTORIAL button is hidden, and the crosshair shows.
2. **Hints:**
   - Each room's hint appears in the bubble, stays inside it, and shows the right key for keyboard and for pad.
   - Room 1: moving completes it.
   - Room 3: dashing completes it.
3. **Room 2:** spawners stay asleep until you walk in, then spawn 4 fodder. The room completes only after you fire and every enemy is dead.
4. **Room 4:** the swarm stays asleep until you enter, then two clumps of 7 appear. A stomp plus a full clear reveals the teleporter.
5. **Teleporter:**
   - Loads dungeon 1 with the tutorial's coins.
   - Quitting during dungeon 1 and pressing Continue still has those coins.
   - The TUTORIAL button now shows on the main menu.
6. **Death in the tutorial:** no death screen, the "Ouch" message shows, and the tutorial restarts from Room 1 with 0 coins. Pause ▸ Give Up also restarts it.
7. **No second tutorial:** a new run in another slot goes straight to dungeon 1.
8. **Replay from the TUTORIAL button:**
   - Room 4 shows the replay text, and the teleporter returns to the menu.
   - Death and Give Up restart the tutorial.
   - The slot's saved run is untouched (Continue resumes it as before).
9. **Settings:**
   - Both sliders show and save.
   - Psychedelic Blood Intensity at 0% gives normal blood with the mode on, and 100% gives the full rainbow.
   - Damage Flash at 0% gives no red on hit.
   - The Gameplay tab's six rows don't overflow, in either the main menu or the pause menu.
10. **Regression:**
    - Dungeon spawners still wake by proximity.
    - Dying in a dungeon still shows the death screen, and Retry goes to dungeon 1.

---

## What's been done, phase by phase

### Phases 1–5 — see prior report content in git history (commit `Phase 4 and 5 Bug fixes`) for the detailed blow-by-blow. Summary: bootstrap, purge/cursor unification, camera ownership + D1 (data-driven boss), `GameSession`/`SaveSystem`/PlayerPrefs migration, and the run lifecycle funnel (Retry/ReturnToMainMenu/AdvanceToBoss/CompleteRun) with the Demo Complete screen. All of Phase 5's Known Issues from that report were subsequently fixed this session (see below) — the P0 list is closed.

### Phase 6 — De-persist the managers and the player (this session)

**Core change:** `CoinManager`, `UpgradeManager`, `EnemyKillTracker`, `WeaponAmmoManager` had `DontDestroyOnLoad` removed and now read their starting state from `GameSession.CurrentRun` in `Awake()`/`Start()`, writing back on every mutation (continuous read/write, not write-on-scene-exit — chosen because it mirrors `CoinManager`'s existing pattern and `RelicManager`'s existing correct-by-construction reset). `WeaponUnlockManager` and `WeaponInventory` got the same treatment for unlocks/equipped weapon. `PlayerController`'s unused `Instance` singleton was removed entirely (zero call sites). `CustomCrosshair` lost `DontDestroyOnLoad` but kept its singleton.

`GameSession.BridgeLegacyManagerResets()` (the Phase 5 TEMP bridge) is deleted entirely — `StartNewRun()` is now just "construct fresh `RunStats`, increment `TotalRuns`," the only reset in the project. `PlayerHealth.ResetForNewRun()`, the position-reset hack, and the per-scene-load `deathUIPanel` re-acquisition subscription are all gone too — a plain `Start()` lookup is sufficient now that the object doesn't survive scene loads.

**New `PlayerSpawner.cs`** instantiates the player prefab at `BossArena`'s `PlayerSpawnPoint` in `Awake()` (before anything else's `Start()` runs). **Deliberately asymmetric with `RoguelikeMode`**: that scene keeps its existing scene-baked player instance as-is (recreated fresh on every scene load now that `DontDestroyOnLoad` is gone — functionally equivalent to "spawned per scene" with zero rewiring risk), while only `BossArena` — which never had a player baked in at all — gets the new runtime spawner. This was a user-approved decision to avoid re-wiring several Inspector-only UI references that only `RoguelikeMode`'s scene had correctly set.

**Real gaps discovered during implementation, not anticipated by the plan:** cross-checking `AUDIT.md` against what's actually scene-baked in `BossArena` revealed it never had `CoinManager`, `UpgradeManager`, `EnemyKillTracker`, or `CustomCrosshair` baked in either — they only ever arrived via `DontDestroyOnLoad`. Once that was removed, `BossArena` would have had none of them. The `CoinManager` gap was serious: health is entirely coin-based, so the player would have been unable to take damage in the boss fight at all. Added minimal scene-baked instances of all four directly into `BossArena.unity`, mirroring `RoguelikeMode`'s configuration.

**A real, fully-blocking bug from this session's own scene editing:** `PlayerSpawner.playerPrefab` was initially wired with `{fileID: 100100000, ...}` — the fileID convention for a `PrefabInstance`'s `m_SourcePrefab` field, **not** valid for a plain `SerializeField GameObject` pointing at a prefab asset (which needs the prefab's actual internal root GameObject fileID). This silently broke the reference, so the player never spawned in `BossArena` via any entry path (direct play, `RoguelikeMode → BossArena`, or `Boot → RoguelikeMode → BossArena`) until fixed to the real fileID (`8485655876686838044`, confirmed by reading `Temp -Player.prefab` directly).

**A second class of bug, found via user testing after the fix above:** `WeaponAmmoManager.ReinitialiseUIReferences()`, `WeaponVFXHandler`'s new weapon-button re-acquire code, and `CustomCrosshair.ApplyForScene()` all used `FindFirstObjectByType<Canvas>()` to locate the HUD canvas. This was safe when a scene had exactly one `Canvas`, but `BossArena` now has two (the HUD and `DemoCompleteCanvas`), so the call could silently return the wrong one — `Find("AmmoText")`/`Find("Crosshair")`/etc. would then fail, leaving those UI references null or stale. Symptom was direction-dependent (worked entering `BossArena` directly, broke arriving from `RoguelikeMode`) because it depends on Unity's internal object registry ordering, not a deterministic logic path. Fixed in `CustomCrosshair` and `WeaponVFXHandler` by searching all canvases in the scene for the one that actually has the expected child object, rather than trusting whichever canvas is found first. **`WeaponAmmoManager` got the same fix but the user reports ammo UI in `BossArena` is still not displaying** — see Known Issues below, not yet root-caused.

### Post-Phase-6 fixes (same session, in response to user testing)
- Player sprite flicker on respawn — fixed (Animator Write-Defaults ordering issue, resolved by setting the sprite before re-enabling the Animator in `PlayerHealth.ResetForNewRun()`... which itself was later deleted in the Phase 6 pass proper since the whole method became unnecessary once the player stopped persisting).
- `RetryButton`/`DeathUIPanel` mismatch (RetryButton script lived on the HUD canvas root, not the actual death panel) — fixed by adding a `DeathPanel` field to `RetryButton` and having `PlayerHealth` read that instead of `retryButton.gameObject`. This was the root cause of three separate-looking symptoms: no Retry UI, canvas appearing to "start disabled," and no Retry UI in `BossArena`.
- Demo Complete → Main Menu button unclickable — `DemoCompleteCanvas` (`sortingOrder: 100`) was being out-raycast by the HUD canvas (`sortingOrder: 1000`) sitting on top of it. Bumped to `2000`.
- Cursor invisible during the Demo Complete screen — added `CursorController.SetCursorOverride(bool)` so in-scene UI (paused overlays, end screens) can force the cursor visible without a scene load, without breaking `CursorController`'s role as sole owner of `Cursor.visible`/`lockState`.
- `aliveSprite` on the player prefab was wired to the wrong texture (`PlayerPistol.png` instead of `Playerspriteidle1.png`) — pre-existing authoring mistake, unrelated to any refactor code, fixed directly in `Temp -Player.prefab`.

---

## Known Issues (open, not yet resolved)

1. **Ammo UI not displaying in `BossArena`.** The canvas-ambiguity fix applied to `WeaponAmmoManager.ReinitialiseUIReferences()` (search all canvases for the one with `AmmoText`/`ReloadIcon`, same fix that resolved the identical bug in `CustomCrosshair` and `WeaponVFXHandler`) did **not** resolve this one. Not yet root-caused. Next step: check the `[AmmoManager] UI re-grabbed — ammoText: ..., reloadIcon: ..., playerShooter: ...` log line (already exists in the code) after entering `BossArena`, to see whether `ammoText` is resolving to something or staying null, and whether `currentWeapon`/`playerShooter` are populated. That will show whether it's still a reference problem or actually a value problem (e.g. `WeaponInventory`'s `Start()`-order relative to `WeaponAmmoManager`'s).
2. **Startup lag (~0.5–1s)** entering Play from Main Menu or directly into a gameplay scene. `SceneBootstrapGuard` was confirmed (via added log) to fire whenever Play doesn't start from `Boot.unity` — including when the editor's currently-open scene is Main Menu, not just direct-to-gameplay entry. Whether this additive Boot load is actually the source of the lag, versus something else (dungeon generation, asset loading) masked by the death-delay countdown, is still unconfirmed.
3. **"Shared player canvas has too many dead/unused GameObjects"** — architectural debt flagged by the user in an earlier session, not scoped into any phase. User wants separate canvases per system (death UI, store/upgrades, HUD) eventually. Not urgent; revisit if it starts causing more bugs like the RetryButton/DeathPanel one above (that one was a direct symptom of this canvas's uncontrolled growth, though not literally an unused-object issue).

Resolved this session and can be removed from tracking: sprite flicker, missing pistol on retry (user fixed via Inspector), Demo Complete screen not appearing, Retry UI not appearing, crosshair detaching in `BossArena`.

---

## Where to resume: Phase 7 — Event bus and unified enemy death

Per `architecture-refactor-plan-v3.md`, this is explicitly called out as the **highest-risk phase** ("the one most able to break things quietly... lifetimes get fixed first" — which they now are, as of Phase 6). Tasks: create a static `GameEvents` class (`OnEnemyDied`, `OnCoinsEarned`, `OnUpgradeSelected`, `OnRelicAcquired`, `OnDungeonCleared`, `OnBossPhaseChanged`, `OnBossDefeated`, `OnRunStarted`, `OnRunEnded`); make `OnDungeonCleared` the single signal the upgrade menu/dungeon regen/enemy spawner/boss-transition counter all subscribe to; create one `EnemyDeathHandler` that raises `OnEnemyDied` and have light cleanup/coin drops/stomp state/`EnemyKillTracker` subscribe independently instead of `Enemy.cs` reaching directly into `EnemyKillTracker.Instance`; D3 hooks only (spawner reads `CurrentDungeonLevel` from `GameSession`, subscribes to `OnDungeonCleared`, no scaling logic); replace mutating cross-manager `.Instance` calls with events (read-only lookups and `GameManager` scene-load service calls are fine to leave alone — flag those instead of forcing them through events); convert `RelicManager`'s duplicate-relic coin reward to raise `OnCoinsEarned`.

**Research already done this session, before being interrupted** (so the next session doesn't need to redo it):
- Read `Enemy.cs` in full. It already has a per-instance `public System.Action OnDeath` event (invoked in `Die()`), but `Die()` still reaches directly into `EnemyKillTracker.Instance.RegisterEnemyKill()` — matches the audit's finding, needs to route through the new event bus instead. Blood splatter, coin drop, and the `EnemyDeath.HandleDeath()` handoff (corpse/cleanup) all currently happen inline in `Die()` too.
- Read `EnemyDeath.cs` in full — handles corpse creation, sound, disabling components. Doesn't currently subscribe to anything; it's called directly (`deathHandler.HandleDeath()`) from `Enemy.Die()`.
- Read `EnemySpawner.cs` in full — separate `Die()`/`OnDeath` pattern for the spawner-as-piñata enemy type, own coin-drop logic, own difficulty scaling (`UpdateDifficultyForLevel`) called externally rather than via any dungeon-level signal yet.
- Read `RelicManager.cs` in full — the duplicate-relic coin reward (`GiveCoinsForDuplicate`) currently writes directly to `GameSession.Instance.Persistent.TotalCoinsEverCollected`, bypassing `CoinManager` entirely. This means it inflates the lifetime achievement counter but gives the player **no actually-spendable coins this run** — arguably a pre-existing bug that converting to `OnCoinsEarned` (Phase 7 task 6, as the plan already specifies) would fix as a side effect. Worth surfacing to the user as an intentional behavior change when Phase 7 starts, since the plan's own ground rules require flagging behavior changes explicitly.
- Was about to classify every `CoinManager.Instance` / `UpgradeManager.Instance` / `GameManager.Instance` call site (mutating vs. read-only vs. legitimate `GameManager` service call) via an Explore agent when the user paused the session — that classification still needs to happen before Phase 7 can be planned concretely. Known caller counts from earlier greps (may have shifted slightly since): `CoinManager.Instance` in 10 files, `UpgradeManager.Instance` in 8 files, `GameManager.Instance` in 10 files.
- Have not yet read `TriangleEnemy.cs` (ranged enemy, also calls `EnemyKillTracker.Instance` per `AUDIT.md`) or `BossEnemy.cs`'s death path in detail relative to the new event bus (already read `BossEnemy.cs` partially in an earlier phase — it has its own `OnDeath` action, separate from `Enemy.cs`'s).

**Suggested approach for next session:** same as Phase 6 — research first (finish the `.Instance` call-site classification, read `TriangleEnemy.cs`), then enter plan mode and get explicit sign-off before touching code, given the plan's own "expect iteration" warning and the fact that this phase both adds a new architectural primitive (the event bus) and touches enemy-death code paths across at least three different enemy types (`Enemy`, `TriangleEnemy`, `EnemySpawner`) plus the boss.
