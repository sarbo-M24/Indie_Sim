# Upgrade System — Execution Plan

**Companion to:** `UpgradeSystemSpec.md` (design) and `DemoBeforeIGDC.md` (schedule).

**Status (2026-09-23, latest) — Bullet Bounce range cap, and Chain Dash/Chain Stomp switched to a reset-and-burst regen model.**

1. **Bullet Bounce chains are now anchored to their first hit.** `PlayerConeShooter.FindNearestBounceTarget()` previously searched `coneRange` from each successive hop's own position, so a multi-bounce chain could walk hop-by-hop across the whole map (e.g. 4 bounces × 8-unit cone range ≈ 32 units of possible total travel from the original impact). New `maxBounceChainRange` field (default 15, tune to taste) rejects any bounce candidate farther than that from the *original* hit position — each hop still respects `coneRange` for that individual jump, but the whole chain now stays within one contained area.
2. **Chain Dash / Chain Stomp regen model changed.** Previously charges refilled one at a time on a shared timer (mirrors how it's always worked). Now: using *any* charge resets the cooldown timer to full (previously the timer only started if it wasn't already counting down), and when that timer elapses without being interrupted by another use, **every** missing charge refills at once (`PlayerController.TickDashRecharge()`/`DashCoroutine()` and `PlayerStompController.TickStompRecharge()`/`TryStomp()`, changed symmetrically in both). With no Chain Dash/Chain Stomp held (max charges = 1), this is behaviorally identical to before — it only changes anything once you're holding extra charges.

---

**Status (2026-09-23) — Crit/Bounce split, new Shotgun Pellet Count upgrade, and `hasTierRarity` decoupled into `hasTier`+`hasRarity`.**

1. **`CritChanceCigData` is now crit-chance-only.** Its old `damagePerTier` field and weapon-damage contribution are gone. Rarity no longer touches crit chance at all — tiers are the only thing that scale chance now. Rarity instead scales a new `baseCritDamageMultiplier` field (crit damage), replacing the old flat, unscaled `critDamageMultiplier`.
2. **`BulletBounceCigData` absorbed the flat-damage role Crit used to have.** `bounceCount` stays a single flat value (does not scale with tier), but the asset now also has `damagePerTier[5]` + a `rarityConfig` reference, contributing `Primary/SecondaryWeaponBonusDamage` the same additive-plus-rarity way Crit used to. This asset needs `hasTier=true, hasRarity=true` now (previously fully flat).
3. **New `ShotgunPelletCountCigData.cs`** — Secondary-only, tiered, **no rarity**. This is the first upgrade in the project with tiers but no rarity roll, which required an architecture change (next point) rather than a workaround.
4. **`CigData.hasTierRarity` (one bool) split into `hasTier` + `hasRarity` (two independent bools)**, touching the roll pipeline every upgrade goes through: `CigPool.RollInstance()` now rolls Tier and Rarity independently off their own flag, and `ShopUIController.ShowDetail()` shows "Tier X | Rarity" only when both are true, "Tier X" alone when only `hasTier` is true, and the old "Flat upgrade" text when neither is. Confirmed with Sarbo before doing this — the alternative (keep one flag, just have the new upgrade ignore a rarity it still rolls and displays) was explicitly rejected in favor of doing this properly.
5. **`PlayerConeShooter.FireShotgun()`** now sources pellet count from a new `GetDynamicPelletCount()` (base 6 + `PackStats.ShotgunBonusPellets`) instead of the hardcoded `6`.

**Not done by this pass, per `UpgradeSystemSpec.md`'s own Claude-Code constraint** ("may only create and edit C# scripts... output a manual setup checklist" instead of touching assets/scenes directly) — every existing `.asset` file needs manual Inspector updates before this compiles-and-plays correctly:
- **All 13 existing assets:** replace the now-gone `hasTierRarity` checkbox with the two new ones — set both `Has Tier` and `Has Rarity` to match the old value (on for every asset except `PrimaryBullet Bounce`/`Secondary Bullet Bounce`/`Dash Deflect`/`Chain Dash`/`Chain Stomp`, which were `false`/`false`).
- **`Primary Dmg & Crit.asset` / `Secondary Dmg & Crit.asset`:** the old `Damage Per Tier` field is gone from the Inspector (harmless orphaned data on disk) — nothing to re-enter, `critChancePerTier`/`rarityConfig` carry over unchanged, just rename/re-check `baseCritDamageMultiplier` (was `critDamageMultiplier`, same 1.5 default).
- **`PrimaryBullet Bounce.asset` / `Secondary Bullet Bounce.asset`:** set `Has Tier` + `Has Rarity` to **true** (was false/false), fill in the new `Damage Per Tier` array, and wire `Rarity Config` to the shared asset — these fields didn't exist on this class before.
- **New asset:** create `Secondary Pellet Count` (Create → Upgrades → Shotgun Pellet Count Cig), `targetSlot = SecondaryWeapon`, `hasTier = true`, `hasRarity = false`, brand = **Mint** (proposed — Secondary now has 4 upgrades needing 4 distinct brands, same situation Dash hit with Chain Dash/Hard), fill in `extraPelletsPerTier`, give it a unique `id`/`displayName`/`description`/`icon`/`cost`, and add it to `__Pack Manager__.prefab`'s `CigPool.catalog` (10 → wait, 13 → 14).

