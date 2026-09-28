# Save System — 4A Audit Report

Input: `save-system-spec.md`. Written 2026-09-28, before any code, per 4A's "audit first, then stop" rule.
**Deferred at the user's request:** 4E and Section 7 (making the dev panel available to players who win).

---

## 1. Where each saved field lives today

| Spec field | Where it lives | In `GameSession.CurrentRun`? |
|---|---|---|
| Coins | `CoinManager._currentCoins` / `_maxCoins` / `_coinsCollectedThisRun`, mirrored after every change by `SyncToRunStats()` | **Yes**: `CurrentCoins`, `MaxCoins`, `CoinsCollectedThisRun` |
| Upgrades held (tier, rarity, burning) | `Pack._held`, mirrored to the run | **Yes**: `HeldCigs` (`List<CigInstance>`: `Data`, `RolledTier`, `RolledRarity`, `IsBurning`) |
| Upgrades bought (removed from the pool) | `CigPool._purchasedIds`, mirrored to the run | **Yes**: `PurchasedCigIds` |
| Stack counts / levels | There are none. One held instance per lineage; `RolledTier` (1–4) is the "level". | n/a |
| Relics | `RelicManager.relicsCollectedThisRun` (`bool[8]`) + `uniqueRelicsCollectedThisRun` | **No.** `RunStats.RelicsHeld` / `UniqueRelicsCollectedThisRun` exist but nothing writes them. `RelicManager` resets in its own `Start()`, so relics already don't survive RoguelikeMode → BossArena. |
| Weapon unlocks | **No unlock system exists.** The loadout is the fixed `WeaponInventory.availableWeapons` array on the player prefab (Pistol, AK47, Shotgun). | Only `EquippedWeapon` (and `WeaponAmmo`, which the spec says not to save) |
| Dungeon number | `RunStats.CurrentDungeonLevel` and `DungeonsClearedThisRun`, both updated in `RoguelikeManager.CompleteDungeon()` | **Partly.** `RoguelikeManager` keeps its own `dungeonsClearedCount`, `currentLevel` (spawner difficulty) and `dungeonSizeIncrement` (a random +1 on 50% of clears). All three start at 0/1/0 in `Start()` and **never read from `CurrentRun`**. The boss trigger (`dungeonsClearedCount >= roomsTillBoss`, 5) uses the local counter. |

Other run data that isn't in the spec's table: `KillsThisRun` (in the run), `RunElapsedSeconds` (field exists, nothing writes it), `CurrentHealth` / `MaxHealth` (unused, since coins are HP), `SelectedBoss` (set by `AdvanceToBoss`).

## 2. Stable IDs on content

| Content | Asset type | ID today |
|---|---|---|
| Upgrades | `CigData` (abstract SO), 12 assets | **Yes:** `id` string, all unique (`Mild-1..4`, `Regular-1..4`, `Electric-1..4`). No duplicate/empty-ID validation exists yet. |
| Weapons | `WeaponData` SO, 3 assets (`Scripts/Weapons/`) | **No `id`.** Only `weaponName` (display text). |
| Relics | **Not a ScriptableObject.** `Relic` is a MonoBehaviour on 8 prefabs. | `int relicIndex` 0–7 only. |

Catalogs: `CigPool` has a serialized `catalog` array, but it's a scene object in RoguelikeMode. Nothing in Boot can resolve an ID. Restoring at the main menu needs a catalog reachable from Persistent scope, so the plan is a `ContentCatalog` ScriptableObject referenced from Boot.

## 3. PlayerPrefs keys

| Key | Written by | Stores | Status |
|---|---|---|---|
| `TotalCoinsEverCollected` | nothing now | lifetime coins (legacy) | Read once by `SaveSystem`'s migration |
| `TotalEnemiesKilled` | nothing now | lifetime kills (legacy) | same |
| `UnlockedAchievements` | nothing now | achievement IDs as CSV (legacy) | same |
| `SoundEnabled` | nothing now | old sound toggle | Read and **deleted** by `SettingsService.Migrate()` |
| `JoystickScale` | `GUIScaler` | mobile joystick size | Live setting, leave it alone |
| `OptionsMenu.SetCustomSetting(name)` | no callers | — | Dead API |

