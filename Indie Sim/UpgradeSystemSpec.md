# Upgrade System — Technical Spec

This is the build spec for the Upgrade System. It supersedes the earlier "burn to activate" design — that model is retired. Read this fully before writing any code.

---

## Constraints for Claude Code — read this first

**Claude Code may only create and edit C# scripts.** Do not use Unity Editor automation to directly create, generate, or modify GameObjects, prefabs, ScriptableObject assets, or scenes — even if that capability is available. Where a GameObject, prefab, or asset needs to exist for a task to be complete, Claude Code should instead output a clear, numbered manual setup checklist (what to create, what fields to set, what to wire to what) for Sarbo to perform by hand in the Unity Editor.

---

## System overview — what's locked in

- Non-persistent, coins-only economy. Nothing survives death or a completed run.
- **Pack:** a 5-slot container of *active* upgrades. Buying a cig puts it directly into the pack, immediately active — no separate activation step.
- **No Unlockables.** The player starts the run already owning both weapons (primary: assault rifle / machine gun; secondary: shotgun). Every cig in the game modifies something the player already has — nothing is "unlocked."
- **No stacking of duplicate cigs.** Once a specific cig is bought, it's removed from the offer pool for the rest of the run.
- **Category 1 brand names** (Mild, Regular, Hard, Mint, Slims, Clove, Electric) are cosmetic/flavor only for now — no mechanical exclusivity rule between them. Ignore the earlier "one brand per slot" rule entirely; it does not apply to this version.
- **No Temporary/One-Level Boost cig type.** That behavior is now a property of Burn itself (see below) — every upgrade is a candidate for a one-level send-off, not just a special subset.
- **No upgrade-driven escalation.** Difficulty scaling comes only from dungeon level (the enemy-scaling hook the architecture refactor already reserves for Phase 7 / D3). Nothing here needs a "Heat" stat or any run-scoped escalation counter — that idea from the earlier pass is fully retired.

---

## Core loop

### Buying
- The shop opens after clearing a dungeon level. It has two tabs: **Buy** and **Burn**.
- Buy tab shows the current offer pool of cigs not yet purchased this run.
- No cap on number of purchases in one visit beyond (a) available coins and (b) pack capacity.
- If the pack is full (5/5), further purchases are blocked. The player must go to the Burn tab and free at least one slot before buying again.
- The moment a specific cig is bought, it's removed from the offer pool permanently for this run — it will not reappear.

### Burning
- Burn tab shows the cigs currently held in the pack.
- The player can burn any number of them, in any combination, any time the shop is open — not gated behind needing space. Burning is a deliberate choice, not just a forced fallback.
- Burning an active upgrade does two things:
  1. For the **next level only**, that upgrade's effect is computed at **max Tier** — whatever Rarity was actually bought stays as-is; only Tier gets maxed.
  2. After that level ends, the upgrade is **fully removed** from the pack — not reverted to its pre-burn state, just gone. The slot is now free.
- This is the only mechanism that frees a pack slot. There's no other discard action.

---

## Cig identity & replacement

Each of the catalog's base upgrades (assault rifle crit, stomp-seeks, dash-AoE, etc.) is one **lineage**. The pack holds at most one active instance per lineage at a time.

**Confirmed:** when the player buys a higher tier/rarity of a lineage they already own an active instance of, the new purchase **replaces** the existing instance in the same pack slot — it does not take a second slot, and it does not add on top of the old one. This is what makes "no stacking" consistent with a tier/rarity system — two different tiers of the same lineage active at once would itself be a form of stacking, just dressed up as two different cigs.

---

## Data model

### `CigData` — abstract base `ScriptableObject`, with 8 concrete subclasses
This is the piece that was left vague last pass — "a reference to an effect" doesn't say where you'd actually go to type in a number. Fixing that: `CigData` itself is an **abstract** ScriptableObject holding only the shared identity fields, and implementing `IUpgradeEffect` is left to each concrete subclass below it. There's no separate "effect object" to hunt down — the asset you create *is* the effect, and its Inspector shows identity fields and magnitude fields together in one view, because Unity draws inherited fields and a subclass's own fields in the same Inspector automatically.