---

**Status (2026-09-23, later still) — Brand exclusivity is back, reversing an earlier spec decision.** A prior spec pass explicitly retired "one brand per slot" as cosmetic-only; Sarbo asked for it back, mechanically. See the raw pre-change asset data snapshot at `UpgradeCatalogSnapshot.md`.

1. **No new fields needed** — `CigData.brand` (`Brand` enum: Mild/Regular/Hard/Mint/Slims/Clove/Electric) and `CigData.targetSlot` already existed, unused (every asset had `brand: 0`/Mild by default). The whole feature is new logic over two already-existing fields.
2. **`Pack.cs`** gained `GetBrandConflict(CigData) : CigInstance` (same-slot, both-Mild-or-Regular check, skips same-lineage) and `BuyWithReplace(offer, toReplace)` (checks affordability *before* removing the held cig, so a failed purchase never destructively drops it). `Buy()` now fails (returns false) if `GetBrandConflict` is non-null — callers must use `BuyWithReplace` for that path.
3. **`ShopUIController.cs`**: `OnBuyButtonClicked` checks for a conflict first; if found, it doesn't buy — it stores the pending offer/conflict, fires `public static event Action<CigInstance, CigInstance> OnBrandConflictDetected`, and shows a warning in `detailText`. New public `ConfirmReplacePurchase()`/`CancelReplacePurchase()` resolve the pending state — these are the hooks for Sarbo's own confirmation panel (backend-only, per his direction — he's building the panel UI himself). Selecting a different buy card, or any other selection-clearing path, abandons a pending replace so it can't get confirmed against a stale card.
4. **All 13 existing assets got a real `brand` value** (previously all `0`/Mild, unused) — assigned so every slot's Mild+Regular pair are the "pick one, replace to switch" upgrades and everything else stays always-buyable exactly as it already behaved: Crit=Mild/Bounce=Regular/FireRate=Electric per weapon slot, Seek=Mild/Circle=Regular/Chain Stomp=Electric for Stomp, Post-Damage=Mild/AoE=Regular/Deflect=Electric/Chain Dash=Hard for Dash (Dash needed a 4th distinct brand for its 4th upgrade — confirmed with Sarbo that all upgrades participate in the brand system, not just a "power upgrade" subset). This is a proposed assignment, freely changeable per-asset in the Inspector — see the table in `UpgradeSystemSpec.md`'s new "Brand exclusivity" section.

**Manual work still needed, not done by this pass:** the actual replace-confirmation panel UI (warning text + confirm/cancel buttons wired to `ShopUIController.ConfirmReplacePurchase()`/`CancelReplacePurchase()`) — Sarbo is building this himself.

---

**Status (2026-09-23, later same day) — Chain Stomp + Primary/Secondary Fire Rate, 3 new catalog entries (13 total).** Both mirror existing patterns exactly rather than inventing new ones.

1. **Stomp converted from a single-cooldown ability into a real charge system**, mirroring `PlayerController`'s dash charges 1:1: `PlayerStompController` gained `stompCharges`/`stompRechargeTimer`/`MaxStompCharges` (`1 + Pack.Instance.Stats.StompExtraCharges`) and a `TickStompRecharge()` ticked every `Update()` (this component didn't have an `Update()` before), replacing the old `canStomp` bool + `StompCooldownRoutine()` coroutine entirely. Confirmed safe before starting: nothing outside `PlayerStompController.cs` reads `canStomp`/`CanStomp()`, so no external code assumed stomp was a binary ready/not-ready flag.
2. **`ChainStompCigData.cs`** (new, flat, no tiers/rarities — exact mirror of `ChainDashCigData.cs`) — contributes `PackStats.StompExtraCharges`.
3. **`FireRateCigData.cs`** (new, tiered + rarity, two asset instances via `targetSlot` — mirrors `CritChanceCigData`'s per-weapon-slot pattern) — contributes `PackStats.Primary/SecondaryFireRateBonus` as a % bonus. Confirmed with Sarbo: tiers + rarity (not flat), and the bonus is a percentage applied multiplicatively (`PlayerConeShooter.GetDynamicFireRate() = baseFireRate * (1 + bonus)`), not a flat shots/sec add — a flat add would've hit the Pistol's `fireRate: 2` far harder than the MachineGun's `fireRate: 10`.
4. `PlayerConeShooter.cs`'s fire-cooldown line now calls `GetDynamicFireRate()` instead of reading `currentWeapon.fireRate` directly. `PlayerAutoAimShooter.cs` (confirmed fully block-commented/dead) was not touched.

**Manual setup still needed, not done by this pass:** create 3 new `.asset` instances (`Chain Stomp Cig`, and `Primary`/`Secondary Fire Rate Cig` from the one `Fire Rate Cig` class with `targetSlot` set accordingly), wire the two Fire Rate ones' `rarityConfig` to the shared `RarityConfig` asset and set `hasTierRarity = true` on them, then add all 3 to `__Pack Manager__.prefab`'s `CigPool.catalog` in `RoguelikeMode.unity` (`catalog.Array.size` 10 → 13) with unique `id`s and `displayName`/`description`/`icon`/`cost` filled in. Placeholder `fireRateBonusPerTier` array and Chain Stomp's `extraCharges` default to values matching Crit Chance's/Chain Dash's own placeholders — tune to taste.

---

**Status (2026-09-23) — Dash rework: invulnerability, pass-through, end-of-dash clear, and a real tiered Dash AoE. Implemented and confirmed working in-editor by Sarbo.** This directly supersedes point 6 below (the "radius derived from dash distance, never authored" rule) and the flat "Dash AoE Cig" row in point 8's table.

1. **Dash now grants real i-frames and lets the player pass through enemies**, neither of which existed before — `PlayerController`'s `invulnerableDuringDash` field had been dead code since it was added (never read anywhere). `PlayerHealth.cs` gained a dedicated `isDashInvulnerable` flag (kept separate from the existing debug `invincible` toggle so the two never fight) gating `TakeDamage()` — the single damage entry point for every enemy type, including the two Pentagram enemies which damage via a polled distance check rather than physics collision, so a physics-layer trick alone would not have covered them. Pass-through uses per-collider `Physics2D.IgnoreCollision(Collider2D, Collider2D, bool)`, not a layer-wide `IgnoreLayerCollision` — the Player sits on the shared "Default" layer (no dedicated Player layer exists), so a layer-wide toggle would have disabled collision for every other Default-layer object too. `DashCoroutine` now wraps its loop in `try/finally` so invulnerability/pass-through cleanup always runs, even on an early wall-hit `break`.
2. **A small radius always pushes enemies out the instant a dash ends — no damage, not upgrade-gated.** `PlayerStompController.DamageAndPushEnemies()` needed a `damage > 0` guard added before its damage-application block first, since calling it with `damage = 0` was previously still triggering `Enemy.TakeDamage(0)` (hit-flash/knockback/damage-number spawn) for zero actual damage.
3. **Dash AoE Cig reworked from flat/single-shot into a real tiered lineage**, mirroring `CritChanceCigData`'s pattern: `damagePerTier[5]` and `radiusPerTier[5]` arrays (index 0 unused) plus a shared `RarityConfig` reference, with the rarity bonus boosting *both* damage and radius (confirmed with Sarbo — some tiered effects elsewhere only apply rarity to damage, this one applies to both). It now ticks every physics step *during* the dash instead of once at dash-end: enemies inside the live radius are pushed continuously (every tick) but only damaged once per enemy per dash, tracked via a `HashSet<IDamageable>` local to that dash's coroutine call — confirmed with Sarbo as the intended model over a stacking damage-over-time field. `PackStats.DashAoeRadius` is back (was explicitly removed by point 6 below) since radius is now authored per-tier, not derived from travel distance.
4. **New debug-only Scene-view gizmo on `PlayerController`** (`showDashAoeDebugRadius`, `debugDashAoeRadiusPreview`) — a cyan static preview circle for tuning before Play mode, plus an orange live-radius circle sourced from `Pack.Instance.Stats.DashAoeRadius` while actually playing. Pure `Gizmos.DrawWireSphere` calls inside `OnDrawGizmosSelected()`, no gameplay logic attached, never runs in a build.

**Manual setup still needed, not done by this pass:** on `Assets/Prefabs/Upgrades/Dash AOEDmg.asset`, flip `hasTierRarity` to **true** (it's currently `false`, which forces `CigPool.RollInstance()` to always roll tier 1/Common regardless of code — would silently make the new tiered arrays always resolve flat if left unchanged), wire `rarityConfig` to the shared `RarityConfig` asset, and fill in the new `damagePerTier`/`radiusPerTier` values (the old flat `damage` field is dropped automatically by Unity's serializer). `dashPassthroughRadius`/`dashEndClearRadius` on the Player prefab are placeholder defaults (`1.5`/`1.75`) — tune to taste.

---

**Status (2026-09-21) — reconciled against a rewritten `UpgradeSystemSpec.md`, untested in-editor.** Sarbo replaced the spec with a new version that changes two load-bearing things from everything below: **burn duration** and **effect data ownership**. Everything in this pass is a code-only reconciliation — no assets touched.

1. **Burn is no longer time-based.** Old model: a real-time countdown (`burnDurationSeconds`) ticking only during gameplay, removed at timer-zero *or* level-end, whichever came first. New model (per spec): burning maxes Tier for the next level only and is removed the moment that level ends — full stop, no timer at all. Removed `CigInstance.BurnTimeRemaining`/`BurnFraction` entirely and deleted `BurnResolver.Update()`'s timer-tick — `HandleLevelEnded()` (unchanged) is now the *only* removal path. `BurningCigsHUD` lost its fill-bar countdown (no fraction to show anymore) and is now a plain "burning this level" on/off indicator per slot. `ShopUIController`'s detail text no longer shows a seconds-remaining readout.
2. ~~**Effect magnitudes must be Inspector-tunable per effect, not one shared hardcoded formula.** ... A new `CigEffectConfig` MonoBehaviour holds one Inspector-editable instance of each of the 8 effects; `CigEffectRegistry` routes through `CigEffectConfig.Instance`...~~ — **superseded same-day by a second spec revision, see below.** `CigEffectConfig`/`CigEffectRegistry`/`CigEffects.cs` no longer exist.
3. ~~**`IUpgradeEffect` signature changed**... Kept `Contribute(CigInstance, ref PackStats)` as a fourth method...~~ — the signature and the `Contribute` addition are both still accurate, just implemented differently now (see below).
4. **`CigData.lineageId` is gone.** Per the new spec, "one asset = one lineage now, so no separate lineageId needed" — `id` alone is the replace-in-place key. `Pack.FindByLineage`/`CanAdd`/`TryAdd` now key off `instance.Data.id`. **This resolves the previously-flagged bug** where `Stomp Upgrade` (`id: 201`) and `Stomp Upgrade 2` (`id: 202`) shared `lineageId: 3` and would've incorrectly replaced each other in one slot — with `id` as the only key, they're automatically independent slots. The existing 5 `.asset` files still have an orphaned `lineageId` value serialized on disk; Unity silently ignores it since the field no longer exists in code. **Superseded by point 7 below anyway** — those 5 assets need to be recreated regardless.
5. **`CigData.burnDurationSeconds` is gone** — meaningless under the new burn model.
6. **Dash AoE radius fixed to match spec wording.** Spec: "dash distance = the circle's diameter." `PlayerController.ApplyDashAoe()` uses `distanceTraveled / 2f` as the radius (was the full distance, plus a per-tier bonus the spec explicitly rules out — "Radius is derived at runtime from the dash's actual distance, never authored"). `PackStats.DashAoeRadius` field removed.

**Resolved, same day:** the new spec's Core Loop section describes "two tabs: Buy and Burn," but that's explicitly superseded by the user's decision — the shop UI **stays** as shipped in Step 3: Buy and Burn both visible at once in one panel, no tabs, independent selections/confirm buttons for each. `UpgradeSystemSpec.md`'s wording is stale on this one point; `ShopUIController.cs`'s current shape (see Step 3 below) is the source of truth for the shop layout, not the spec's tab description.

---

## Second same-day revision — `CigData` becomes an abstract base, effects live on the assets themselves

Sarbo replaced the spec again a few hours later with a third version that changes *where effect data and code live* — this supersedes point 2 above entirely, before `CigEffectConfig` was ever wired into a scene.

7. **`CigData` is now `abstract` and implements `IUpgradeEffect` directly.** No more `effectId` enum + registry indirection: the concrete subclass asset *is* the effect. `CigEffectId` enum, `CigEffectRegistry.cs`, `CigEffectConfig.cs`, and `CigEffects.cs` are all deleted. `Pack.cs`/`BurnResolver.cs` now call `instance.Data.Apply(...)`/`.ApplyMaxed(...)`/`.Remove()`/`.Contribute(...)` directly instead of going through a registry lookup. `Contribute(CigInstance, ref PackStats)` stays on the interface for the same reason as before (pull-based, scene-boundary-safe) — the spec is silent on the "how," this isn't in conflict with it.
8. **8 concrete `CigData` subclasses, one file each** (`Assets/Scripts/Upgrades/*.cs`), each `[CreateAssetMenu]`-tagged so Sarbo can right-click → Create → Upgrades → *(name)* individually. Crit and Bounce are each one subclass instantiated twice (once per weapon slot) — 8 subclasses cover the 10 lineages, per the spec's own count:

   | Subclass file | CreateAssetMenu name | Lineage(s) it covers | Own tunable fields |
   |---|---|---|---|
   | `CritChanceCigData.cs` | Upgrades/Crit Chance Cig | Primary crit, Secondary crit (2 instances, `targetSlot` picks the bucket) | `critChancePerTier[5]`, `critDamageMultiplier` (flat), `damagePerTier[5]` (flat weapon damage — see the 2026-09-21 "weapon dmg" update below), `rarityConfig` ref |
   | `BulletBounceCigData.cs` | Upgrades/Bullet Bounce Cig | Primary bounce, Secondary bounce (2 instances) | `bounceCount` (flat) |
   | `StompSeekCigData.cs` | Upgrades/Stomp Seek Cig | Stomp seek | `radiusPerTier[5]`, `damagePerTier[5]`, `rarityConfig` ref |
   | `StompCircleCigData.cs` | Upgrades/Stomp Circle Cig | Stomp circle | `bulletCountPerTier[5]`, `damagePerTier[5]`, `rarityConfig` ref |
   | `DashPostDamageCigData.cs` | Upgrades/Dash Post-Damage Cig | Dash post-damage window | `damageMultiplierPerTier[5]`, `durationSeconds` (flat), `rarityConfig` ref |
   | `DashAoECigData.cs` | Upgrades/Dash AoE Cig | Dash AoE | `damage` (flat) |
   | `DashDeflectCigData.cs` | Upgrades/Dash Deflect Cig | Dash deflect | none — pure flag |
   | `ChainDashCigData.cs` | Upgrades/Chain Dash Cig | Chain dash | `extraCharges` (flat) |

   Every field lives directly on the asset's own Inspector alongside the inherited identity fields (`id`, `displayName`, `description`, `icon`, `brand`, `targetSlot`, `hasTierRarity`, `cost`) — nothing to cross-reference, per the spec's whole point of this revision.
9. **New `RarityConfig.cs`** (`[CreateAssetMenu]`) — the shared rarity→bonus asset, replacing the old static `TierRarityTable` C# class entirely (deleted). One asset, 4 fields (Common/Uncommon/Rare/Epic bonus %). Every tiered subclass above has its own `rarityConfig` field — **wire all of them to the same one `RarityConfig` asset instance** (the 11th asset). The 4 purely-flat subclasses (Bounce, DashAoE, DashDeflect, ChainDash) don't reference it at all since they never use rarity.
10. **⚠️ Breaking for the 5 existing `.asset` files.** `MachineGun Upgrade.asset`, `Shotgun Upgrade.asset`, `Dash Upgrade.asset`, `Stomp Upgrade.asset`, `Stomp Upgrade 2.asset` were all created as instances of the old concrete `CigData` class. Now that `CigData` is `abstract`, Unity cannot load them as-is — they'll show as broken/missing-type in the Inspector once this compiles. **These are not being auto-migrated** (Claude Code doesn't touch assets) — see Manual Setup below for what replaces them. The old 5 files can be deleted once their replacements exist; until then they're harmless dead weight, not a compile error (C# scripts compile fine regardless of what's serialized on disk).