**Discrepancy with the spec:** the spec says to move achievements out of PlayerPrefs. That was already done in Phase 4. Achievements and lifetime stats currently live in **`persistent_stats.json`** (`SaveSystem`, JsonUtility), which holds `PersistentStats`: `TotalCoinsEverCollected`, `TotalEnemiesKilled`, `UnlockedAchievementIds`, `TotalRuns`, `BestRunDungeonsCleared`, `DemoCompleted`, `PlayerPrefsMigrated`. Settings aren't in PlayerPrefs either; they're in `settings.json` (`SettingsService`, including input rebinds). Both stay untouched, since settings are out of scope.

## 4. Newtonsoft

**Present only transitively**, as `com.unity.nuget.newtonsoft-json` (resolved 3.2.1), pulled in by `com.unity.pipeline 0.8.0-exp.1` (experimental). Plan: add it to `manifest.json` explicitly so it doesn't disappear if that package is removed.

## 5. Lifecycle call sites

| Event | Where |
|---|---|
| New run | `MainMenu.LoadScene1()` → `GameManager.StartNewRun()` → `GameSession.StartNewRun()` + load RoguelikeMode. Retry → `GameManager.RetryRun()` → same path. |
| Level start | `RoguelikeManager.Start()` → `GenerateNewDungeon()` (first dungeon); `RoguelikeManager.ContinueDungeon()` (every later one, same scene) |
| Level clear | `RoguelikeManager.CompleteDungeon()`: increments counters, raises `LevelBoundary`, then opens the shop or goes to the boss after clear #5 |
| **Store entry** | `ShopUIController.Open()`, called from `CompleteDungeon()`. The store is an **overlay inside RoguelikeMode**, not a scene. |
| **Store exit** | `ShopUIController.OnContinueClicked()` → `Close()` + `RoguelikeManager.ContinueDungeon()`. **There is no Save & Exit button yet.** |
| **Death** | `PlayerHealth.Die()` (private), reached from `TakeDamage` (coins run out) and `KillPlayer()` (dungeon timer). It shows the death UI after `deathDelay`. **It does not call `EndRun`**, so there is no death-finalisation point yet. |
| Death screen exits | Retry → `RetryRun()` (no `EndRun`); Main Menu → `PlayerHealth.GoToMainMenu()` → `ReturnToMainMenu()` → `EndRun(false)` |
| Pause quit | `OptionsMenu` → `GameManager.ReturnToMainMenu()` → `EndRun(false)`. There's no warning prompt. |
| Victory | `BossEnemy.OnDeath` → `BossSceneManager.OnBossDefeated()` → `GameManager.CompleteRun()` → `EndRun(true)` → `DemoCompleteScreen.Show()` |

**Phase 5 status:** the funnel exists (`GameManager`: `StartNewRun`, `RetryRun`, `ReturnToMainMenu`, `AdvanceToBoss`, `CompleteRun`), so 4D is **not** blocked by the sequencing rule. 4D still has to add a real death-finalisation call, because `Die()` bypasses the funnel today.

## 6. Other findings that affect the design

