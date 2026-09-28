# Save System Spec — 1BK

Scope: slot-based save system for 1BK. This replaces the SaveSystem portion of **Phase 4** in `architecture-refactor-plan.md`.

## Ground rules for this session

- **One sub-phase per commit** (4A, 4B, 4C, 4D). Stop after each one for human review.
- **Stop and ask** on any decision this spec does not cover. Do not invent behaviour.
- **Audit before you implement.** Sub-phase 4A starts with a written audit report. Wait for approval before writing code.
- **Stay in scope.** Do not refactor systems outside this spec. If something blocks you, report it.
- Respect the existing lifetime scopes:
  - Persistent: survives across launches.
  - Run: survives scene loads within one run.
  - Level: destroyed and recreated with each level.
- Persistent objects own no run-scoped mutable state.

---

## 1. Requirements

### Slots and menu flow

- There are **3 save slots**.
- The main menu has a **Start** button and a **Continue** button:
  - **Start** opens slot select. The slot the player picks becomes the **active slot** for the whole session.
  - **Continue** resumes the **most recently played slot that has an active run**, without going through slot select. It is **hidden** when no slot has an active run.
  - Continue works out its target from the slot headers' last-written timestamps. There is no stored "last slot" pointer, because a pointer can go stale after deaths, deletes or corruption, while the timestamps can't.
  - Corrupted slots and slots with no active run are never Continue targets. For example: if the latest slot's run ended in death, Continue picks the next most recent slot that still has an active run.
- Picking an empty slot prompts for a **slot name**, then starts a new run.
- Each slot tile shows:
  - the slot name
  - current coins
  - current dungeon number
  - last played time
  - an empty or no-active-run state where applicable
- Picking a slot with an active run resumes that run. Picking a named slot with no active run starts a new run in that slot.
- Slot select offers **Delete slot**, with a confirmation. There is no overwrite path: to start over in a slot, the player deletes it.
- **Save & Exit** always writes to the active slot. The player is never asked to choose a slot at exit.

### What gets saved (current)

| Data | Scope | Notes |
|---|---|---|
| Slot name, created/last-played timestamps | Slot | Survives death |
| Current dungeon number and resume point | Run | See Section 4 |
| Coins | Run | Coins are also HP, so no separate HP field is saved |
| Upgrades owned (including stack counts or levels, if they exist) | Run | Saved by stable ID |
| Relics owned | Run | Saved by stable ID |
| Weapon unlocks | Run | Saved by stable ID |
| Achievements | Global | In the global profile file, not in slots |
| Dev panel unlocked (demo) | Global | Section `global.demo`. Set when the final boss is defeated. See Section 7. |
| Audio/video settings | — | **Stay in PlayerPrefs.** Out of scope. |

The following are **not saved**:
- The dungeon layout. A fresh seed is rolled on every resume.
- Enemy state.
- Anything picked up or earned mid-level.
- Ammo. The reserve is infinite for now.

### Death

- Final death **deletes all Run-scope data** from the active slot. The slot keeps its name and any Slot-scope data.
- The wipe must be written **at the moment death is finalised, before the death screen is shown**. This stops a player from quitting on the death screen to keep the run.
- **Extra-life hook:** the save system reacts only to a *finalised* run end. A future extra-life system must be able to intercept death before it is finalised. Death finalisation should therefore be one call site that other systems can veto or defer. Build the hook point only. Do not build an extra-life feature.

### Victory (defeating the final boss)

- Victory is a run end, just like death. It goes through the **same run-end finalisation point**, with reason "Victory".
- Write order matters:
  1. Set the dev panel unlock in `profile.json` and write it.
  2. Wipe the slot's Run-scope data and write the slot.
  3. Show the existing "Demo Complete" screen.
- This order means a crash between the writes leaves the player unlocked with a boss run still resumable. That is harmless. The reverse order could lose the unlock, which is not.

---

## 2. Architecture

### Components

