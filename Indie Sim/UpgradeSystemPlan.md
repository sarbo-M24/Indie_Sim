# Upgrade System — Execution Plan

**Companion to:** `UpgradeSystemSpec.md` (design) and `DemoBeforeIGDC.md` (schedule).
**Status:** reviewed against the codebase, not yet started. No code written.

---

## Context

`DemoBeforeIGDC.md` puts a playable demo on Steam before **Oct 28, 2026**, with **Oct 16 as the last working engine day** (Mumbai trip after). The Upgrade System replaces Phase 7 of the architecture refactor in the active work sequence — Phase 7 is deferred post-demo. `UpgradeSystemSpec.md` is the design: a 5-slot "pack" of cigarette upgrades, bought instantly-active from a post-level shop, freed only by "burning" (max tier for a fixed real-time duration once gameplay resumes, then gone — see the "Revised" note under Burning in the spec).

This plan covers the Upgrade System only, Steps 0–5.

### What the code review changed about the spec's assumptions

Three findings reshape the build. They are the reason this plan is not a straight transcription of `DemoBeforeIGDC.md`'s step list.

1. **The catalog is ~8 new mechanics, not data authoring.** `DemoBeforeIGDC.md:74` calls Step 3 "repetitive once the pattern exists — good candidate for plain Sonnet execution." That is wrong. Verified against the code:
   - **Crit does not exist.** `grep -i crit` over `Assets/**/*.cs` returns zero matches. No chance roll, no multiplier, no damage-number variant. 2 catalog entries depend on it.
   - **Player bullets are hitscan with no colliders.** Damage is applied instantly at `PlayerConeShooter.cs:564` / `:456`; the "bullet" is a cosmetic prefab moved by `Vector3.Lerp` (`:599`) and destroyed. There is nothing to bounce. 2 catalog entries depend on it.
   - **No player-owned damaging projectile exists at all.** `Bullet.cs`/`BulletPool.cs` are enemy-only (`Bullet.cs:8` layers are the player). Stomp's "circle of bullets" needs one built from scratch. `BulletVisual.cs` is orphaned — nothing in the project references it.
   - **Dash exists** (`PlayerController.cs:108–189`) but has no damage, no i-frames (`invulnerableDuringDash:24` is declared and never read), and exactly one charge (`canDash` is a `bool`, `:35`). 4 catalog entries depend on extending it.
   - **No multiplier layer exists anywhere.** Every stat in `UpgradeManager` is `base + flatInt`. Crit and the dash damage window both need multiplicative resolution.

2. **The pack must be a pull-based stat resolver, not a push-based mutator.** `UpgradeManager` is currently a stat-resolution *service*: 5 consumer classes call `GetFinalX()` on it every time they need a number. The player is runtime-spawned per scene by `PlayerSpawner`, so any state an effect *pushes* onto player components dies at the `RoguelikeMode → BossArena` boundary. Pull-based also makes `Remove()` trivial and correct by construction: drop the instance, recompute.

3. **A full run has only two shop visits.** `roomsTillBoss = 3` (`RoguelikeManager.cs:19`) and the shop opens only on the non-boss branch (`:281`), so a run is `clear → shop → clear → shop → clear → boss`. A 5-slot pack fed by 2 visits never fills, so Burn — the system's signature mechanic — would essentially never fire. Raising this is part of the plan.

### Decisions taken

- **Weapons:** MachineGun + Shotgun only. Pistol is retired from the demo (asset stays on disk, removed from the inventory).
- **Tier/rarity:** one `CigData` asset per lineage; tier and rarity are rolled at runtime onto a `CigInstance`. 10 assets total, matching the spec's own manual-setup preview.
- **Migration:** clean cut. The old upgrade path is deleted in Step 0. The game runs on flat base stats with no shop until Step 3 lands.
- **Catalog:** cost-ordered, cheapest first, with a hard cut date of **~Oct 6**. Whatever is unfinished is cut and the catalog ships at whatever size it reached.
- **Run length:** raise `roomsTillBoss` to 5–6.

---

## Flagged deviation from UpgradeSystemSpec

`UpgradeSystemSpec.md:64` specifies `CigData.effect` as "a reference to an `IUpgradeEffect`." A C# interface cannot be assigned in the Inspector without either `[SerializeReference]` (awkward authoring) or making every effect its own ScriptableObject asset (8 more assets to hand-create).

