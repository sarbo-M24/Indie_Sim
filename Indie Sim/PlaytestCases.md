# Playtest Cases — untested work as of 2026-10-07

**What counts as untested:** everything in `PauseKeyboardSpawnerSteamPlan.md`
(its status line reads *"implemented 2026-09-29 (compile-checked; awaiting
Play-mode test)"*), plus the controller work from 2026-10-06 (commits
`1c0881b`, `aece207`), plus the tutorial and settings work from 2026-10-07
(G–I, detailed in `PROGRESS_REPORT.md` ▸ "Latest session"), plus the shop
and menu polish from later on 2026-10-07 (J). **S** is a Steam demo readiness
pass for the build itself.

| # | Feature | Where it lives | Committed? |
|---|---|---|---|
| A | Virtual keyboard (slot name prompt) | `VirtualKeyboard.cs`, `NamePromptDialog.cs`, `Main menu.unity` | No |
| B | Give Up (pause menu) | `OptionsMenu.cs`, `GameManager.GiveUpRun`, `ConfirmDialog.cs`, canvas prefab | No |
| C | Save & Exit moved from the shop to the pause menu | `OptionsMenu.cs`, `ShopUIController.cs`, canvas prefab | No |
| D | Spawner placement settings | `EnemySpawner.cs`, `DungeonMapGenerator.cs` | No |
| E | Controller hints + RB/R1 shop Continue | `ControllerPresence.cs`, `ControllerHintPanel.cs`, `ShoulderGlyphImage.cs`, `ShopUIController.cs` | Partly (rows in the pause/shop/death/main menu aren't) |
| F | B on the pause menu resumes | `OptionsMenu.OnCancelPressed` | Yes |
| G | First-run tutorial, death restart, Tutorial button replay | `Scripts/Tutorial/`, `Tutorial.unity`, `GameManager`, `GameSession`, `GlobalSections`, `Teleporter`, `MainMenu`, `Main menu.unity` | No |
| H | Tutorial spawners (room-entry wake, room clear) | `EnemySpawner.cs`, `ActivateEnemySpawner.cs`, `Prefabs/Enemies/Tutorial/`, `TutorialHintZone.cs` | No |
| I | Damage Flash rename + Psychedelic Blood Intensity slider | `GameSettings`, `SettingBindings`, `PsychedelicBloodController`, `Settings Panel.prefab` | No |
| J | Cat petting on pad, B deselect, pack cig hover lift, canvas button style, Reset/Back footer, starting coins on GameSession, slime event | `ShopMascot`, `ShopUIController`, `PackCigView`, `CursorTooltip`, `ButtonFocusScale`, `ButtonFocusStyle`, `TabbedMenuPanel`, `GameSession`, `CoinManager`, `EnemyTransition.anim`, canvas prefab | No |
| S | Steam demo readiness (build-level) | the built .exe | nothing new to commit |
| — | Steam notes (`SteamReleaseNotes.md`) | doc only | nothing to test |

**Setup:** an Xbox pad (wired is fine), keyboard + mouse. Start from **Boot**
or **Main menu** unless a case says otherwise. Keep the Console open and
filtered to warnings and errors. Every case also expects **no errors in the
Console**.

## Status after Claude's Play-mode run (2026-10-07)

Claude ran these at **logic level** (calling the game's own methods, reading
state, save files and logs), with saves backed up and restored. They passed
unless noted. Not covered by that run: anything needing a real pad press, a
real mouse click, or a judgement about how it looks or feels.

| Section | Verified by Claude | Still yours |
|---|---|---|
| A | — | **all** |
| B | B2, B4, B5, B7, B8, B9 (called `GiveUpRun` directly) | B1, B3, B6, plus **one** Give Up through the real pause-menu button |
| C | C1–C5; C6 state only | C6 by hand (focus and buying after closing pause) |
| D | D1–D3 (5 dungeons: no start/end spawners, spacing ≥ 5, teleporter ≥ 5.5, about 1 small-room warning per dungeon) | D4–D10 |
| E | — | **all** |
| F | — | **all** (simulated keys can't reach the game's input actions) |
| G | G1, G2, G7, G11, G13–G17 | G3–G6, G8 (first-run, not replay), G9 (watch: once, dungeon 1 completed itself straight after the tutorial; not reproduced since), G10 (pick coins up in the tutorial and confirm they carry), G12, G18 |
| H | H1, H2, H3, H5, H7; H6 logic only | H4, H6 with a real stomp (many die at once), H8, H5's 4+4 then 3+3 timing |
| I | I1, I10 (new values), I11 | I2–I9, I10's "older Flash value carried over" |
| J | — | **all** |
| S | — | **all** |
| K | — | **all** (pad layout, added after the run) |
| L | — | **all** (pause over shop, added after the run) |
| M | — | **all** (shop resume fixes, added after the run) |
| N | — | **all** (hints follow last input, added after the run) |
| O | — | **all** (menu panel look + cross-device rebinding, added after the run) |

---

## A. Virtual keyboard

The name field's limit is **20** characters (the plan said 16; the keyboard follows the field).

| ID | Steps | Expected |
|---|---|---|
| A1 | Pad only. Main menu → Play → pick an **Empty** slot. | Name prompt opens with the on-screen keyboard. Focus is on **Q**. The field shows "Slot N". SHIFT reads **SHIFT ON**. |
| A2 | From A1, press A on any letter. | "Slot N" is **replaced** by that capital letter. Shift turns off (keys show lowercase). |
| A3 | Type a few more letters, then **X**. | Each letter is lowercase. X deletes one character. |
| A4 | Press **Y**. | A space is added. |
| A5 | Press **BACK** until the field is empty. | Field empties and SHIFT turns back **on**. |
| A6 | Type until nothing more is added. | Stops at 20 characters; extra presses do nothing. |
| A7 | Press **CLEAR**. | Field empties, SHIFT on. |
| A8 | D-pad around the keyboard. SHIFT/SPACE/BACK/CLEAR/DONE are now a column on the **right**. | Right off the end of a row (or Left off its start) lands on the special key at that height. From a special key, Left goes to the end of the nearest row, Right to its start. Up/Down wrap within the grid and within the column. Focus never leaves the keyboard. The panel is wider (≈1066px) and nothing overlaps the name box above. |
| A9 | Type "Test", press **DONE**. | Prompt closes and the run starts in that slot. The slot later shows "Test". |
| A10 | Repeat with a new slot and finish with **Start** instead of DONE. | Same as A9. |
| A11 | Open the prompt and press **B**. | Prompt cancels; you're back on slot select with nothing created. |
| A12 | Open the prompt and press **DONE** without typing. | The slot is named "Slot N" (the default). |
| A13 | Open the prompt with the pad, then **move the mouse**. | Keyboard hides; the text field is focused and you can type on the physical keyboard. |
| A14 | From A13, press any pad button. | Keyboard comes back with focus on Q. Text typed so far is kept. |
| A15 | Mouse/keyboard only (pad unplugged). Open the prompt. | No on-screen keyboard. The field is focused and Enter confirms. |
| A17 | Move around the keyboard with the D-pad. | The selected key is **inverted** (white key, dark text); the previous key goes back to dark. Exactly one key is inverted at a time. Pressing A on it still dips it slightly. |
| A18 | Look at SPACE / BACK / DONE (after assigning X, Y and Start sprites in the glyph set). | Each shows its shortcut icon on the left (SPACE = Y, BACK = X, DONE = Start; Triangle/Square/Options on a PS pad), label to the right. With no sprite assigned the icon is hidden and **one** warning is logged. |
| A19 | Pad. Open slot select (before picking a slot). | The hint row shows bottom-left. When the name prompt or the delete confirm opens, it's covered by them. |
| A20 | Pad. Slot select with a used slot: focus its card and press **X**. Repeat with focus on its Delete button, on an **Empty** slot, and on Back. | Used slot: the delete confirm opens (same as the Delete button); No keeps it, Yes empties it. Empty slot / Back: nothing happens. Hint row shows a 5th hint, "Delete", with the X icon (once the X sprite is assigned). |
| A21 | Name prompt open, press **X**. | Backspace only; the slot isn't deleted. |
| A16 | Pad. Open the prompt and keep the stick still while looking at it. | Keyboard stays up (it shouldn't flicker if the mouse isn't touched). **Watch:** a mouse that jitters on the desk will hide it (by design, any mouse input switches to mouse mode). |

## B. Give Up

| ID | Steps | Expected |
|---|---|---|
| B1 | Mid-dungeon, pause. | Pause menu shows **Give Up** and **Exit** (no Save & Exit). |
| B2 | Give Up → confirm dialog. | Text: "Give up this run? It counts as a death and a new run starts right away." The Yes button reads **GIVE UP**. |
| B3 | Answer No (and separately: press B). | Dialog closes, still paused, run untouched. |
| B4 | Give Up → **GIVE UP**. | No death screen. The game reloads straight into a **fresh run, level 1**, same slot, with coins/upgrades reset. Game isn't frozen and the pause menu isn't stuck open. **If the tutorial hasn't been finished yet, the fresh run starts in the tutorial instead** (see G8). |
| B5 | After B4, quit to the main menu and look at the slot. | The slot shows the new run (level 1), not the old one. Best-run dungeons cleared is the higher of the old value and the given-up run. |
| B6 | After B2, cancel, then pick **Exit** instead. | The same dialog now says the checkpoint warning and its Yes button is back to its normal label (not "GIVE UP"). |
| B7 | Give Up from inside the **shop** (pause while shopping). | Shop closes and the new run starts. Player input works in the new run: you can move, fire, dash. |
| B8 | Give Up in the **Boss Arena**. | New run, level 1, in the dungeon (not the arena). |
| B9 | After B4, die normally later in the new run. | Normal death flow. The death is recorded (this proves Give Up didn't leave the run-end flag stuck). |

## C. Save & Exit (moved to the pause menu)

| ID | Steps | Expected |
|---|---|---|
| C1 | Clear a dungeon to reach the shop. Look at the shop panel. | No Save & Exit button on the shop itself anymore. Continue's pad navigation: Down from Continue goes nowhere. |
| C2 | In the shop, pause. | Pause menu shows **Save & Exit** and **Give Up**. **Exit is hidden.** Pad focus skips the hidden button. |
| C3 | Save & Exit. | No warning. Goes straight to the main menu. |
| C4 | Continue that slot. | You resume **in the shop**, with the same coins and pack. |
| C5 | Outside the shop, pause. | Exit is back, Save & Exit hidden. Exit shows the checkpoint warning. |
| C6 | Open the pause menu over the shop, then close it again. | The shop still works afterwards: focus, buying, and Continue. |

## D. Spawner placement

The current scene uses the old setup: `Spawner Prefabs` is empty, so it falls back to **Enemy Spawner** (Corner, 1–3 per room, all rooms except start/end, min distance 5).

| ID | Steps | Expected |
|---|---|---|
| D1 | Play 3–4 dungeons as-is. | Spawners appear in room corners, 1–3 per room, never in the start or end room. Feels the same as before. |
| D2 | Watch the teleporter (end of the last main room). | No spawner within 3 units of it. (`teleporterClearance` is new and wasn't in the plan.) |
| D3 | Console during generation. | Only expected logs. A "No Corner spawner positions…" warning for a tiny room is OK; a flood of them isn't. |
| D4 | On the Enemy Spawner prefab, set Placement = **Center**, Center Jitter 1. Play. | Spawners sit at room centres (within ±1 tile), clear of walls. |
| D5 | Placement = **Interior**, Wall Clearance 2. | Spawners anywhere at least 2 tiles from a wall. Small rooms may log "No Interior positions" and skip. No errors. |
| D6 | Placement = **AlongWall**. | Spawners hug a single wall, not corners. |
| D7 | Per Room Min = Max = 0. | No spawners at all and no errors (the run may not be clearable, so this is just a sanity check). |
| D8 | Room Types: add **Start**. | Spawners can now appear in the start room. |
| D9 | Add a 2nd prefab to Generator → Spawner Prefabs (e.g. a duplicate set to Center, 1–1). | Rooms get both kinds: corner ones plus a centre one. Min distance is respected across both prefabs. |
| D10 | Select the Center/Interior prefab in the Scene view. | Cyan square gizmo showing the wall-clearance area. |

Put the prefab back to **Corner / 1–3 / AllButStartAndEnd** after D4–D10.

## E. Controller hints + RB shortcut

The glyph set is filled with Xbox A/B/D-pad + RB and PlayStation Cross/Circle + R1.

| ID | Steps | Expected |
|---|---|---|
| E1 | No pad plugged in. Open pause, Settings, Controls, the shop, the death screen, and the main menu. | No hint rows anywhere and no RB glyph. Mouse/keyboard behave exactly as before. |
| E2 | Plug in an Xbox pad. Open each of the above. | Each shows its 4-icon + label row (Settings/Controls centred under the window, the rest bottom-left). The shop shows the **RB** glyph right of Continue. |
| E2b | With a pad: die, and open the tutorial panel. | Each shows **one** hint only: A / Cross "Select". No Up, Down or Back, even after unplugging and replugging the pad. |
| E2c | Open Settings **and Controls** (pause and main menu): no pad, then Xbox, then PS. | The "LB / RB or Q / E" text is gone. Either side of the tabs: **Q ◀ … ▶ E** with no pad, **LB ◀ … ▶ RB** on Xbox, **L1 ◀ … ▶ R1** on PS. Icons swap live on plug/unplug and look crisp (no blur). |
| E3 | Pause menu open, **unplug** the pad. | Hint row disappears immediately. Plug it back in: it reappears. |
| E4 | In the shop, press **RB**. | Same as clicking Continue: the next dungeon generates. Afterwards: quit to menu, continue the slot, and you resume at the **start of that next level** (checkpoint saved). |
| E5 | In the shop, open the pause menu, then Settings. Press **RB**. | Settings switches tab. The shop does **not** continue. Back out to the pause menu and press RB: still nothing. |
| E6 | In the shop, trigger a brand-conflict buy (the replace popup) and press RB. | Nothing happens; the popup stays. |
| E7 | In the shop, hold **RB** through the transition into the next level. | Nothing fires on spawn; the player stands still. |
| E8 | In the shop, press RB repeatedly fast. | Only one Continue; no double level skip. |
| E9 | Settings opened over the shop, with a pad. | Two hint rows visible (shop's, dimmed, plus Settings'). Expected; flag it if it bothers you. |
| E10 | (Optional, needs a PS pad) DualShock/DualSense. | Cross/Circle icons and R1. Through **Steam** with Steam Input on, it may show Xbox icons; that's the known caveat. |
| E11 | Unassign one hint sprite in the glyph set, open a menu with a pad. | That hint (icon + label) is hidden. **One** warning in the Console, not one per frame. |

## F. B on the pause menu

| ID | Steps | Expected |
|---|---|---|
| F1 | Pad. Pause, press **B**. | Game resumes. |
| F2 | Pause → Settings → **B**. | Back to the pause menu (not gameplay). B again: gameplay. |
| F3 | Pause → Controls → B → B. | Same as F2. |
| F4 | Pause → Exit (warning) → **B**. | Warning closes, still paused. B again: resume. |
| F5 | Keyboard. Pause with **Esc**, then Esc. | Resumes once and stays resumed (no flicker back into pause). |
| F6 | Pause over the shop → B. | Back to the shop, shop still usable. |
| F7 | Main menu → Settings → B. | Back to the main menu. |

## G. Tutorial

**Reset first:** **Tools ▸ Save ▸ Delete All Saves**, or **Tools ▸ Tutorial ▸ Replay Tutorial On Next New Run** (that one only clears the "tutorial done" flag). Do G1–G12 in order, using one slot.

| ID | Steps | Expected |
|---|---|---|
| G1 | Boot → main menu. | No **TUTORIAL** button (the tutorial hasn't been finished). |
| G2 | Start → pick an Empty slot → name it. | **Tutorial** scene loads, not dungeon 1. The HUD and **crosshair** show. No run timer, no old 5-second tutorial popup. |
| G3 | Walk into Room 1 and move. | The bubble and cat fade in with "**[W/A/S/D]** to move", text inside the bubble. After about 1 s of moving: "Nice! Now go to the next room". |
| G4 | Plug in a pad and move a stick, then go back to the mouse. | Key names in the hint switch to pad names (e.g. **[A]**) and back, live. |
| G5 | Room 3: dash. | "{Dash} to dash" with your dash key. Dashing completes it. |
| G6 | Every room: long hints. | Text wraps or shrinks and never spills outside the bubble; nothing overlaps the tail. |
| G7 | Die in the tutorial (stand in the Room 4 swarm). | **No death screen.** Corpse plus "Ouch! Let's try that again." for about 1.5 s, then the tutorial reloads at Room 1 with the **starting coins** (50, set on GameSession in Boot), not what was left when you died. Total runs isn't increased. |
| G8 | In the tutorial: Pause ▸ **Give Up** ▸ confirm. | Tutorial restarts (not dungeon 1). |
| G9 | Clear Room 4 (see H) and step into the **teleporter**. | Teleport effect, then **dungeon 1** with the coins picked up in the tutorial. |
| G10 | From G9: Pause ▸ Exit to the menu, then **Continue** that slot. | Resumes at dungeon 1 **with the tutorial coins** (the run was re-saved at the teleporter). |
| G11 | Main menu after G9. | **TUTORIAL** button shows (between CONTROLS and the achievements button) and is reachable with the pad. |
| G12 | Start a new run in **another** slot. | Goes straight to dungeon 1, no tutorial. Same after restarting the game. |
| G13 | Main menu ▸ **TUTORIAL**. | Tutorial loads with the starting coins (50) and no upgrades. |
| G14 | Replay: clear Room 4. | Bubble says "Nice refresher! Step into the teleporter to head back to the menu." Teleporter goes to the **main menu**, not a dungeon. |
| G15 | Replay: die, and separately Pause ▸ Give Up. | Both restart the tutorial. No death screen. |
| G16 | After G13–G15, **Continue** the slot from G10. | Its run is exactly as it was (the replay wrote nothing to the slot). |
| G17 | Fresh profile: start a run, quit mid-tutorial (Pause ▸ Exit), then **Continue** that slot. | Resumes in **dungeon 1** (the tutorial isn't resumable). Starting a new run later still plays the tutorial, since it was never finished. |
| G18 | Press Play directly on `Tutorial.unity` in the editor. | Scene works (hints, enemies, death restart). Expected: the teleporter loads dungeon 1 with no slot active. |

## H. Tutorial spawners

| ID | Steps | Expected |
|---|---|---|
| H1 | Walk past the wall of Room 2 or Room 4 without entering. | Their spawners stay **asleep** (no yellow activation flash, no enemies), even within about 10 units through a wall. |
| H2 | Enter Room 2. | The spawner flashes, releases **4** fodder at once, then turns grey. Hint: "Aim and hold **[LMB]** to shoot the enemies". |
| H3 | Room 2: kill 3 of the 4. | Room doesn't complete yet. Killing the 4th completes it ("Good shooting! On to the next room"). |
| H4 | Room 2, alternative: shoot the spawner itself **before** walking fully in (if you can reach it). | If it dies before spawning, the room completes once you've fired. No soft-lock. |
| H5 | Enter Room 4. | Both spawners wake; 4 + 4 appear, then 3 + 3 about 0.5 s later (14 total), clumped. All 14 chase you. Hint: "They're swarming! **[key]** to blast them all at once". |
| H6 | Room 4: stomp into the clump. | Many die at once. The room completes only after **all 14** are dead **and** you've stomped at least once. Then the teleporter appears. |
| H7 | Room 4: kill everything by shooting only. | Room doesn't complete until you stomp once. |
| H8 | Scene view, select a hint zone. | Red lines from Room 2/4's zone to its spawners. The label reads e.g. "Fire + clear room". |
| H9 | **Regression:** play a few dungeons. | Normal spawners still wake by **proximity** as before. The tutorial spawner prefabs never appear in dungeons. |

## I. Settings sliders

| ID | Steps | Expected |
|---|---|---|
| I1 | Main menu ▸ Settings ▸ Gameplay. | Six rows in order: Damage Numbers, Psychedelic Mode, **Psychedelic Blood Intensity**, Screen Shake, Camera Lead, **Damage Flash Intensity**. Nothing overflows the window; spacing is a bit tighter than the Audio tab. |
| I2 | Same from the **pause menu** ▸ Settings. | Same six rows, same fit. |
| I3 | Pad: navigate the Gameplay tab. | Focus reaches the new slider. Left/Right moves it 5% per press. |
| I4 | Psychedelic Mode **on**, Blood Intensity **100%**, kill enemies. | Splatter and ground blood cycle through the rainbow (as before). |
| I5 | Drag Blood Intensity to **50%** in the pause menu while blood is on the floor. | Floor blood shifts live to a muted, red-leaning version of the colours. New splatters match. |
| I6 | Blood Intensity **0%**, mode on. | Blood looks exactly like normal red blood. |
| I7 | Psychedelic Mode **off**, any intensity. | Normal blood. The slider has no effect. |
| I8 | Damage Flash **0%**, take a hit. | No red vignette pulse. (The sprite still blinks, hitstop and shake still happen; expected for now.) |
| I9 | Damage Flash **100%**, take a hit. | Red vignette pulses twice. |
| I10 | Set both sliders, quit the game, relaunch. | Values kept. A Flash value set **before** today's change also carried over. |
| I11 | Settings ▸ Reset to defaults. | Both back to 100%. |

---

**If everything passes:** the A–D work plus the remaining hint rows can be
committed together. The uncommitted `Player Canvas RoguelikeMode.prefab` and
`Main menu.unity` carry both. **G–I** can go in their own commit or with the
above, but `Main menu.unity` also carries the TUTORIAL button, so it goes in
whichever commit lands last (or commit everything together).

## J. Shop and menu polish (2026-10-07)

| ID | Steps | Expected |
|---|---|---|
| J1 | Pad. Open the shop. From any offer card (or Buy) press **Left**. | Focus lands on the **cat**. The "Pet" tooltip appears in the **centre of the cat panel**, not at a corner. |
| J2 | From J1 press **A** a few times. | Each press: the cat hops and cycles its pet frames, and the bubble says a pet line. The tooltip stays put (the panel doesn't hop). |
| J3 | From the cat press **Right**. | Back to the card you'd selected; with none selected, the first card; with all bought, the bottom row. |
| J4 | Mouse. Hover the cat, then click it. | Tooltip follows the cursor (unchanged). Click pets. |
| J5 | Pad. Select an offer (A), then press **B**. | Highlight goes, Buy greys out, detail text resets to "Select a cig to see its details." |
| J6 | Select an offer, move focus to **Buy**, press **B**. | Selection clears and focus jumps back to that card (not stranded on the greyed Buy). |
| J7 | Select a pack cig (lifts + highlight), press **B**. Then select one, move to **Burn**, press **B**. | Both clear; from Burn, focus returns to the cig. |
| J8 | Select an offer, then move focus around without pressing A. | Selection stays. Only B, or A on something else, changes it. |
| J9 | Keyboard: select an offer with the mouse, press **Esc**. | Pause opens; Esc does **not** clear the selection. |
| J10 | Trigger the replace popup, press **B**. | Nothing happens (the popup still needs its Cancel button). |
| J11 | Mouse. Hover a pack cig, move off it. | Hover: border **and** lift. Off: both go. |
| J12 | Hover the **bottom edge** of a pack cig and hold still. | No flicker (it doesn't bounce up and down). |
| J13 | Click a pack cig, move the mouse away. | Highlight shows and it **stays lifted**. Burn still works. |
| J14 | Mouse, then pad, over every button in pause, quit warning, Settings, Controls, death, victory, tutorial Skip. | Same look either way: grows (1.15×) with a reddish tint. **No darkening** with the pad. Pressing flashes light red. Greyed buttons don't react. |
| J15 | Same over the shop's buttons, cards and pack cigs. | **Buy / Burn / Reshuffle / Continue** (changed later on 2026-10-07): grow 1.1× with a light reddish tint, same with mouse and pad, no extra darkening on the pad; tune it on `ShopUIController` ▸ *Action Button Focus*. Cards and cigs keep their own hover. |
| J16 | Pad. Settings (from the main menu **and** from pause): go Down to **Reset to defaults**, press **Right**. Repeat in **Controls**. | Focus moves to **Back**; Left returns to Reset. Up from either goes to the last row; Down wraps to the top row. |
| J17 | Main menu → Play → an **Empty** slot → name it, then get back to slot select (quit to menu). | The new slot shows **50** coins (not 0). In-game you start with 50. |
| J18 | Boot ▸ `[Persistent]` ▸ **GameSession**: set Starting Coins to e.g. 70 and start a new run. Put it back to 50 after. | The new run, its slot and a tutorial restart all use 70. The CoinManager values in the scenes no longer matter (only a fallback for pressing Play directly on a scene). |
| J19 | Fight fodder (slimes) in a dungeon. | No "'slime' AnimationEvent has no function name" errors; the slime animation looks the same. |

## S. Steam demo readiness (build-level)

Researched 2026-10-07. Valve's build review checks three things: the game
**launches on every OS the store page lists**, **every feature the store page
lists is in the build**, and **no in-game purchases bypass Steam Wallet**
([Review process](https://partner.steamgames.com/doc/store/review_process)).
For demos Valve also wants demo-specific store content and capsules that say
"Demo", recommends **disabling achievements in demos**, and allows linking to
the full game only through the Steam overlay
([Demos](https://partner.steamgames.com/doc/store/application/demos)).
"Full Controller Support" on the store page means the whole game is playable
on a pad, with correct glyphs and no screen that needs mouse or keyboard
([Steam Input for devs](https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_devs)).
Store page, content survey and the controller questionnaire are in
`SteamReleaseNotes.md`; these are the checks to run on the **built .exe**.

Findings from the code that these cases target:
- ~~No Quit button~~ **Fixed 2026-10-07:** QUIT is now the last button on the
  main menu (`MainMenu.QuitGame`). S4/S5 check it.
- `productName` is **`OneBitKill(Beta)`**. It's the window title, the exe name
  and part of the save folder path
  (`LocalLow/ElderMonkInteractive/OneBitKill(Beta)`). Settle the demo's name
  **before** release: renaming it later moves the save folder, so players'
  existing saves would seem to vanish.
- ~~Run In Background on~~ **Turned off 2026-10-07:** the build now freezes
  while it isn't the focused window, so tabbing out can't kill you (S7).
- No Steamworks SDK, no web links, no purchases. Achievements are in-game
  only, so "disable Steam achievements in the demo" doesn't apply yet.

| ID | Steps | Expected |
|---|---|---|
| S1 | Make a **Windows build**. Copy the whole build folder to another PC without Unity (or at least another drive), run the exe. | Starts to the main menu; no error dialogs or missing-DLL messages. |
| S2 | Before launching, rename `%USERPROFILE%\AppData\LocalLow\ElderMonkInteractive\OneBitKill(Beta)` (rename it back after). | A true first launch works: no errors, a new run goes through the **tutorial**, the save folder and `settings.json` get created. |
| S3 | In the build, play start to finish: tutorial → 3 dungeons + shops → boss → Demo Complete. | Nothing blocks progress. The Demo Complete screen makes clear the demo is over and returns to the menu. |
| S4 | Repeat S3 **pad only**, from launch to quitting, never touching mouse or keyboard (slot naming, settings, every menu, quitting). | Possible end to end, with Xbox prompts everywhere. Needed for a "Full Controller Support" claim. Quit with the new **QUIT** button. |
| S5 | From the main menu, quit normally (no Alt-F4), once with the mouse and once with the pad. | **QUIT** (last main-menu button, reachable with the pad) closes the game cleanly, no hang or error. |
| S6 | Mid-dungeon press **Alt-F4**, relaunch, Continue. Repeat mid-shop. | Closes without hanging. The slot loads at its last checkpoint; no corrupted save. |
| S7 | Fullscreen: Alt-Tab out mid-fight for about 10 s, then back. Also minimize and restore. | No black screen, wrong resolution or lost input on return. While tabbed out the game is **frozen** (Run In Background is off): enemies don't move, timer doesn't run, no damage taken. It resumes on return (no pause menu opens). |
| S8 | Run at 1280×720, 1920×1080, 2560×1440 and an ultrawide (21:9) if possible, windowed and fullscreen. | HUD, menus, shop and tooltips fit and stay readable; nothing cut off. |
| S9 | Once the demo app exists, launch **through Steam** and press **Shift+Tab**. | Steam overlay opens and closes; the game doesn't take the overlay's clicks or keys as input. |
| S10 | Launch through Steam with an Xbox pad, then a PS pad (Steam Input on). | Both work. A PS pad shows Xbox glyphs under Steam Input: the known caveat in `SteamReleaseNotes.md` §4. Don't claim PS support until that's settled. |
| S11 | In the build, use every Settings option and the Controls rebinding, then relaunch. | All work in the build (not just the Editor) and are kept after relaunch. |
| S12 | Leave the build idle on the main menu, and paused mid-run, for 10+ minutes; watch Task Manager. | Memory stays flat; no crash or audio glitches. |
| S13 | Compare the store page's listed features (languages, controller support, single-player…) with the build. | Everything listed is really there. Only English is listed unless more languages get added. |

## K. Pad layout change (2026-10-07)

Dash = **LT** (unchanged), Stomp = **LB** (was A), Switch Weapon = **RB** (was Y). Only `PlayerControls.inputactions` changed; the tutorial hints read the live binding.

| ID | Steps | Expected |
|---|---|---|
| K1 | Pad, in a dungeon: press LT, LB, RB. | LT dashes, LB stomps, RB switches to the next weapon. A and Y no longer stomp or switch. |
| K2 | Tutorial Room 3 and Room 4 with a pad. | Hints read "**[LT]** to dash" and "**[LB]** to blast them all at once" (L2 / L1 on a PS pad). LB completes Room 4's stomp task. |
| K3 | Pause → Controls → Gamepad tab. | Stomp shows LB, Switch Weapon shows RB. Reset to defaults gives the same. |
| K4 | In the shop, press RB once. | Continues to the next level only; the weapon doesn't switch (the Player map is off in the shop). |
| K5 | Pause → Settings, LB/RB between tabs, then resume. | Tabs switch; on resume you haven't stomped or switched weapon. |
| K6 | Anyone with an old **gamepad** rebind for Stomp or Switch Weapon in `settings.json` keeps their own binding. | Expected: saved overrides win over the new defaults. Yours only has keyboard/mouse overrides, so you'll get the new layout. |

## L. Pause menu draws over the shop (2026-10-07)

`Options Panel`, `Settings Panel` and `Controls Panel` in `Player Canvas RoguelikeMode.prefab` now each have their own **Canvas** (Override Sorting on, order 1100 / 1110 / 1110, above the root's 1000) plus a **GraphicRaycaster**. They draw above `UpgradeShop` wherever they sit in the hierarchy.

| ID | Steps | Expected |
|---|---|---|
| L1 | In the shop, press **Esc** (and separately Start on a pad). | Pause menu, its dark overlay and buttons show **on top** of the shop. |
| L2 | From L1: click/pad through Save & Exit, Give Up (confirm dialog), Settings, Controls. | Each shows above the shop and takes clicks; nothing on the shop reacts underneath. |
| L3 | Close pause again. | Shop is usable as before (focus, buying, cat, Continue). |
| L4 | Pause mid-dungeon (no shop) and open Settings / Controls. | Same as before: no visual change. |
| L5 | Tutorial scene: pause, open Settings. | Shows normally (same prefab). |
| L6 | In the shop, pause. Look at **Save & Exit**, then hover it with the mouse and select it with the pad. | Same `button_0` sprite and 30 pt label as Resume / Settings / Controls / Give Up; grows 1.15× with the reddish tint like them. |

## M. Resuming into the shop (2026-10-07)

Two fixes: the scene's own Teleporter in `RoguelikeMode.unity` is deleted (each dungeon spawns its own, and `Teleporter.ActivateTeleporter` ignores calls while no dungeon is running), and the 5-second instruction popup (`TutorialManager`) is skipped when the run resumes into the shop. Start from **Boot / Main menu**: pressing Play on `RoguelikeMode` uses a throwaway debug slot, so Save & Exit isn't written.

| ID | Steps | Expected |
|---|---|---|
| M1 | Reach the shop, Pause ▸ Save & Exit, Continue the slot. | Shop opens straight away: no countdown behind it, no teleport sound/effect, time not frozen. |
| M2 | From M1, use the shop with the pad straight away. | Focus is on the shop (not an invisible Skip button); cat animates; buying works. |
| M3 | From M1, press Continue. | Next dungeon starts with no popup; its teleporter (last room) works normally. |
| M4 | Exit mid-dungeon (not in the shop), Continue the slot. | The 5-second popup **does** show, as before. |
| M5 | New run from the main menu (after the tutorial is done). | The popup shows on dungeon 1, as before. |

## N. Gamepad hints follow the last input, not what's plugged in (2026-10-07)

Hint rows, the RB glyph by the shop's Continue, the virtual keyboard's key icons and the Q/E ↔ LB/RB tab glyphs now show only while the player's **last input came from a pad** (`ControllerPresence.IsActive`). Moving the mouse or pressing a key hides them / switches to Q/E even with a pad plugged in; any pad input brings them back. **This replaces section E's "plug in a pad → hints appear":** plugging in alone no longer shows them until the pad is used.

| ID | Steps | Expected |
|---|---|---|
| N1 | Pad plugged in, open the pause menu with **Esc**. | No hint row (last input was the keyboard). |
| N2 | From N1, press D-pad down or move the stick. | Hint row appears straight away. |
| N3 | From N2, nudge the mouse. | Hint row disappears straight away. Pad button again: back. |
| N4 | Same in Settings / Controls (pause and main menu). | Tab glyphs swap live: Q ◀ ▶ E after the mouse/keyboard, LB ◀ ▶ RB (L1/R1 on PS) after the pad. |
| N5 | Shop with a pad: use the pad, then the mouse. | RB glyph by Continue shows after the pad, hides after the mouse. RB still continues either way. |
| N6 | Unplug the pad while the hints show. | Hints hide. Plug back in: they stay hidden until a pad button or stick is used. |
| N7 | Game start with only a pad, no mouse touched: first press on the main menu. | Hints appear on that first pad press (not before). |
| N8 | Main menu / pause / shop / slot select: use the pad. | The **mouse cursor disappears** with the first pad input and the hints show. Only the pad-selected button is highlighted, even if the hidden cursor is resting on another button. |
| N9 | From N8, nudge the mouse (and separately press a key). | Cursor reappears where it was, hints hide, hover highlights work again. |
| N10 | Gameplay (no menu) with the pad, then the mouse. | No OS cursor either way (as before); the crosshair is unchanged. |

## O. Main-menu Settings/Controls look + cross-device rebinding (2026-10-07)

The Roguelike copies' window sprite/colour, Back/Reset button sprites and black label text were moved into `Settings Panel.prefab` and `Controls Panel.prefab`, so both scenes share them; the Main menu copies also got a `ButtonFocusStyle` (same grow + red tint as the pause menu).

| ID | Steps | Expected |
|---|---|---|
| O1 | Main menu ▸ Settings, then ▸ Controls. Compare with Pause ▸ Settings / Controls in a dungeon. | Same window (cig container sprite, dark grey), same Back/Reset buttons and text colours. Buttons grow + tint the same way. |
| O2 | Controls ▸ **Keyboard** tab with the **pad**: A on a binding. | Row says "Press a key...". Hint: "Keyboard binding: press a key or mouse button · B to cancel" (CIRCLE on PS). |
| O3 | From O2 press **B**. Repeat and press **Esc**. | Both cancel: binding unchanged, still on the Controls panel (B doesn't also close it). |
| O4 | From O2 press a keyboard key instead. | It binds (swap rules as before). |
| O5 | Controls ▸ **Gamepad** tab with the **mouse**: click a binding. | Hint: "Controller binding: press a button on the pad · ESC to cancel". With no pad connected: "...connect a pad and press a button...". |
| O6 | From O5 press **Esc**. Repeat and press **Start**. | Both cancel. |
| O7 | From O5 press **B** on the pad. | B is **bound** (it's a valid pad button, not a cancel). |
| O8 | Same-device cases: keyboard tab with the keyboard, gamepad tab with the pad. | Hints as before: "...· ESC to cancel" / "Press a button to bind · START (MENU/OPTIONS) to cancel". |