**SaveService**
- A plain class (not a MonoBehaviour), created by the Boot composition root and owned by `GameSession` in Persistent scope.
- It is responsible for:
  - slot file I/O
  - the file envelope
  - format versioning
  - atomic writes
  - backup recovery
  - the section registry
- It has **no knowledge of game data** such as coins, upgrades or relics.

**Save sections** are the extension point for everything that gets saved. Each section declares:
- a **stable string key**, for example `run.economy`, `run.upgrades`, `run.relics`, `run.weapons`, `run.progress`, `slot.meta` or `global.achievements`
- a **scope**: Global (lives in the profile file), Slot (survives death) or Run (wiped on death)
- a **version** integer
- a **capture** step that reads from `GameSession`, never from scene managers, and produces a DTO
- a **restore** step that writes a DTO back into `GameSession`
- a **default** step that produces the value to use when the section is missing from the file
- a **migrate** step that upgrades a payload from an older section version

Sections are registered once, in the Boot composition root. **Adding new saved data later means adding one new section and one registration line, with no change to SaveService.** Future meta-progression will simply be a Slot-scope or Global-scope section.

**DTOs are separate from runtime classes.**
- Never serialize `GameSession`, `CurrentRun` or any runtime POCO directly.
- Each section maps runtime data to its own DTO and back. The DTO is the on-disk contract, so the runtime model can be refactored freely behind it.

**The source of truth is `GameSession`.**
- Capture reads from `GameSession.CurrentRun` and related data.
- Capture must work while the player is in the store, when level managers may not exist.
- If any saved data currently lives only inside a manager, report it during the audit. Do not work around it.

**Content catalogs.** Upgrades, relics and weapons are saved as **stable string IDs**, never as asset references or list indices.
- If these ScriptableObjects don't already have an ID field, add one, plus an editor validation step that flags duplicate or empty IDs.
- A catalog per content type resolves an ID back to its asset.
- On restore, an unknown ID (for example, content that was removed) is skipped with a warning log. It is never treated as a crash.

### Files

The save folder is `Application.persistentDataPath/saves/`, containing:
- `slot_0.json`, `slot_1.json`, `slot_2.json`
- `profile.json` for Global-scope sections
- a `.tmp` and a `.bak` sibling per file

**Envelope** (the same structure for slots and profile):
- a **header** containing:
  - the save format version
  - the slot index
  - created and last-written timestamps
  - a **summary block** (slot name, coins, dungeon number, has-active-run flag) that the slot-select UI reads without restoring any sections. SaveService fills the summary from the relevant sections at write time.
- a **sections** map from section key to `{ version, payload }`