---

## Design change (2026-09-21, same day) — weapon upgrades now grant damage too, not just crit

Per the user's direction: the MachineGun and Shotgun upgrades should both increase weapon damage *and* crit chance from the same cig, using `FinalDmg = WeaponBaseDmg + UpgradeDmg + (RarityBonus × UpgradeDmg)` — the same additive-plus-rarity shape as every other tiered effect, just applied to weapon damage now too.

- **`CritChanceCigData`** (kept, not removed or split into a new class) gained a `damagePerTier[5]` field (`UpgradeDmg`) alongside its existing crit fields. `Contribute()` now also writes `PrimaryWeaponBonusDamage`/`SecondaryWeaponBonusDamage` (new `PackStats` fields) using the same `rarityConfig` bonus already resolved for crit chance — one asset, one rarity roll, two effects.
- **`PlayerConeShooter.GetDynamicWeaponDamage()`** — `WeaponBaseDmg` (`currentWeapon.baseDamagePerShot`) now has the resolved `UpgradeDmg + RarityBonus×UpgradeDmg` total added on top, per weapon slot, before the (separate, pre-existing) dash-damage-window multiplier is applied.
- No asset changes needed beyond what Step 4's checklist already asks for — `MachineGun Crit Upgrade`/`Shotgun Crit Upgrade` (or whatever Sarbo names them) are still `Crit Chance Cig` instances, just with the new `Damage Per Tier` array now visible in their Inspector to fill in alongside the crit-chance array.

