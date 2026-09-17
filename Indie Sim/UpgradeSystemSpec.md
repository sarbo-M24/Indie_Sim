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

### `CigData` (ScriptableObject)
- `id` — unique string
- `displayName`
- `lineageId` — identifies which of the catalog's base upgrades this belongs to (used to enforce the replace-in-place rule above)
- `brand` — enum, cosmetic only: Mild / Regular / Hard / Mint / Slims / Clove / Electric
- `targetSlot` — enum: PrimaryWeapon / SecondaryWeapon / Stomp / Dash
- `hasTierRarity` — bool. False for the flat, one-off upgrades (e.g. "bullets bounce off enemies")
- `tier` — int 1–4, only meaningful if `hasTierRarity`
- `rarity` — enum Common / Uncommon / Rare / Epic, only meaningful if `hasTierRarity`
- `baseDamage`, `upgradedDamage`, `rarityBonus` — numeric fields feeding the formula below (values are design/balancing work, not filled in here)
- `effect` — reference to an `IUpgradeEffect`

### `IUpgradeEffect` interface
- `Apply()` — called the instant a cig is bought (immediately active, per the core loop above)
- `ApplyMaxed()` — called when the cig is burned. Computes the effect at max Tier (4), using whatever Rarity was actually bought — valid for exactly one level.
- `Remove()` — called when the level-boundary signal fires after a burn. Fully removes the effect — this is not a revert-to-previous-state, since the pre-burn version is also gone.

Three lifecycle methods, one interface, every cig (tiered or flat) implements the same shape. A flat cig with `hasTierRarity = false` just has `ApplyMaxed()` do the same thing as `Apply()`, since there's no higher power level to jump to.

### Damage formula (mechanic, not a balance number)
```
FinalDamage = BaseDamage + UpgradedDamage + (RarityBonus × UpgradedDamage)
```
- `BaseDamage` — the weapon/ability's inherent value, untouched by upgrades.
- `UpgradedDamage` — flat bonus from the cig's Tier (design-authored per tier; values TBD, not balancing work for this pass).
- `RarityBonus` — percentage multiplier from Rarity (design-authored per rarity, e.g. Common likely = 0%; values TBD).
- On `ApplyMaxed()`, this same formula evaluates using Tier = 4 (max), with `RarityBonus` unchanged from whatever was actually bought.

### `RunStats` addition
- A held-cigs/pack list (max 5), backed by `GameSession.CurrentRun` as with every other run-scoped system in the refactor. No new escalation field needed.

---

## Manager decomposition

Split into four pieces, not one god object — same standard already applied to critiquing the *old* `UpgradeManager` earlier in this design process:

- **CigPool** — owns the offer pool, tracks what's been bought (removed permanently), generates the Buy tab's current offer set.
- **Pack** — owns the up-to-5 active `CigData` instances, enforces the replace-in-place rule, handles add/remove.
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
- Damage in an area after the dash completes; dash distance = the circle's diameter — flat, no tiers/rarities
- Deflects projectiles the player dashes into — flat, no tiers/rarities
- Chain dash — more dashes available per activation — flat, no tiers/rarities

**10 catalog entries total: 5 tiered lineages, 5 flat.** (Bullets-bounce counts as two separate entries — one per weapon — since it's a distinct purchase and a distinct pack slot on each.)

---

No open questions remain. This spec is ready for Step 1.

---

## Manual setup — preview

Claude Code should hand these off as instructions rather than create them directly:
- 10 `CigData` ScriptableObject assets, one per catalog entry above (whether tier/rarity variants of the same lineage are separate assets or computed at runtime from a single asset — Claude Code should propose this and ask rather than assume)
- A Pack manager GameObject/prefab in the relevant scene(s), holding the `Pack`, `CigPool`, and `BurnResolver` components
- A Shop UI prefab with the two-tab (Buy/Burn) layout, wired to `ShopUIController`
