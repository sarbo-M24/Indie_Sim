# DemoBeforeIGDC

**Hard deadline:** playable demo LIVE on Steam before **Oct 28, 2026** (IGDC day 1). Today is Sept 16 — **42 days out.**

**Steamworks status:** account created and approved. A Steam application (App ID) for this game still needs to be created. The store page does not exist yet at all — no copy, no screenshots, no capsule art, no trailer.

---

## ⚠️ Read this part first — Steam's own process is the real deadline risk

This isn't a "do it whenever" backlog item. Valve's own rules create a hard, dated critical path independent of how done the game is:

- A store page, once approved, must sit in **"Coming Soon" status for a minimum of 2 weeks** before you're allowed to release. Counting back from Oct 28, that means the page needs to be **approved and posted as Coming Soon by ~Oct 14.**
- Store page review typically takes 3–5 business days, but Valve's own guidance is to submit **at least 7 business days** before you want it live, to leave room for a requested-changes round. Working back from Oct 14, that means the page should be **submitted for review by roughly Sept 30 – Oct 3.** That's about **2 weeks from today, not 5.**
- Separately, the **build itself** also needs review (also typically 3–5 business days, same 7-day-buffer guidance), and the store page must be submitted before the build can be. This can run inside the 2-week Coming Soon window rather than stacking after it — so it's not fully additive, but it's not free either.
- Store page screenshots must be **real gameplay**, minimum 5, 1920×1080, 16:9 — no placeholders, no mockups. That means there needs to be a presentable, working build **before** the store page can be submitted, which is one more reason the Upgrade System build below needs to land early, not stretch to fill the whole runway.
- Real-world caveat worth knowing: review isn't always 3–5 days. There are documented cases of "extended review" (e.g. failed automated tests) with no committed timeline from Valve. Build in slack; don't plan against the optimistic number.
- **Resolved:** the 30-day Steam Direct fee waiting period is tied to when the $100 was *paid*, not to when the App ID is actually created — paying it grants an account credit that gets activated later, with no new clock or payment on activation. Since the fee was paid months ago at account setup, that clock has long since run out. Worth a 30-second glance at the Steamworks dashboard to confirm the app credit is sitting there available before creating the application, but this is not expected to be a blocker.

**Bottom line:** the store page (copy, art, at least 5 real screenshots) needs to be submittable within the next ~2 weeks. That's not a parallel side-task to fit in whenever — it's the tightest deadline in this whole plan.

---

## Scope for this doc

This doc covers the **Upgrade System** — design (finalized below) and a build plan for it. UI, sound, animation polish, and boss variants are real work but explicitly deprioritized until the upgrade system is functional — tracked at the bottom, not detailed yet.

---

## Refactor plan — what changes given the deadline

**Phase 7 (event bus, unified enemy death) is deferred to post-demo.** Reasoning:
- It was never a hard dependency for the Upgrade System — `UpgradeSystemSpec` was deliberately designed to use a minimal standalone level-boundary signal instead of waiting on Phase 7's full `GameEvents` bus, specifically so this wouldn't be a blocker.
- The P0 bugs that partly motivated Phase 7 ("no unified enemy death/cleanup path") are already closed — per the Progress Report, they were fixed by other means this session, not by the architectural cleanup Phase 7 was scoped to do.
- What's left in Phase 7 is real, worthwhile technical debt (`Enemy.cs` reaching directly into `EnemyKillTracker.Instance`, etc.) — but it's debt, not a blocker. With ~30 engine-days left and a new feature (the Upgrade System) still to build from scratch, this isn't the time for a pure architecture-quality pass, however worthwhile it is long-term. Revisit it after IGDC, not instead of the demo.

**The Upgrade System takes Phase 7's place in the active work sequence**, not after it — it doesn't need to wait, and there isn't runway to do both.