---

**Status (2026-09-18):** Steps 0–3 implemented, committed (`712ab28`, `b9297b1`, `0817873`, `38eb012` "Store UI added"), and playtested working in-editor. `__Pack Manager__.prefab` (`Pack` + `CigPool` + `BurnResolver`) is instanced in both `RoguelikeMode.unity` and `BossArena.unity`, closing gap #2 below. The shop UI is live on `Player Canvas RoguelikeMode.prefab` and confirmed working end to end: buy, burn, and the gameplay burn-countdown HUD.

**Step 3 deviated from this doc's original design in one deliberate way, at the user's direction:** no Buy/Burn tabs. Both lists are visible at once in the same panel, each with its own independent card selection and its own confirm button (`buyButton` / `burnButton` on `ShopUIController`), so a buy selection and a burn selection can be held simultaneously without one clearing the other. The single shared `confirmButton` + tab-toggle design described further down is superseded by this — see `ShopUIController.cs` for the current shape.

**New this step, not in the original plan:** `BurningCigsHUD.cs` — a fixed pool of `Slider`s (sized to `Pack.MaxSlots`) on the gameplay HUD, positional against `Pack.Instance.HeldCigs` like the shop's card slots. Each slot shows/hides based on whether that held cig `IsBurning` and its value follows `CigInstance.BurnFraction` (1 → 0) every frame. `ShopUIController` hides it on `Open()` and restores it in `Close()`, since burn only ticks while gameplay input is enabled (shop open = frozen, by design — unchanged). `ShopUIController.PopulateBurnCards()` also now filters out already-burning cigs from the Burn list, since they can't be burned again — their countdown is only visible on this HUD once the player leaves the shop.

