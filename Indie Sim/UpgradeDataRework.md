# Upgrade Data Rework — IGDC Demo catalog

Implements `Copy of Upgrade list sheet (one bit kill) Category 1 - IGDC Demo.csv` (the upgrade sheet). This pass covers **data only**: `CigData` subclasses, `PackStats` fields, `Contribute()` mapping. Gameplay consumers of the new stats are the **Logic** pass (Step L below). The same rules from `UpgradeSystemSpec.md` apply: Claude Code edits C# only; asset changes are a manual checklist.

---

## Verdict — refactor, not rebuild

The architecture already fits the sheet: abstract `CigData` → one asset per lineage → `Contribute()` into a pull-based `PackStats` → `Pack` / `CigPool` / `BurnResolver` / `ShopUIController`. Brand exclusivity (Mild/Regular exclusive per slot, Electric free) matches the sheet's three brands as-is. Nothing about buying, burning, or the pack needs to change. **What's wrong is the data**: which upgrades exist, what tiers scale, and what rarity touches.

`RarityConfig` stays exactly as it is.

---

## The sheet vs. the code

| Slot | Brand | Sheet: Tiers scale | Sheet: Rarity scales | Code before | Change |
|---|---|---|---|---|---|
| Primary | Mild | crit chance | crit dmg % | `CritChanceCigData`, same | tier table only |
| Secondary | Mild | crit chance | crit dmg % | same | tier table only |
| Stomp | Mild | dmg + radius | dmg % | `StompSeekCigData` (dmg + radius) | **renamed `StompPowerCigData`** (it never seeked) |
| Dash | Mild | dmg % | — | `DashPostDamageCigData`, **had rarity** | rarity removed; stored as bonus % |
| Primary | Regular | flat dmg + bounce count | dmg % | `BulletBounceCigData`, **bounce count flat** | bounces now per tier |
| Secondary | Regular | flat dmg + bounce count | dmg % | same | same |
| Stomp | Regular | flat dmg + bullet count | dmg % | `StompCircleCigData`, same | tier table only |
| Dash | Regular | dmg + knockback force | dmg % + knockback % | `DashAoECigData`, **dmg + radius** | radius becomes fixed; **knockback added** per tier |
| Primary | Electric | fire rate | — | `FireRateCigData`, **had rarity** | rarity removed |
| Secondary | Electric | pellets | — | `ShotgunPelletCountCigData`, same | tier table only |
| Stomp | Electric | more stomps, + cooldown | — | `ChainStompCigData`, **flat, no cooldown** | charges + cooldown penalty per tier |
| Dash | Electric | more dashes, + cooldown | — | `ChainDashCigData`, **flat, no cooldown** | charges + cooldown penalty per tier |

**Dropped from the demo catalog:** `Dash Deflect` (not on the sheet) and `Secondary Firerate` (the Secondary Electric slot is now Pellets). Their scripts stay — the sheet is "Category 1", so they may return — they just come out of `CigPool`'s catalog array.

**12 lineages, 12 assets, every one tiered.** 7 have rarity (Crit ×2, Stomp Power, Bounce ×2, Stomp Circle, Dash Push); 5 don't (Dash Post-Damage + all four Electric).

---

## Data-model changes

**`hasTier` removed.** The sheet says every upgrade has 4 tiers, so it was always true. `CigPool` always rolls 1–4. `hasRarity` stays and is the only switch.

**`rarityConfig` moved to the `CigData` base.** Same field name, so every existing asset keeps its reference. One helper, `GetRarityBonus(instance)`, returns 0 when `hasRarity` is false, so a rarity-less asset with a config wired in still can't pick up a bonus.

**`TierValues` / `TierValuesInt` replace the 5-slot arrays.** The old arrays had an unused index 0 (and the FireRate assets had a stray `0.5` in it). The new structs show **Tier 1–4** as four labelled fields in the Inspector. `EffectiveTier` (maxed to 4 while burning) indexes them directly.

**Rarity formula (unchanged):** `Final = TierValue × (1 + RarityBonus)`. For crit it scales the *extra* crit damage (`1.5×` → `+0.5`), not the 1× baseline.