**Before Step 1 of the Upgrade System (see UpgradeSystemSpec), audit two existing classes that Phase 6 already touched:**
- `UpgradeManager` — already de-persisted in Phase 6, reading/writing `GameSession.CurrentRun`. The new Pack/CigPool/BurnResolver design almost certainly replaces this outright rather than extending it — confirm what it currently does before writing anything new, so you don't end up with two competing "upgrade" systems in the codebase.
- `WeaponUnlockManager` — also de-persisted in Phase 6, built around weapons being unlocked mid-run. Since the new design has no Unlockables (player starts owning both weapons), this manager may now be dead code. Worth confirming and likely removing rather than building the new system alongside it.

**One Known Issue from the Progress Report is still worth fixing in this window, independent of everything above:** the ammo UI not displaying in `BossArena`. It's a confirmed bug, not speculative, and `BossArena` needs to be presentable for Steam screenshots anyway. The startup lag and the shared-canvas debt are both fine to leave as-is — neither is confirmed to matter for the demo.

**Small thing to note, not a decision needed now:** the Upgrade System's shop will call `CoinManager.Instance` directly to check/deduct coin balance. That's consistent with how the rest of the pre-Phase-7 codebase already works — it becomes one more call site for Phase 7 to eventually convert to events, not something to solve now.

**Phase 8 (final verification pass) still makes sense before build-lock** — just scope it to cover the Upgrade System too, not only the original refactor phases. It's a natural fit for the Oct 10–16 fix-it buffer already in the timeline below.

---

## Upgrade System — Design

Full design + implementation spec now lives in a dedicated doc: **UpgradeSystemSpec** (linked separately). Short version of what changed from the first pass: no separate "burn to activate" step (buying is immediately active), no Unlockables (player starts with both weapons, everything is Upgradable-only), Category 1 brand names are cosmetic only for now, no Temporary Boost cig type (that behavior folded into the general Burn mechanic — burning an active upgrade maxes it out for one more level, then removes it), and difficulty escalation comes purely from dungeon level, not from upgrades. A few content-level questions are still open there pending answers — see that doc.
---

## Build plan for the Upgrade System

Same phase structure as `architecture-refactor-plan-v3.md` — tasks, acceptance criteria, checkpoint — since that's the working pattern already.

### Step 0 — Audit before building
**Tasks:** review `UpgradeManager` and `WeaponUnlockManager` (both touched in Phase 6) to confirm the new system replaces `UpgradeManager` outright and that `WeaponUnlockManager` is now dead code given no Unlockables. Report findings before writing new code.
**Acceptance:** a clear statement of what gets removed/replaced vs. kept, confirmed before Step 1 starts.

### Step 1 — Data model & Effect interface
**Tasks:** Effect interface (Apply / MaxOverride / Remove — see UpgradeSystemSpec for the burn lifecycle). `CigData` ScriptableObject (id, name, brand [cosmetic], target slot, tier/rarity fields where applicable, description, Effect reference). Add a held-cigs/pack list to `RunStats`.
**Acceptance:** one hand-written test cig exists as an SO asset, applies immediately on a debug "buy" call, and correctly maxes-then-removes on a debug "burn" call.
**Model:** judgment-heavy — plan with Opus (`opusplan`), execute with Sonnet, matching how Phases 6–7 of the refactor are already being run.

### Step 2 — Pack/Container manager
**Tasks:** scene-local manager owning the 5-slot pack, backed by `GameSession.CurrentRun`. Buy (instant-active), burn (any number, any time the shop is open), buy-blocked-when-full-until-a-slot-is-freed.
**Acceptance:** buying to 5/5 blocks further purchases until something is burned; burning frees the slot and the freed upgrade's max-for-one-level-then-remove behavior fires correctly on the next level.

### Step 3 — Content: the upgrade catalog
**Tasks:** author the full catalog (see UpgradeSystemSpec) as `CigData` assets using Step 1's interface, once the open content questions there are resolved. Repetitive once the pattern exists — good candidate for plain Sonnet execution rather than Opus.
**Acceptance:** every catalog entry buys, appears active immediately, and burns (max-then-remove) correctly.

### Step 4 — Shop UI hookup
**Tasks:** post-round menu with two tabs (Buy, Burn) per UpgradeSystemSpec. Functional, not final art — real UI polish is backlog.
**Acceptance:** full loop playable: clear a round → shop → buy and/or burn → effect visible next round → repeat to death.