**Correction:** earlier drafts of this doc's manual-setup checklist said to build the shop on `Player Canvas HardcoreMode.prefab`. That prefab is stale/unused (only referenced by the old `Assets/Scenes/Test Scenes/HardcoreMode.unity`). The canvas actually instanced in both live scenes, and where the shop was actually built, is `Assets/Prefabs/UI/Player Canvas RoguelikeMode.prefab`.

**Two bugs hit and fixed during Step 3 build-out**, worth knowing if similar symptoms show up again:
- `CigCard.prefab`'s `CigCardUI.selectedHighlight` was wired to the card's own root GameObject instead of a dedicated child. Since `SetSelected(false)` runs at the end of both `SetEmpty()` and `Populate()`, every populated card immediately deactivated itself — this was the "store comes up empty" symptom. Fixed by pointing `selectedHighlight` at `None` (a dedicated highlight child can be added later for the visual).
- `MachineGun Upgrade.asset` and `Shotgun Upgrade.asset` originally shared `lineageId: 100`, which would have made buying one replace the other in the same pack slot (`Pack.TryAdd`'s replace-in-place keys off `lineageId`). Fixed — they're `1` and `2` now.

~~**Still-open item found while reviewing the above:** `Stomp Upgrade.asset` (`id: 201`, `effectId: 5`) and `Stomp Upgrade 2.asset` (`id: 202`, `effectId: 6`) — two distinct mechanics per the Step 4 catalog table (entries #1 and #7) — currently **both have `lineageId: 3`**. Unlike the fixed MachineGun/Shotgun case, their `id`s differ, so `CigPool` will still offer both independently; but `Pack.CanAdd`/`TryAdd` will treat a second one bought while the first is held as a replace-in-place, not a second pack slot.~~ — **Resolved by the 2026-09-21 spec reconciliation**: `lineageId` no longer exists, `id` is the sole replace-in-place key, and `201`/`202` are already distinct — so these two are automatically independent pack slots now, no asset edit needed.

Two open gaps from Step 2, still relevant:

1. **"Replace a held lineage with a higher tier" is unreachable through the actual shop flow for any single-asset-per-lineage entry.** `Pack.TryAdd()` (`Assets/Scripts/Upgrades/Pack.cs:85`) implements replace-in-place correctly in isolation, but `CigPool` permanently removes a lineage's only `CigData` id from the offer pool the instant it's bought (`CigPool.cs:5–9`, `:46`), so that lineage can never be re-offered to trigger the replace branch — except via the two-assets-one-lineage pattern noted above. **Needs a decision:** either the pool needs a re-offer mechanism for owned lineages, or that section of the spec is stale and the replace-in-place code path (and this acceptance criterion) should be dropped. Secondary, smaller issue in the same code path: the replace branch swaps the held instance without calling `Remove()`'s effect cleanup on the old one first — harmless today since only `NoOpEffect` is registered, but will silently skip cleanup once a stateful Step 4 effect exists.
2. ~~`BossArena.unity` has no `Pack Manager` GameObject yet~~ — resolved, `__Pack Manager__.prefab` is now in both scenes. `BurnResolver.Update()`'s pause guard still only checks `RoguelikeManager.Instance`, which is null in `BossArena` — worth remembering if `BossArena` ever gets its own pause mechanism, since burn timers there currently just tick unconditionally.

**Step 4 catalog code — all 10 lineages implemented (2026-09-21), untested in-editor.** Every `CigEffectId` now has a real effect registered in `CigEffectRegistry` (see `Assets/Scripts/Upgrades/CigEffects.cs`), and every gameplay site the catalog table calls out has been wired to read `Pack.Instance.Stats`. Not playtested yet — built in one pass per the user's direction, to be tested together once done. Also done in this pass: `roomsTillBoss` raised 3→5 (`RoguelikeManager.cs:16`).

What changed, by system:
- **`PlayerStompController.cs`** — `PerformStomp()` now adds `StompBonusRadius`/`StompBonusDamage` on top of the flat radius/damage, and fires a `FireBulletCircle()` ring of player-owned bullets when `StompBulletCount > 0`. `DamageAndPushEnemies()` is now `public` (reused by Dash AoE). The bullet circle reuses the enemy `Bullet`/`BulletPool` — no new prefab — by overriding `damageableLayers`/`destructionLayers` per-shot via `Bullet.Initialize`'s new optional params, to the stomp's own `stompEnemyLayer`/`stompWallLayer`. Pool is found at runtime via the `"BulletPool"` tag (same convention as `TriangleEnemy`) or `bulletPoolOverride` if you'd rather wire it directly.
- **`PlayerController.cs`** — `canDash` (bool) replaced with `dashCharges` (int), capped at `1 + Pack.Instance.Stats.DashExtraCharges`; charges recharge one at a time on a shared timer instead of gating the whole ability, so Chain Dash lets you dash again immediately if a charge is banked. Dash completion now fires `ApplyDashAoe()` (radius = actual distance travelled + `DashAoeRadius`, reuses `PlayerStompController.DamageAndPushEnemies`) and `StartDashDamageWindowIfActive()` (tints the body sprite and starts a timer `PlayerConeShooter` reads via the new `GetDashDamageMultiplier()`). Every dash physics step also calls `TryDeflectBulletsNearby()`, which flips any enemy `Bullet` within `deflectDetectionRadius` (new field) around the player.
- **`PlayerConeShooter.cs`** — `GetDynamicWeaponDamage()` now multiplies by the dash damage window. Crit is rolled per-hit (not per-shot) inside `BulletTrailCoroutine`/`PiercingBulletTrailCoroutine` via the new `ApplyCrit()`, so a shotgun's 6 pellets crit independently. Bounce chains from `BulletTrailCoroutine` via `ChainBounces()`/`FindNearestBounceTarget()`, extending the visual bullet's path through `AnimateBulletAlongPath()` (multi-waypoint Lerp instead of one straight line). Primary vs. Secondary is resolved by `currentWeapon.weaponType == Shotgun` (`IsSecondaryWeapon`), since MachineGun/Shotgun are the only two weapons and already differ on that field — no new "which slot" data needed.
- **`Bullet.cs`/`BulletPool.cs`** — `Initialize`/`SpawnBullet` gained optional `damageableLayersOverride`/`destructionLayersOverride` params (default `null` = unchanged, fully backward compatible with every existing enemy call site). Added `Bullet.Deflect(LayerMask, Vector2)` for Dash Deflect and a public `CurrentVelocity` getter.
- **`DamageNumberManager.Spawn`/`DamageNumberPopup.Initialize`** — gained an optional `isCrit` param (default `false`) that colors/scales the popup and appends `!`.

**Manual work still needed before this is testable — see "After Step 4" under Manual Setup below**, which lists the 5 new `CigData` assets to create and the new Inspector fields to wire on the player prefab. Until those exist, the corresponding upgrades simply can't be offered/granted (the `DebugUpgradeInjector` tool needs a `CigData` asset per entry too).

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

## Flagged deviation from UpgradeSystemSpec — RESOLVED by the second same-day spec revision

This section originally covered `effectId` + a static registry as a workaround for "a C# interface can't be assigned in the Inspector." **That workaround is gone.** The spec's newest revision resolves the underlying problem a different way: `CigData` is `abstract` and each concrete subclass *implements* `IUpgradeEffect` directly, so there's no interface reference to assign in the first place — the asset itself is the effect. See "Second same-day revision" above for the current shape. Left here only so the history of *why* the old `effectId` design existed isn't lost.

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

**After Step 3 — done (2026-09-18), built and playtested working.**

Final shape, superseding the "locked in" design originally written here: no tabs. `Cig Card` prefab (`Assets/Prefabs/Upgrades/CigCard.prefab`) is a dumb, reusable component — `icon` Image, `rarityIcon` Image, a `Button`, a `selectedHighlight` GameObject (currently `None`, see the fixed-bugs note above). `ShopUIController` shows the Buy list and the Burn list simultaneously in one panel on `Player Canvas RoguelikeMode.prefab`, each with its own selection and its own confirm button (`buyButton` / `burnButton`, no shared Confirm, no tab-toggle). `RoguelikeManager.cs:277` calls `ShopUIController.Instance.Open()` if one exists in the scene, falling back to `ContinueDungeon()` otherwise.

`BurningCigsHUD` (new, not in the original plan) sits separately on the same canvas, outside `Shop Panel`, and shows live burn countdowns during gameplay — see the top status section for details.

`__Pack Manager__.prefab`'s `CigPool.catalog` now holds 5 `CigData` assets (`MachineGun Upgrade`, `Shotgun Upgrade`, `Dash Upgrade`, `Stomp Upgrade`, `Stomp Upgrade 2`), instanced into both `RoguelikeMode.unity` and `BossArena.unity`.

Rarity icon sprites are still placeholders/unset (`rarityIcons` is empty on `ShopUIController` in-scene) — cosmetic only, doesn't block testing.

**After Step 4 — superseded by the second same-day spec revision. Do this instead:**

**In progress:** Sarbo has already created `New Crit Chance Cig Data.asset`, `New Stomp Seek Cig Data.asset`, and `Rarity Config.asset` in `Assets/Prefabs/Upgrades/` (confirms the new subclass scripts compile clean in the Editor). Still need: the other 8 lineage assets, the `rarityConfig` wiring on the ones created so far, renaming/filling in `id`/`displayName`/`cost`/etc., and adding all 10 to `CigPool.catalog`.

1. **Delete or ignore the 5 old `.asset` files** (`MachineGun Upgrade`, `Shotgun Upgrade`, `Dash Upgrade`, `Stomp Upgrade`, `Stomp Upgrade 2` in `Assets/Prefabs/Upgrades/`) — they're instances of `CigData`, which is now `abstract` and can't be loaded as a concrete type. Note their old `id`/`cost`/`displayName`/`description`/`icon` values before deleting if you want to carry them over to the replacements below.

2. **Create all 10 `CigData` assets fresh**, via right-click → Create → Upgrades → *(subclass name)* in the Project window — each subclass shows up as its own menu entry (see the table in "Second same-day revision" above for exact subclass/menu names and which fields each one has). For each:

   | New asset (suggested name) | Subclass to create | targetSlot | hasTierRarity |
   |---|---|---|---|
   | MachineGun Crit Upgrade | Crit Chance Cig | PrimaryWeapon | true |
   | Shotgun Crit Upgrade | Crit Chance Cig | SecondaryWeapon | true |
   | MachineGun Bounce Upgrade | Bullet Bounce Cig | PrimaryWeapon | false |
   | Shotgun Bounce Upgrade | Bullet Bounce Cig | SecondaryWeapon | false |
   | Stomp Seek Upgrade | Stomp Seek Cig | Stomp | true |
   | Stomp Circle Upgrade | Stomp Circle Cig | Stomp | true |
   | Dash Post-Damage Upgrade | Dash Post-Damage Cig | Dash | true |
   | Dash AoE Upgrade | Dash AoE Cig | Dash | false |
   | Dash Deflect Upgrade | Dash Deflect Cig | Dash | false |
   | Chain Dash Upgrade | Chain Dash Cig | Dash | false |

   Give each a unique `id` string (e.g. reuse `101`/`102`/`201`/`202`/`301` for the 5 that replace old assets, pick new ones for the other 5) and fill in `displayName`/`description`/`icon`/`cost` as before. Add all 10 to `__Pack Manager__.prefab`'s `CigPool.catalog` array.

3. **Create 1 `RarityConfig` asset** (right-click → Create → Upgrades → Rarity Config) and set its 4 bonus-% fields. **Wire it into every tiered asset's `rarityConfig` field**: MachineGun Crit, Shotgun Crit, Stomp Seek, Stomp Circle, Dash Post-Damage (5 of the 10 — the other 5 are flat and have no `rarityConfig` field at all).

4. **Wire new Inspector fields on `Temp -Player.prefab`:**
   - `PlayerStompController`: `Stomp Bullet Speed`/`Stomp Bullet Lifetime` (defaults are fine to start) — `Bullet Pool Override` can stay empty, it auto-finds the scene's `BulletPool` by tag.
   - `PlayerController`: `Enemy Bullet Layer` — set to whatever layer enemy bullets live on (same layer as `PlayerStompController`'s existing `Stomp Bullet Layer`). `Deflected Bullet Target Layer` — set to the enemy layer (same as `PlayerStompController`'s `Stomp Enemy Layer`). `Deflect Detection Radius` (default `1.2`) and `Dash Damage Window Tint` (default red) are cosmetic/feel — tune by testing.

5. **Known cosmetic caveat, not fixed in this pass:** `PlayerController`'s new dash-damage-window tint and `PlayerHealth`'s existing hit-flash both write directly to the same body `SpriteRenderer.color` with no coordination. If you get hit while the dash window is active (or vice versa), one can stomp the other's color — visual glitch only, not a gameplay bug. Worth a look if it's noticeable in testing.

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
- **Dash Deflect upgrade can't detect any bullets today.** `PlayerController`'s `enemyBulletLayer` field is an empty/unset `LayerMask` on the Player prefab, so `TryDeflectBulletsNearby()`'s `Physics2D.OverlapCircleAll(..., enemyBulletLayer)` matches nothing. Found while reworking Dash for the 2026-09-23 invulnerability/AoE pass (same coroutine, unrelated bug) — not fixed since it's an Inspector-only setting, not code. Set it to the enemy-bullet layer (same one `PlayerStompController`'s `Stomp Bullet Layer` already uses) whenever Dash Deflect gets tested.
- **`StoreManager.OnSceneLoaded` searched for `"StorePanel"`** (`:67`), a name that also exists nowhere — that re-wiring path always failed and always logged an error. Moot once the file is deleted in Step 0.