Shared base fields (on the abstract `CigData`):
- `id` — unique string, also the lineage identity (one asset = one lineage)
- `displayName`
- `brand` — enum, cosmetic only: Mild / Regular / Hard / Mint / Slims / Clove / Electric
- `targetSlot` — enum: PrimaryWeapon / SecondaryWeapon / Stomp / Dash
- `hasTierRarity` — bool. False for the flat, one-off upgrades (e.g. "bullets bounce off enemies", "dash deflects projectiles")

8 concrete subclasses, each adding only the magnitude field(s) it actually needs, tagged with `[CreateAssetMenu]` so they show up individually in the Project window's Create menu:
- `CritChanceCigData` — a crit % field, per tier. Created twice as separate assets (Primary and Secondary each get their own instance, independently tunable) — 2 of the 10 total assets.
- `BulletBounceCigData` — a bounce-count field. Also created twice, one per weapon — 2 of the 10.
- `StompSeekCigData` — a range field and a damage field, per tier.
- `StompCircleCigData` — a damage field and a bullet-count field, per tier.
- `DashPostDamageCigData` — a damage % field per tier, plus the fixed 2-second duration.
- `DashAoECigData` — a damage field and a radius field, both per tier, plus a rarity bonus that boosts both. Ticks continuously every physics step while dashing (not once at dash-end); each enemy inside the radius takes damage once per dash and is otherwise just continuously pushed out. Distinct from the always-on, non-upgrade no-damage push that clears a small radius the instant every dash ends.
- `DashDeflectCigData` — no magnitude field at all, a pure behavior flag.
- `ChainDashCigData` — a charge-count field per tier.

8 subclasses, 10 asset instances (crit chance and bullet-bounce each instantiated twice, once per weapon). **This is where you'll actually input data:** create each asset via right-click → Create → [menu path Claude Code sets up] in the Project window, then fill in that asset's own Inspector — the tier-value array for whichever field(s) that upgrade owns, right there, nothing to cross-reference.

### `CigInstance` (runtime, not an asset)
The Tier and Rarity a specific offer or held cig actually has. Rolled when the shop generates an offer; this — not the `CigData` asset directly — is what lives in the offer pool, the pack, and `RunStats`.
- `cigData` — reference to the lineage's `CigData` asset
- `tier` — int 1–4, only meaningful if `hasTierRarity`
- `rarity` — enum Common / Uncommon / Rare / Epic, only meaningful if `hasTierRarity`

### Shared rarity lookup
One small, separate config asset (e.g. `RarityConfig`) — the **11th asset**, made once, not per-upgrade — mapping Rarity → bonus percentage: Common, Uncommon, Rare, Epic each get one field. Every `CigData` subclass's `Apply`/`ApplyMaxed` reads from this same asset rather than storing its own copy. This is the other place you'll input data, and you'll only ever do it once.

### `IUpgradeEffect` interface (implemented by each `CigData` subclass directly)
- `Apply(int tier, Rarity rarity)` — called the instant a cig is bought (immediately active, per the core loop above), using the actually-bought tier/rarity
- `ApplyMaxed(Rarity rarity)` — called when the cig is burned. Internally uses Tier = 4, with whichever Rarity was actually bought — valid for exactly one level
- `Remove()` — called when the level-boundary signal fires after a burn. Fully removes the effect — this is not a revert-to-previous-state, since the pre-burn version is also gone

A flat cig (`hasTierRarity = false`) just has `ApplyMaxed()` call `Apply()` with the same fixed values, since there's no higher power level to jump to.

### Generalized formula (mechanic, not a balance number)
The same additive-plus-rarity-multiplier shape applies to *whichever* numeric field an effect owns — not only damage:
```
FinalValue = BaseValue + UpgradedValue + (RarityBonus × UpgradedValue)
```
- `BaseValue` — owned by the thing being upgraded, not the cig. For weapon damage specifically, this lives on the weapon script itself (`PlayerConeShooter`), read at the moment of calculation — not duplicated onto `CigData`. For a field with no natural pre-upgrade baseline (e.g. crit chance), `BaseValue` is simply 0.
- `UpgradedValue` — the effect's own per-tier bonus for that specific field (design-authored per tier, per effect; values TBD).
- `RarityBonus` — pulled from the shared rarity lookup above, not authored per-cig.
- On `ApplyMaxed()`, `UpgradedValue` is looked up at Tier = 4 regardless of what was actually bought; `RarityBonus` stays whatever was actually bought.

