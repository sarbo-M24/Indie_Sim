# Plan — Virtual Keyboard, Give Up, Store Save & Exit, Spawner Placement, Steam Notes

Status: **implemented 2026-09-29** (compile-checked; awaiting Play-mode test).
Decisions: Give Up = instant new run (no death screen) · store shows only
Save & Exit · generator takes a list of spawner prefabs.
Scene/UI edits go through one-click editor builders (same pattern as
`SlotSelectUIBuilder` / `SavePointUIBuilder`) so no scene YAML is hand-edited.
I only do compile checks; you test in Play mode.

---

## 1. Virtual keyboard (gamepad only) — slot name prompt

**Now:** `NamePromptDialog` pre-fills "Slot N" and a pad player lands on
Confirm, so they can't type a name.

**Change:**
- New `VirtualKeyboard` component (`Scripts/UI/SlotSelect/VirtualKeyboard.cs`):
  a grid of key buttons (A–Z, 0–9, space, `-`, `_`), plus **Shift**, **Backspace**,
  **Clear** and **Done**. It edits the dialog's `TMP_InputField` text directly
  and respects a max length (16 chars).
- Shown only when `InputManager.UsingGamepad` is true while the prompt is
  open. Mouse/keyboard players keep the normal typed field. If the player
  switches device while the prompt is open, the keyboard shows or hides to
  match.
- Pad shortcuts while it's open: **X / Square = Backspace**, **Y / Triangle = Space**,
  **Start = Done**, **B / Circle = cancel the prompt** (as it does now).
  D-pad/left stick move across keys, with explicit wrap-around navigation.
- Focus: the first key when the prompt opens with a pad. `SlotSelectPanel.Update`
  already calls `UIFocus.EnsureSelection` for the prompt, so it will point at
  the keyboard.
- Built by a new menu item, **Tools/Save/Build Virtual Keyboard**. It adds the
  keyboard under the existing Name Prompt in `Main menu.unity` and wires the
  reference. It's safe to re-run.

Files: `NamePromptDialog.cs`, new `VirtualKeyboard.cs`, `SlotSelectUIBuilder.cs`
(or a small new builder).

## 2. Give Up (pause menu) — restart the whole run

Acts as a voluntary death, so the run ends the same way a death does:
- New **GIVE UP** button in the pause menu, opening a confirm ("Give up this
  run? It ends like a death — stats are recorded and the slot's run is
  cleared.").
- On confirm (**as built**): `GameManager.GiveUpRun()` → `GameSession.EndRun`
  (profile updated, slot's run wiped) → `RetryRun()` (new run, same slot,
  written straight away). No death screen, and run-end interceptors are
  skipped.
- Works in RoguelikeMode and BossArena, since both use the same canvas prefab.
- In the store (§3) it first closes the shop and hands input back, then
  dies.

Files: `OptionsMenu.cs`, `PlayerHealth.cs`, `SavePointUIBuilder.cs` (a new
button and confirm, built into `Player Canvas RoguelikeMode.prefab`).

## 3. Save & Exit moves from the shop panel to the pause menu

- Pause-menu button **SAVE & EXIT**, shown only while the store is open
  (`ShopUIController.IsOpen` — new public getter). It calls
  `GameManager.SaveAndExitToMenu()` with no warning, because nothing is lost.
- **While in the store, the normal "Exit to Main Menu" button is hidden**, so
  there's no lossy quit next to a lossless one. Outside the store it works as
  it does now (with the checkpoint warning).
- Remove from `ShopUIController`: the `saveAndExitButton` field, its listener,
  its navigation link (`continueButton` down → nothing), and
  `OnSaveAndExitClicked`. The builder deletes the old button GameObject from the
  canvas prefab.
- Pause-menu button navigation is rebuilt whenever the menu opens, so hidden
  buttons are skipped on a pad.

Files: `OptionsMenu.cs`, `ShopUIController.cs`, `SavePointUIBuilder.cs`.

## 4. Spawner placement control (per-prefab fields)

**Now:** `DungeonMapGenerator.SpawnEnemySpawnerInRoom` hardcodes the
placement: only tiles with exactly 2 adjacent walls (room corners), 1–3 per
room, every room except start/end. `spawnerOffsetFromCenter` and
`spawnEnemySpawnersInMainRooms` are unused, and so is the respawn path.

**Change:** placement settings live on the spawner prefab, and the generator
reads them from there.
- New serialized block on `EnemySpawner`, under **Dungeon Placement**:
  - `placement` — `Corner` (current behaviour), `Center` (room centre ±
    `centerJitter` tiles), `Interior` (any floor tile at least `wallClearance`
    tiles from a wall), `AlongWall` (edge tiles with 1 adjacent wall)
  - `perRoomMin` / `perRoomMax` (current: 1 / 3)
  - `wallClearance` (tiles, for Center/Interior)
  - `centerJitter` (tiles)
  - `minDistanceFromOtherSpawners` (the generator's `minDistanceBetweenSpawners`
    stays, but only for the unused respawn path)
  - `roomTypes` — a flags mask of `RoomType` values this spawner may appear in
    (default: all except START/END, same as now)
- The generator gets a **list** of spawner prefabs (`spawnerPrefabs`) so one
  room can have, for example, corner spawners plus a centre spawner. The old
  `enemySpawnerPrefab` stays and is used as the list's first entry if the list
  is empty, so the current scene keeps working with no inspector changes.
- Every candidate tile is checked against the floor/wall sets. If none are
  valid (a small room with Interior placement), it logs a warning and skips.
- Gizmo: selecting the prefab draws its placement mode as text. Selecting the
  generator in Play mode marks each room's chosen spawner tiles.
- The unused `spawnerOffsetFromCenter` / `spawnEnemySpawnersInMainRooms` fields
  get removed. The respawn code stays untouched because it's out of scope.

Files: `EnemySpawner.cs`, `DungeonMapGenerator.cs`. You set the values on the
spawner prefab(s) afterwards.

## 5. Steam policies + controller support — reference doc

Write `SteamReleaseNotes.md` beside this plan, checked against current
Steamworks docs at writing time:
- **Store / release policy checklist:** app fee, "Coming Soon" page timing,
  store and build review, required capsule and screenshot assets with sizes,
  content survey / mature content, **AI-generated content disclosure**, the
  price and regional pricing tool, refunds, demo rules (the demo is its own
  app), Steam Next Fest eligibility, and the Steam Deck compatibility review.
- **Controller support declaration** for the store page, listing what the code
  supports today:
  - Xbox One/Series and generic XInput pads
  - DualShock 4 and DualSense (the game already shows PlayStation button
    names via `BindingLabels`)
  - Switch Pro: pending verification
  - Which store checkboxes to tick ("Full Controller Support", "PlayStation
    controllers") and what Steam's review expects for each
- **Bluetooth:** Unity Input System 1.14.2 talks to DS4/DualSense as HID devices
  over USB and Bluetooth on Windows, and Xbox pads connect over Bluetooth
  through the OS. I'll check the exact device list against the Input System
  docs. There's also a **Steam Input caveat**: when launched from Steam, Steam
  can wrap a PS pad as a virtual Xbox pad. Button prompts would then show Xbox
  names, and "PlayStation controller support" on the store page expects
  correct PS prompts. The doc will cover the Steamworks setting that controls
  this. There's no Steamworks SDK in the project today.
- No code in this item.

---

## Order of work

1. §3 Save & Exit move + §2 Give Up (same files and builder, one pass)
2. §1 Virtual keyboard
3. §4 Spawner placement
4. §5 Steam doc

After each step: compile check, then a short list of what to test in Play
mode. I commit only when you say so.