### Step 5 — Level-boundary signal
**Tasks:** the minimal signal described in UpgradeSystemSpec's sequencing note — needed so a burned upgrade knows when "the next level" has ended and it should be removed.
**Acceptance:** burn an upgrade, clear the next level, confirm it's gone from the pack and its effect is reverted to nothing (not to its pre-burn state).

**Checkpoint after Step 5:** playable end-to-end with real content. Everything past this is backlog, not upgrade-system work.

---

## Backlog — tracked, not detailed yet
- UI (shop presentation, pack/container HUD)
- Sound (pick/burn SFX, ambient)
- UI / player animations
- Boss variants (reuse existing boss, vary logic slightly per variant)


## ⚠️ Availability constraint — this changes the runway, not just the end date

Mumbai event Oct 17–18, then visiting friends afterward with little to no expected work time. **Oct 16 is the last working day on the engine.** That's not a soft target — treat it as the day all code changes stop.

This cuts the real dev runway from 42 days to **30 days** (Sept 17 → Oct 16), and — more importantly — it removes the "week 6 buffer" the original plan assumed. There is no engine-accessible time between Oct 16 and Oct 28. Any risk buffer has to live *inside* the 30 days, not after them.

One thing this does **not** eliminate: releasing on Steam still requires manually clicking "Release App" — Valve does not auto-publish on a chosen date. That click needs zero engine access, just a Steamworks login, so it can technically happen from a phone during the trip if it has to. But it does need to actually happen, so it needs a real plan, not "I'll get to it" — see below.

---

## Steam critical path (see warning at top)
- Create the Steam application (App ID) — do this immediately; the $100 fee is already paid (months ago, at account setup) and the resulting app credit just needs activating.
- 30-day waiting period: already satisfied, since it runs from the payment date, not from App ID creation.
- Store page copy, capsule art, and at least 5 real gameplay screenshots — needs a presentable build, so depends on Step 4 above landing early. **This timeline doesn't move** — it was already independent of the Oct 16 constraint.
- Submit store page for review by **~Sept 30–Oct 3** to have realistic room for a changes round before the Oct 14 Coming Soon deadline.
- Submit the final build for its own review by **~Oct 8–10** — earlier than the original plan — so there's still engine time left to fix anything the review flags before Oct 16.
- **Best case:** both the 2-week Coming Soon window and build review clear by ~Oct 15–16, and you click Release before leaving for Mumbai. Demo is live, nothing left to do remotely.
- **Fallback, if either review runs long:** the Release click has to happen remotely, sometime Oct 17–28. Put an actual calendar reminder on this now — it's a 5-minute action, but it's also the one step that can silently blow the hard deadline if it's left to memory while you're traveling.

---

## Rough timeline (working draft — adjust as we go)

- **Sept 17 – ~Oct 3 (Weeks 1–2):** Upgrade System Steps 1–5. Biggest open programming unknown, and also what makes real screenshots possible — nothing else here shifts because of the travel constraint.
- **In parallel, starting now:** Steam App ID creation, store page copy/art drafting.
- **~Sept 30–Oct 3:** submit store page for review.
- **~Oct 3–10 (about a week):** backlog — UI, sound, animations, boss variants — plus playtesting. **This window shrank from ~18 days to ~7, which is the real casualty of the compressed schedule.** Worth deciding now which of those four are actually demo-critical versus safe to cut or simplify — trying to fully do all four in a week on top of everything else isn't realistic. Flagging this now so it's a decision, not a surprise on Oct 8.
- **~Oct 8–10:** feature-complete build locked and submitted for build review.
- **~Oct 14:** store page approved, posted as Coming Soon (unchanged, backward-calculated from Valve's 2-week rule).
- **Oct 10–16:** fix-it buffer for anything build review flags — the only buffer that exists in this plan, since nothing exists after the 16th. Release, ideally, lands in this window.
- **Oct 17–18:** Mumbai event.
- **Oct 19–27:** visiting friends — treat as no engine access. Release click happens here only as the fallback, per the reminder above.
- **Oct 28:** absolute hard deadline. Demo should already have been live for days by this point, not cutting it live on the day itself.