Flagging one assumption here rather than deciding it silently: this treats every effect's field symmetrically under the rarity multiplier, including non-damage ones like range and charge count. That's a reasonable default, not a directive — say so if you want rarity to only matter for damage-flavored effects and just add flat bonuses elsewhere.

### `RunStats` addition
- A held-`CigInstance` pack list (max 5), backed by `GameSession.CurrentRun` as with every other run-scoped system in the refactor. No new escalation field needed.

---

## Manager decomposition

Split into four pieces, not one god object — same standard already applied to critiquing the *old* `UpgradeManager` earlier in this design process:

- **CigPool** — owns the offer pool, tracks what's been bought (removed permanently), generates the Buy tab's current offer set.
- **Pack** — owns the up-to-5 active `CigInstance` entries, enforces the replace-in-place rule, handles add/remove.
- **BurnResolver** — handles the burn action: invokes `ApplyMaxed()`, registers the pending `Remove()` against the level-boundary signal.
- **ShopUIController** — pure UI glue between the three above and the two-tab (Buy/Burn) interface. Owns no gameplay state itself.

---

## Sequencing note — level-boundary signal, don't block on Phase 7

`BurnResolver` needs to know when "the next level" has ended, to call `Remove()` on anything that was burned. The architecture refactor's Phase 7 event bus already plans an `OnDungeonCleared` signal for exactly this kind of boundary — but Phase 7 hasn't started and is the highest-risk phase in that plan. **Don't wait on it.** Build one minimal, direct signal now (a plain C# event or a callback the existing round-end code already fires) that `BurnResolver` subscribes to. When Phase 7 eventually formalizes the full event bus, it can absorb this one signal without `BurnResolver` needing to change — it only needs *a* signal firing at the right time, not a specific dispatch mechanism.

---

## Upgrade catalog — categorized

### Primary weapon (assault rifle / machine gun)
- Increased crit chance — tiers + rarities
- Bullets bounce off enemies — flat, no tiers/rarities

### Secondary weapon (shotgun)
- Increased crit chance — tiers + rarities
- Bullets bounce off enemies — flat, no tiers/rarities

### Stomp
- Seeks enemies in a range instead of only hitting directly under the player — tiers + rarities (range, damage)
- Increased damage + releases a circle of bullets around the player — tiers + rarities (damage, bullet count)

### Dash
- Deal more damage for 2 seconds after dashing (player tints red while active) — tiers + rarities (damage %, visual)
- A radius around the player continuously pushes enemies away and damages each one once per dash, for the dash's duration — tiers + rarities (damage, radius)
- Deflects projectiles the player dashes into — flat, no tiers/rarities
- Chain dash — more dashes available per activation — flat, no tiers/rarities

Separately, as a base-kit (non-upgrade) behavior: the player is invulnerable and passes through enemies for the dash's duration, and a small radius pushes enemies out (no damage) the instant the dash ends.

**10 catalog entries total: 6 tiered lineages, 4 flat.** (Bullets-bounce counts as two separate entries — one per weapon — since it's a distinct purchase and a distinct pack slot on each.)

---

No open questions remain. This spec is ready for Step 1.

---

## Manual setup — preview

Claude Code should hand these off as instructions rather than create them directly:
- 10 `CigData` subclass asset instances (one per lineage — 2 each for `CritChanceCigData` and `BulletBounceCigData`, 1 each for the remaining 6 subclasses). Created via right-click → Create → [menu path] in the Project window, then filled in directly on each asset's own Inspector — identity fields and that upgrade's own tier-value array together in one place.
- 1 `RarityConfig` asset — the 11th asset, made once, holding the Common/Uncommon/Rare/Epic bonus percentages every `CigData` subclass reads from.
- A Pack manager GameObject/prefab in the relevant scene(s), holding the `Pack`, `CigPool`, and `BurnResolver` components
- A Shop UI prefab with the two-tab (Buy/Burn) layout, wired to `ShopUIController`