**Serialization:** use Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`). If the package isn't installed, add it. Do not use JsonUtility, which can't handle the dictionary or the nested payloads.

### Robustness rules

- **Atomic write:**
  1. Serialize to `.tmp`.
  2. Move the current file to `.bak`.
  3. Rename `.tmp` into place.
- **Load order:** try the main file, then `.bak`. If both fail, the slot is shown as **Corrupted** in the UI, with Delete as the only option. Never silently reset a slot.
- **Missing section** on load → apply its default. This is how old saves survive when new sections are added.
- **Unknown section** in a file (no registered handler) → ignore it, and preserve it on the next write.
- **Section version older than the code** → run its migrate step. **Newer than the code** → treat the slot as corrupted and log it clearly.
- **No encryption, obfuscation or checksums.** This is a single-player game.
- Do not rely on `OnApplicationQuit` for saving. All saves happen at the explicit save points in Section 4.

### Editor testability

- When a level scene is played directly in the editor (through the SceneBootstrapGuard path), there is no chosen slot. In that case SaveService uses a **transient debug slot that never writes to disk**.
- Add editor menu items for:
  - opening the save folder
  - dumping a slot's JSON to the console
  - deleting all saves

---

## 3. Slot states

| State | Meaning | Selecting it |
|---|---|---|
| Empty | No file exists | Name prompt, then new run |
| Named, no run | Slot data exists, Run data is empty (after death) | New run in this slot |
| Active run | Run data exists | Resume the run (Section 4) |
| Corrupted | Main file and backup both failed to load | Only Delete is available |

---

## 4. Save points and resume

A **resume point** is saved in the `run.progress` section alongside the dungeon number. There are two values:
- **Store:** the player was in the store after clearing dungeon N.
- **LevelStart:** the player should start dungeon N fresh.

| Trigger | Write | Resume point stored |
|---|---|---|
| New run started | Full slot write | LevelStart(1) |
| Player enters the store after clearing a level | Autosave | Store |
| Store → **Save & Exit** | Full write, then return to the main menu | Store |
| Store → **Continue** (leaving the store) | Checkpoint write | LevelStart(next dungeon) |
| Mid-level quit (pause menu) | **No write** | Unchanged: the last checkpoint stands |
| Final death | Wipe Run-scope data, write | None |
| Final boss defeated | Write the unlock to the profile, then wipe Run-scope data and write the slot | None |

**Resume behaviour:**
- **Store:** load the store with the saved coins, upgrades, relics and weapons. Buying some upgrades, quitting and returning to buy more is intended behaviour. The store has no rolled stock to protect, so it needs no extra section.
- **LevelStart(N):**
  1. Restore sections into `CurrentRun`.
  2. Roll a new seed and generate a fresh dungeon at depth N.
  3. Load the scene and rehydrate the player from `CurrentRun`.
  
  If depth N maps to the BossArena, route there using the existing progression and BossDefinition logic. **Do not add new routing rules.**

**Mid-level quit UX:** the pause-menu Quit option shows a warning that progress since the last checkpoint will be lost, then returns to the main menu. Alt-F4 behaves the same way.

**Known and accepted:** a player about to die mid-level can force-quit and retry from the checkpoint. This is single-player, so no countermeasure will be built.

---

## 5. Sub-phases

### 4A — Audit and infrastructure

**Audit report first.** Stop and wait for approval after writing it. The report must cover:
- Where each saved field currently lives (coins, upgrades and any stack levels, relics, weapon unlocks, dungeon number), and whether `GameSession.CurrentRun` owns it.
- Whether the upgrade, relic and weapon ScriptableObjects have stable IDs.
- Every existing PlayerPrefs key, and what it stores.
- Whether the Newtonsoft package is present.
- Where store entry, store exit, level start and death currently happen in code.

**Then implement:**
- SaveService
- the envelope
- atomic I/O with backup recovery
- the section registry
- scopes
- versioning and migration plumbing
- the debug slot
- the editor menu items

**Acceptance criteria:**
- A test section round-trips through a file.
- Deleting the main file recovers from `.bak`.
- A garbage file marks the slot Corrupted.
- An unknown section is preserved across a write.

### 4B — Sections and catalogs

**Implement:**
- The sections: `slot.meta`, `run.progress`, `run.economy`, `run.upgrades`, `run.relics`, `run.weapons` and `global.achievements`.
- Content IDs, the editor duplicate-ID validation and the catalogs.
- Moving achievements out of PlayerPrefs into `profile.json`. **No migration:** delete the old PlayerPrefs save keys once. Leave the settings keys alone.

**Acceptance criteria:**
- A capture-then-restore of a populated `CurrentRun` produces identical state.
- An unknown content ID is skipped with a warning.
- Removing a section from the file loads its defaults.

### 4C — Main menu and slot-select flow

**Implement:**
- Main menu **Start** → slot select with 3 tiles showing the header summary.
- Main menu **Continue**, targeted and shown or hidden as described in Section 1.
- The name prompt for empty slots.
- Delete, with confirmation.
- Correct rendering of all four states in Section 3.
- Setting the active slot in `GameSession`.

**Acceptance criteria:**
- The tiles read headers only; no sections are restored.
- The active slot persists through the session.
- **Continue visibility:** Continue is hidden on a fresh install, and after every slot's run has ended.
- **Continue targeting:** with two active slots, Continue resumes whichever one was written most recently.
- **Continue skips dead slots:** if the latest slot's run died, Continue resumes the other active slot.

### 4D — Save points and resume routing

**Sequencing:** if Phase 5 (the run lifecycle funnel) is not yet complete, **stop and report instead of implementing 4D**. These save points belong in the funnel, and wiring them into scattered call sites now creates rework. If the funnel exists, implement the Section 4 table there, including:
- the single death-finalisation point with the extra-life hook
- the mid-level quit warning
- the Store Continue / Save & Exit options

**Acceptance criteria:**
- **Store:** Save & Exit in the store, relaunch, pick the slot → you are in the store with identical coins, upgrades, relics and weapons.
- **Level:** leave the store, collect coins, quit mid-level, relaunch → you start the same dungeon number on a *different* layout, with the coins you had on leaving the store.
- **Death:** die, relaunch → the slot shows its name and no active run, and selecting it starts a new run.
- **Death screen:** quitting on the death screen does not preserve the run.
- **Crash in store:** killing the process in the store after clearing a level resumes you in the store.
- **Victory:** defeat the final boss, relaunch → the slot has no active run and `profile.json` has the unlock set.

### 4E — Gate the existing dev panel for players

Depends on 4D (the victory unlock). Behaviour is defined in Section 7. **The Section 7 feature audit comes first. Stop after it.**

**Acceptance criteria:**
- **Locked:** in a player build before the unlock, the panel is unreachable by any route, including keybinds.
- **Unlocked:** in a player build after the unlock, the panel opens from the pause menu and shows only the approved controls.
- **Real grants:** upgrades granted through the panel show exactly the same effects as bought ones.
- **Grants persist:** grants survive a Save & Exit and resume.
- **Dev builds unchanged:** the Editor and development builds behave exactly as before.
- **Removable:** turning the `DEMO_BUILD` flag off removes player access with no errors.

---

## 7. Demo dev panel (existing panel, player-gated)

We are not building a new panel. The **existing dev panel** is made available to players after they finish the game once.

- **Unlock:** the panel unlocks when the final boss is defeated. It is **global**, stored in `global.demo` in `profile.json`, so it applies to all slots, both current and future.
- **Gating:**
  - In the Editor and in development builds, the panel keeps behaving exactly as it does today.
  - In player (release) builds, the panel is unreachable until the unlock is set. After that, it opens from a **pause menu button**. Any existing keybind may stay as a secondary route, but only if the keybind is also gated.
- **Feature audit, required before any code.** List every control in the existing panel and classify it:
  - **Player-safe:** grants that go through the real acquisition path (the same path as a store purchase or pickup), so their effects apply and they persist as normal run state.
  - **Dev-only:** anything that bypasses the run lifecycle funnel, such as level skips, direct scene loads, god mode, raw list edits, or save or state manipulation.
  - **Broken for players:** controls that directly mutate lists or bypass acquisition. These are *player-facing* bugs once exposed, because effects won't apply or the save state will be inconsistent.

  Stop and report the audit. Wait for a decision on which controls players get.
- **Hiding controls:** in player builds, dev-only controls are hidden, not merely disabled.
- **Save interaction:** anything a player grants through the panel is ordinary run state. It persists at the next save point, and nothing extra is saved.
- **Demo-only:** player access sits behind one build define, `DEMO_BUILD`, so it can be stripped from the full release. The `global.demo` section stays registered either way, so saves remain compatible.

---

## 8. Out of scope

- Cloud saves. Steam Auto-Cloud can later point at the saves folder through Steamworks config; it needs no code.
- Meta-progression content. The Slot and Global scopes exist so it can be added later as sections.
- The extra-life feature. Only the veto hook is built.
- Settings persistence changes.
- Save encryption.