**New `PackStats` fields:** `DashAoeKnockback`, `StompCooldownPenalty`, `DashCooldownPenalty`. Written in this pass, read in Step L.

---

## Steps

**D1 — Base + tier types.** `TierValues.cs` (new), `CigData.cs` (drop `hasTier`, hold `rarityConfig`, add `GetRarityBonus`). *Accept:* compiles; old assets still show their rarity config.

**D2 — Subclasses per the table.** Every subclass uses `TierValues`. `StompSeekCigData` → `StompPowerCigData` via a `git mv` of the `.cs` **and** `.cs.meta`, so the script GUID and the `Stomp Seek.asset` binding survive. *Accept:* each Inspector shows Tier 1–4 fields matching the sheet's "Tiers" column, and no rarity-less upgrade touches `GetRarityBonus`.

**D3 — `PackStats` + callers of `hasTier`.** New fields; `CigPool.RollInstance` and `ShopUIController.ShowDetail` stop reading `hasTier`. *Accept:* project compiles; F1 debug HUD shows the new fields.

**D4 — Manual asset pass (Sarbo).** See checklist below.

**L — Logic pass (done 2026-09-24).** New stats wired into gameplay:
- **Chain Stomp / Chain Dash cooldown.** `PlayerStompController.StompCooldown` and `PlayerController.DashCooldown` = base cooldown + the pack's penalty. They drive both the recharge timer and the UI fill, so the icon's radial fill stays correct.
- **Dash Push knockback.** `DamageAndPushEnemies` takes an optional `knockbackForce`. When it's above 0: an outward impulse lands on the same call the enemy takes damage (so once per enemy per dash, sharing `dashAoeHitThisDash`); it goes through `EnemyMovement.ApplyKnockback` when present, and later pushes skip zeroing velocity so the impulse carries. Stomp and the dash-end clear pass 0, so their behaviour is unchanged.
- **No change needed:** bounce count per tier (`ChainBounces` already reads the count), post-dash % (`GetDashDamageMultiplier`), fire rate, pellets, crit.
- *Accept:* F1 HUD `+cd` matches the slower icon refill after buying a Chain cig; Dash Push visibly throws enemies further at higher tiers; stomp push feels identical to before.

---

## D4 — Manual asset checklist

Tier values reset to the script defaults (current placeholder numbers) because the field types changed — re-enter any you'd tuned.

1. `CigPool` on `__Pack Manager__.prefab` → **Catalog**: remove `Dash Deflect` and `Secondary Firerate`. 12 entries remain.
2. Set **Brand** per the table above: Mild = 0, Regular = 1, Electric = 6. (Chain Dash is currently Hard → set Electric.)
3. Set **Has Rarity**: ✅ Primary/Secondary Crit, Stomp Seek (now Stomp Power), Primary/Secondary Bullet Bounce, Stomp Circle, Dash AOEDmg. ❌ Dash Post Dmg, Primary FireRate, Secondary pellet cig, Chain Stomp, Chain Dash.
4. Every **Has Rarity ✅** asset: make sure **Rarity Config** points at `Rarity Config.asset` (the Bullet Bounce assets currently have none).
5. **Shotgun Pellet Count** — the asset doesn't exist yet: Create → Upgrades → Shotgun Pellet Count Cig, `targetSlot` Secondary, brand Electric, add to the catalog.
6. Fill each asset's Tier 1–4 values. Optional: rename `Primary Dmg & Crit` / `Secondary Dmg & Crit` → `… Crit`, `Stomp Seek` → `Stomp Power`, `Dash AOEDmg` → `Dash Push` (file renames only; nothing references them by name).

---

## Deliberately out of scope

- **Deflect / Secondary Fire Rate scripts** — kept, just uncatalogued.
- **`Apply` / `ApplyMaxed` / `Remove` no-ops** — still unused hooks; not touched.
- **Upgrade id format** — the sheet numbers upgrades 1–4 *within* a brand; `id` stays a unique lineage string.
- **Balance numbers** — defaults are placeholders.