1. **Saving on every kill.** `EnemyKillTracker.RegisterEnemyKill()` calls `GameSession.Save()` on every kill, and also saves from `OnApplicationQuit` and `OnApplicationPause`. `AchievementManager` and `RelicManager` also write lifetime stats. Under the new model these are profile writes, not slot writes. I'll keep them as profile-only writes, and they never touch a slot.
2. **Store resume.** The store is an overlay that opens after `CompleteDungeon()`. Resuming into it means loading RoguelikeMode and opening the shop without first running the normal level start. Today `RoguelikeManager.Start()` always generates a dungeon. 4D will add a resume branch there.
3. **Resuming at the boss never happens.** Clear #5 goes straight to BossArena with no store and no save point. The latest checkpoint before the boss is therefore always LevelStart(5), a dungeon. The spec's "if depth N maps to BossArena" case can't occur with the current table. I'll route through the existing `roomsTillBoss` check anyway and add no new rules.
4. **Pause quit ends the run.** `ReturnToMainMenu()` calls `EndRun(false)`. Today that only folds best-run stats and doesn't wipe anything, so a mid-level quit is compatible with the checkpoint as long as 4D doesn't make `EndRun` wipe the slot on this path.
5. **No test runner.** `com.unity.test-framework` is only transitive, and play-mode tests aren't run from the CLI here. For the 4A/4B acceptance checks I plan **editor menu self-tests** (`Tools/Save/Run Self-Test`) that you trigger and read in the console.
6. **The old `SaveSystem` component** sits on a GameObject under `[Persistent]` in `Boot.unity`. Replacing it means deleting `SaveSystem.cs` and removing that GameObject from Boot.
7. **There is no Boot composition-root class.** `GameSession.Awake()` currently plays that role. I'll create a small `SaveBootstrap` there that builds `SaveService` and registers the sections.

---

## 7. Decisions needed before code

1. **Lifetime stats.** The spec lists only `global.achievements`, but `PersistentStats` also holds lifetime coins/kills (achievement progress), `TotalRuns`, `BestRunDungeonsCleared` and `DemoCompleted`. *Proposal:* one extra `global.stats` section for these.
2. **Existing `persistent_stats.json`.** Import it once into `profile.json` and delete it, or drop it (the spec says "no migration")? *Proposal:* import once, since it holds real achievement progress.
3. **Relics.** State lives only in `RelicManager`, and IDs are int indices. *Proposal:* make `RelicManager` read and write `CurrentRun` like `CoinManager` does, and save relic IDs as `"relic_0".."relic_7"`, derived from the index, with no prefab changes. The alternative is a `string id` on the `Relic` component. Either way, `RelicManager` needs a small change that is outside the spec's systems.
4. **Weapons.** There's no unlock system. *Proposal:* add `id` to `WeaponData` (`pistol`, `ak47`, `shotgun`), and have `run.weapons` save the **equipped weapon ID** plus an owned-IDs list (currently always the full loadout) so real unlocks can drop in later.
5. **Dungeon counters.** On a LevelStart(N) resume, `RoguelikeManager` has to seed `dungeonsClearedCount`, `currentLevel` and the boss countdown from `CurrentRun`. `dungeonSizeIncrement` is random per clear and not in `RunStats`. Save it, or re-roll it on resume? *Proposal:* save it in `run.progress`, since it's cheap and keeps difficulty honest.
6. **Run stats outside the spec's table** (`KillsThisRun`, `CoinsCollectedThisRun`, used by the Demo Complete summary). Save them or not? *Proposal:* save them. Otherwise a resumed run's end summary undercounts.
7. **`global.demo` unlock flag.** You've deferred the dev panel. Should 4D still write the unlock flag on victory (data only, no panel gating), or skip `global.demo` completely for now? *Proposal:* write the flag, since it costs nothing and keeps the save format stable, and leave all panel gating for later.

---

## 8. Decisions (answered 2026-09-28)

1. `global.stats` section for lifetime stats: **yes**.
2. Import `persistent_stats.json` into `profile.json` once, then delete it: **yes**.
3. Relics: save them as a **secondary meta currency** (no plans for it yet; keep the option open). Scope still to be confirmed, see the 4A report.
4. `WeaponData.id` + `run.weapons` (equipped + owned IDs): **yes**. Two weapons are in use now, and more are coming.
5. Save `dungeonSizeIncrement` in `run.progress`: **yes**.
6. Save `KillsThisRun` / `CoinsCollectedThisRun`: **yes**.
7. Write the `global.demo` unlock flag on victory, with no panel gating: **yes**.

The system is pre-ship and will keep changing until it's finalised a few days before release.