**Instead:** `CigData` carries an `effectId` enum, and a static registry in code maps id → effect instance. Effects still implement `IUpgradeEffect` with the exact `Apply` / `ApplyMaxed` / `Remove` lifecycle. This is purely how the reference is resolved.

**Why:** Claude Code may not create assets (`UpgradeSystemSpec.md:9`) — every asset is hand-made work for Sarbo. This keeps that at 10 assets instead of 18, and removes a whole class of "forgot to drag the effect in" wiring bugs.

---

## Architecture

Four pieces per `UpgradeSystemSpec.md:89–94`, all new, all under `Assets/Scripts/Upgrades/` (which today holds only `.asset` files — the project's convention is that SO assets sit beside their C# definition).

| Piece | Responsibility |
|---|---|
| `Pack` | Owns the ≤5 active `CigInstance`s. Enforces replace-in-place by lineage. **Owns `PackStats`** — the recomputed aggregate every consumer queries. Scene-local singleton; rehydrates from `GameSession.CurrentRun` in `Start()`. |
| `CigPool` | Owns the offer pool. Tracks purchased ids (permanently removed for the run). Rolls tier/rarity when generating an offer. |
| `BurnResolver` | Handles burn: flips the instance to burning, registers pending removal, subscribes to the level-boundary signal. |
| `ShopUIController` | Pure UI glue over the three above. Owns no gameplay state. |

**Resolution model.** `Pack.Recompute()` walks the active instances and calls `effect.Contribute(instance, ref stats)`, using the instance's *effective* tier — its rolled tier, or 4 if it is burning. Consumers read `Pack.Instance.Stats`. This is what makes `Remove()` a one-liner and what makes state survive `RoguelikeMode → BossArena` (the list lives in `RunStats`; the scene-local `Pack` rebuilds `Stats` from it on `Start()`, exactly the Phase 6 pattern).

`Apply()` / `ApplyMaxed()` / `Remove()` are kept as the spec's lifecycle, and each just sets instance state and triggers a recompute. Effects that are pure stat contributions do nothing else. Effects that are *behavioral* (dash AoE, deflect, stomp bullet circle) contribute a flag/value to `PackStats`, and the relevant gameplay site reads that flag.

**Damage formula** (`UpgradeSystemSpec.md:75`) `FinalDamage = BaseDamage + UpgradedDamage + (RarityBonus × UpgradedDamage)` lives in one static helper so every effect resolves identically. `BaseDamage` comes from the `WeaponData` SO; `UpgradedDamage` from a per-tier table; `RarityBonus` from a per-rarity table. **Never write to a `WeaponData` asset** — in the Editor a runtime SO write persists across play sessions. The codebase currently honors this invariant (verified: zero runtime assignments to `WeaponData` stat fields); keep it.

---

## Step 0 — Clean cut

Delete the old system before writing anything new, so there is never a moment with two competing upgrade systems.

**Delete:**
- `Assets/Scripts/Managers/UpgradeManager.cs`
- `Assets/Scripts/Managers/StoreManager.cs`
- `Assets/Scripts/Managers/UpgradeButtonUI.cs`
- `Assets/Scripts/Weapons/UpgradeDataSO.cs` + the 8 assets in `Assets/Scripts/Upgrades/`
- `Assets/Scripts/Managers/GameSessionData.cs` — already 100% dead (zero references project-wide); it holds a third competing upgrade model
- `Assets/Scripts/Weapons/WeaponUnlockManager.cs` — no Unlockables in the new design

**Repair the fallout.** Most consumers are already null-guarded and degrade to base stats, but three sites are not:

- `RoguelikeManager.cs:268` — `UpgradeManager.Instance.AdvanceDungeonLevel()` is an **unguarded deref that will hard-NRE**. Replace with `GameSession.Instance.CurrentRun.CurrentDungeonLevel++`.
- `WeaponInventory.cs:105,119,157,190,215` — five **unguarded** `WeaponUnlockManager.Instance.IsWeaponUnlocked()` calls. Strip the gating entirely; the player owns everything they carry.
- `RoguelikeManager.cs:281–284` — `storeManager.OpenStore()`. Leave a TODO; Step 3 replaces it.

**Point the stat consumers at base values** (temporarily, until Step 1's `Pack` exists):
- `PlayerConeShooter.cs:270,283` → return `currentWeapon.baseDamagePerShot` / `maxPierceCount`
- `PlayerController.cs:101` → `currentMoveSpeed = baseMoveSpeed` (this also silently fixes a latent bug: `UpgradeManager.basePlayerSpeed = 5f` was overwriting `PlayerController.baseMoveSpeed = 10f`, so applying a speed upgrade made the player *slower*)
- `PlayerStompController.cs:91–94,226–227` → base radius/damage
- `WeaponAmmoManager.cs:285–361` and `WeaponInventory.cs:272–295` → `weapon.magazineCapacity`

**Trim `RunStats.cs`:** delete lines 26–36 (`AppliedUpgradeIds` + the ten `Bonus*` fields) and line 39 (`UnlockedWeaponNames`). Add:
```csharp
public List<CigInstance> HeldCigs = new List<CigInstance>();   // max 5
public List<string> PurchasedCigIds = new List<string>();      // removed from pool for the run
public List<string> PendingBurnRemovalIds = new List<string>();
```

**Acceptance:** project compiles with zero errors. A full run plays end to end — dungeon → boss → Demo Complete — on flat base stats, with no shop. No NREs on the level-clear path (walk the teleporter, confirm `CompleteDungeon()` completes).

---

## Step 1 — Data model, effect interface, and the level-boundary signal

The spec puts the level-boundary signal at Step 5, but Step 2's acceptance criteria ("the freed upgrade's max-then-remove fires correctly on the next level") cannot be tested without it, and it is roughly ten lines. It moves here.

**New files** in `Assets/Scripts/Upgrades/`:
- `CigEnums.cs` — `Brand` (Mild/Regular/Hard/Mint/Slims/Clove/Electric, cosmetic), `TargetSlot` (PrimaryWeapon/SecondaryWeapon/Stomp/Dash), `Rarity` (Common/Uncommon/Rare/Epic), `CigEffectId`
- `CigData.cs` — the SO. `[CreateAssetMenu(menuName = "Upgrades/Cig Data")]`, matching the project's existing `menuName` convention (`Upgrades/Upgrade Data`, `Weapons/Weapon Data`). Fields per `UpgradeSystemSpec.md:54–64`, with `effectId` replacing `effect`.
- `CigInstance.cs` — `[Serializable]`: `CigData Data`, `int RolledTier`, `Rarity RolledRarity`, `bool IsBurning`. `EffectiveTier => IsBurning ? 4 : RolledTier`.
- `IUpgradeEffect.cs` — `Apply()`, `ApplyMaxed()`, `Remove()`, plus `Contribute(CigInstance, ref PackStats)`.
- `PackStats.cs` — the aggregate struct consumers query (crit chance/multiplier per weapon, bounce counts, stomp radius/damage/bullet count, dash damage window + multiplier, dash AoE radius/damage, deflect flag, extra dash charges).
- `TierRarityTable.cs` — static per-tier `UpgradedDamage` and per-rarity `RarityBonus` values, plus the shared `ResolveDamage()` helper. **Placeholder numbers, clearly marked.** Balancing is explicitly not this pass.
- `CigEffectRegistry.cs` — `CigEffectId` → `IUpgradeEffect`.
- `LevelBoundary.cs` — `public static event Action OnLevelEnded;` + `RaiseLevelEnded()`.

**Wire the signal.** `RoguelikeManager.CompleteDungeon()` (`:253`) is the single chokepoint — reached from `Teleporter.LoadNextDungeon()` (`Teleporter.cs:171`) and `DEBUG_SkipDungeon()` (`:343`). Raise `LevelBoundary.OnLevelEnded` **before the boss branch at `:274`**, so it fires on the boss path too, not just the shop path. Sequence per level is then: clear → signal (removes any still-burning cigs — no carryover, even if their timer hasn't run out) → shop opens → player burns → next level, where the burn timer starts ticking.

> **Static event + scene-local subscriber is the exact leak pattern** `architecture-refactor-plan-v3.md:287` warns about. `BurnResolver` must unsubscribe in `OnDisable`. Audit this specifically.

**Acceptance:** one hand-written test `CigData` asset exists; a debug "buy" call adds it to the pack and its effect shows up in `PackStats` immediately; a debug "burn" call makes it resolve at tier 4; clearing the next level removes it entirely and `PackStats` returns to baseline (not to its pre-burn value).

---

## Step 2 — Pack, CigPool, BurnResolver

- **`Pack`** — scene-local singleton (`Instance`, no `DontDestroyOnLoad`), consistent with every other manager post-Phase-6. `Start()` rehydrates `HeldCigs` from `GameSession.CurrentRun` and calls `Recompute()`; every mutation writes back, mirroring `CoinManager`'s pattern (`CoinManager.cs:65–97`). Enforces replace-in-place: buying a higher tier/rarity of a lineage already held **replaces it in the same slot** (`UpgradeSystemSpec.md:48`). Blocks buying at 5/5.
- **`CigPool`** — holds the `CigData[]` catalog, filters out `PurchasedCigIds`, rolls tier/rarity for each offer. Reuse `StoreManager`'s `Shuffle<T>()` (`StoreManager.cs:213`) — it is the only thing in that class worth keeping; lift it into a small static helper before deleting the file in Step 0.
- **`BurnResolver`** — `Burn(CigInstance)` sets `IsBurning` and starts its `BurnTimeRemaining` countdown (from `CigData.burnDurationSeconds`), recomputes. Ticks every burning instance's timer down in `Update()`, but only while `RoguelikeManager.GameplayInputEnabled` is true (the timer doesn't run while the shop is open, since the shop pauses via input-disable, not `timeScale`). Removes an instance the moment its timer hits zero. Also removes every still-burning instance on `LevelBoundary.OnLevelEnded`, so a level clearing early never lets a burn carry into the next level. (Superseded the original plan's `PendingBurnRemovalIds` list on `RunStats` — `CigInstance.IsBurning` already persists through `RunStats.HeldCigs`, so a second ID list was redundant and has been removed.)

**Point the stat consumers at `Pack.Instance.Stats`**, reverting Step 0's temporary base-value returns. Keep the existing null-guard style — every call site already guards, and that is what makes the system degrade safely if `Pack` is missing from a scene.

**Acceptance:** buying to 5/5 blocks further purchases until something is burned; burning frees the slot; buying a higher tier of a held lineage replaces in place rather than taking a second slot; a burned cig resolves at tier 4, its timer only ticking while gameplay is active, and is fully gone the instant either the timer expires or the level ends (whichever is first) — no carryover either way.

---

## Step 3 — Shop UI

Two tabs, Buy and Burn. Functional, not final art — UI polish is backlog.

Follow the established convention: uGUI, `Screen Space - Overlay`, TextMeshPro, panel hidden via `SetActive(false)` in `Start()`, buttons wired with `onClick.AddListener` in code rather than Inspector-only.

- **Pause by input-disable, not `timeScale`.** The old store used `RoguelikeManager.SetGameplayInputEnabled(false)` (`RoguelikeManager.cs:371`, called from `StoreManager.cs:99`/`:207`). Keep that — `timeScale = 0` would freeze every `WaitForSeconds` coroutine in the shop.
- **Call `CursorController.Instance.SetCursorOverride(true)`** on open and `false` on close (`CursorController.cs:49`), as `DemoCompleteScreen.cs:45` does — otherwise the cursor is invisible over the shop.
- **Continue is code, not a UnityEvent.** The old Continue button called `RoguelikeManager.ContinueDungeon()` via an Inspector UnityEvent on the prefab (`Player Canvas HardcoreMode.prefab:5226`), which is invisible to grep and silently null-targets in BossArena. `ShopUIController` calls `RoguelikeManager.Instance?.ContinueDungeon()` directly instead.
- Replace `RoguelikeManager.cs:281–284`'s `storeManager.OpenStore()` with `ShopUIController.Instance.Open()`.
- **New scope, added after Step 2:** a small **Burning Cigs HUD** — one fill bar per currently-burning cig, live during actual dungeon gameplay (not shown while the shop is open). Bind it to `Pack.Instance.HeldCigs`, filtering to `IsBurning`, reading `CigInstance.BurnFraction` (0-1, already exposed) for the fill amount. This is a HUD element on `Player Canvas HardcoreMode.prefab`, separate from the Buy/Burn shop panel itself.

**Acceptance:** full loop playable — clear a level → shop opens → buy and/or burn → effect visible next level, with its HUD bar counting down only while playing → repeat to death or boss.

---

## Step 4 — Catalog, cost-ordered

Author the 10 lineages in ascending implementation cost. **Cut date ~Oct 6** — whatever is unfinished is cut, and the catalog ships at the size it reached. Each entry ships complete (buys, resolves, burns correctly) before the next starts.

| # | Entry | Cost | Notes |
|---|---|---|---|
| 1 | **Stomp: radius + damage** | Low | `PlayerStompController.cs:91–94` already reads bonus radius/damage. Pure `PackStats` wiring. **Spec mismatch:** `UpgradeSystemSpec.md:115` describes this as "seeks enemies in a range instead of only hitting directly under the player" — but stomp is *already* a 5-unit AoE (`stompRadius = 5f`, `:14`, via `OverlapCircleAll` at `:132`). Reinterpreted as a radius+damage tier. |
| 2 | **Dash: AoE on completion** | Low | Reuse `PlayerStompController.DamageAndPushEnemies()` (`:130–178`) wholesale — same overlap + wall-LOS + `TakeDamage` shape. Radius = dash distance (`dashSpeed * dashDuration`, `PlayerController.cs:121`). Fires at the end of `DashCoroutine` (`:170`). |
| 3 | **Dash: 2s damage window + red tint** | Low-med | Timed multiplier in `PackStats`, read by `PlayerConeShooter.GetDynamicWeaponDamage()` (`:270`). Tint via the player `SpriteRenderer`. |
| 4 | **Crit — both weapons** | Medium | Builds the crit system; covers **2 catalog entries**. Roll **per-hit** at `PlayerConeShooter.cs:564` and `:456`, *not* per-shot at `:298` — shotgun fires 6 pellets (`pelletsPerShot = 6`, `:354`) all sharing one damage int, so a per-shot roll crits all six at once. Needs a crit variant on `DamageNumberManager.Spawn()` (`:24`); `ReportDamage()` (`:766`) is the single funnel. |
| 5 | **Chain dash** | Medium | Convert `canDash` (`PlayerController.cs:35`) from `bool` to a charge counter and restructure the cooldown tail at `:179–188` into per-charge recharge. Touches the dash UI fill (`SetDashFill`, `:195`). |
| 6 | **Bullets bounce — both weapons** | Med-high | **2 entries.** Keep hitscan; implement as a chained re-target. `FirePiercer`/`PiercingBulletTrailCoroutine` (`:399`, `:449`) is already a working "one shot, N targets, exclusion list" precedent — bounce is that with a per-hop re-aim. `BulletTrailCoroutine` (`:559`) needs to take a waypoint list so `Vector3.Lerp` at `:599` becomes per-segment. Do **not** convert to physics projectiles; that would move damage out of the shooter entirely and touch ammo, crosshair, and every weapon branch. |
| 7 | **Stomp: circle of bullets** | High | Needs a player-owned damaging projectile, which does not exist. Adapt `BulletPool`/`Bullet` (`Assets/Scripts/Enemy/boss_type/`) with an enemy-layer variant. First candidate to cut. |
| 8 | **Dash: deflect projectiles** | High | Must intercept enemy `Bullet` instances during dash and re-own them — flip direction and swap `damageableLayers` (`Bullet.cs:8`) from player to enemy. Second candidate to cut. |

**Also in this step:** raise `roomsTillBoss` (`RoguelikeManager.cs:19`) from 3 to 5–6 so a run has 4–5 shop visits. This is a tuning value to settle by feel, not a balance pass.

**Acceptance:** every shipped entry buys, appears active immediately, resolves at tier 4 when burned, and reverts to nothing after the next level.

---

## Step 5 — Checkpoint

Playable end to end with real content. Everything past this is backlog (UI polish, sound, animations, boss variants), not upgrade-system work.

---

## Manual setup for Sarbo (Unity Editor)

Per `UpgradeSystemSpec.md:9`, Claude Code only writes C#. These are handed off as checklists at the step that needs them, not done directly.

**After Step 0:**
1. Remove `Pistol` from `WeaponInventory.availableWeapons` on `Temp -Player.prefab` — leave `MachineGun` and `Shotgun`.
2. Delete the now-missing-script GameObjects left behind in `RoguelikeMode.unity` and `BossArena.unity` (`Upgrade Manager`), and the `Store Page Panel` / `nextscene` / `Upgrade Name` / `Upgrade Desc` / `Upgrade Cost` / `Insufficient Coins` objects on `Player Canvas HardcoreMode.prefab`.

**After Step 1:** create one test `CigData` asset in `Assets/Scripts/Upgrades/` to validate the buy/burn debug path.

**After Step 2:** add a `Pack Manager` GameObject to `RoguelikeMode.unity` and `BossArena.unity`, carrying `Pack`, `CigPool`, and `BurnResolver`. Assign the catalog array on `CigPool`.

**After Step 3:** build the Shop panel as a sibling of the old store panel inside `Player Canvas HardcoreMode.prefab` — that prefab is instanced in **both** gameplay scenes, so it exists everywhere automatically. Two tabs, a card list, a Continue button. Wire to `ShopUIController`.

**After Step 4:** create the remaining `CigData` assets, one per shipped lineage. Naming follows the project's Title-Case-with-spaces convention.

---

## Verification

1. **Compile clean** after every step. No new warnings.
2. **Play directly in `RoguelikeMode` and in `BossArena`** — both must produce a playable scene. This is the Phase 6 acceptance bar and the new managers must not break it.
3. **Buy loop:** clear a level → shop → buy → confirm the effect is live *that same level*, not next. Fill to 5/5 → confirm further purchases are blocked.
4. **Replace-in-place:** buy a lineage, then buy a higher tier of the same lineage → confirm it occupies the same slot and the pack count does not grow.
5. **Burn loop:** burn a cig → confirm it resolves at tier 4 and its HUD fill bar counts down only while playing (frozen if the shop reopens) → confirm it's removed the instant the timer hits zero, or immediately on level-clear if that happens first → confirm `PackStats` is back to baseline afterward, not to its pre-burn value.
6. **Scene boundary:** buy cigs in `RoguelikeMode`, advance to boss, confirm the pack and every effect survive into `BossArena` (the list is in `GameSession.CurrentRun`; `Pack` rehydrates in `Start()`).
7. **Run boundary:** die → Retry → confirm the pack is empty, the offer pool is full again, and coins are 0. `GameSession.StartNewRun()` constructs a fresh `RunStats`, so this should be correct by construction — verify it explicitly rather than assuming.
8. **Leak audit:** confirm `BurnResolver` unsubscribes from `LevelBoundary.OnLevelEnded` in `OnDisable`. Enter and leave `RoguelikeMode` several times, then clear a level and confirm the handler fires exactly once.
9. **Debug path:** `RoguelikeManager.DEBUG_SkipDungeon()` (`:343`) routes through `CompleteDungeon()` — confirm the signal fires there too, since that is the fast way to test the burn lifecycle.

---

## Found during review, deliberately out of scope

Recorded so the findings aren't lost. None of these are touched by this plan.

- **BossArena ammo UI bug — root-caused.** `WeaponAmmoManager.cs:64` searches for a child named `"Bullet number"`. **No object with that name exists anywhere in the project**; the real object is `Ammo Count` (`Player Canvas HardcoreMode.prefab:6009`), whose TMP component is the exact fileID that `RoguelikeMode`'s working Inspector override points at. RoguelikeMode is unaffected only because the fallback guards with `if (ammoTextTransform != null)` (`:72`) and never clobbers the good reference; BossArena has no override because its player is runtime-spawned, so it falls through to the broken lookup. **The fix is one string.** The same block's `reloadIcon` lookup for `"ReloadIcon"` (`:69`) is also a name that exists nowhere, so `SetReloadFill()` (`:260`) silently no-ops everywhere. This closes Known Issue #1 in `PROGRESS_REPORT.md`.
- **`RelicManager` duplicate-relic reward gives no spendable coins** — `RelicManager.cs:130–131` writes to `Persistent.TotalCoinsEverCollected` while the actual `CoinManager.AddCoins` call sits commented out at `:126`. Inflates the lifetime achievement counter, gives the player nothing. Orthogonal to upgrades.
- **The machine gun is unreachable through normal play today.** `Assets/Scripts/Upgrades/Unlock MachineGun.asset` has `upgradeType: 2` (`GunUpgrade`) instead of `1` (`UnlockGun`), so it unlocks nothing — and the pool filter at `UpgradeManager.cs:276–281` hides it until the machine gun is already unlocked. Moot once the asset is deleted in Step 0, but worth knowing so it isn't mistaken for a regression the new system caused.
- **`WeaponInventory`'s ammo dictionary (`:267–306`) duplicates `WeaponAmmoManager`** — two competing ammo sources of truth. The `WeaponInventory` half appears to have no external call sites.
- **`PlayerAutoAimShooter.cs` is fully block-commented AND will not compile if uncommented** — it references `weapon.damagePerShot`, a field renamed to `baseDamagePerShot`. `WeaponController.cs` and `BulletVisual.cs` are likewise dead. Deletion candidates for a future purge.
- **`StoreManager.OnSceneLoaded` searched for `"StorePanel"`** (`:67`), a name that also exists nowhere — that re-wiring path always failed and always logged an error. Moot once the file is deleted in Step 0.
