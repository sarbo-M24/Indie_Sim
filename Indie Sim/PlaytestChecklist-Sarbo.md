# Playtest checklist — Sarbo's share (2026-10-08)

Every case from `PlaytestCases.md` that needs a person: a real pad press,
mouse click or key, ears, eyes, a built .exe, or Steam. **149 cases** in seven
sessions, ordered so each session's setup carries into the next. IDs match
`PlaytestCases.md`; the other 89 cases are Claude's (logic level, on your OK).

**Every case also expects:** no errors in the Console (keep it open, filtered
to warnings + errors).

**Gear:** Xbox pad (wired is fine), keyboard + mouse, headphones/speakers. A
PS pad only for E10 / S10.

**Mark results** in the ✓ column: ✅ pass, ❌ fail (+ a short note), ⏭ skipped.

| Session | What | Cases | Time (rough) |
|---|---|---|---|
| 1 | Main menu, slot select, Settings/Controls, Credits — mouse then pad | 52 | 50–60 min |
| 2 | Tutorial, fresh profile | 9 | 15 min |
| 3 | Dungeons: combat, pause, death, HUD, teleporter, audio | 30 | 40 min |
| 4 | Shop | 33 | 35 min |
| 5 | Editor-only check | 1 | 2 min |
| 6 | Windows build + Steam | 16 | 60+ min |
| 7 | Re-check of the first pass's fixes (do this first) | 8 | 15 min |

---

## Session 1 — Main menu and menus

**Setup:** Editor, open `Boot.unity`, press Play. Pad plugged in but don't
touch it until a case says so. You'll need **two empty save slots** for A
(delete slots on slot select with X / the Delete button if needed).

### 1a. Credits and clicks (mouse, then pad)

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| Q3 | On the main menu, click several buttons quickly (e.g. open/close Settings). | Each click sounds the instant the mouse button is released, with no ~0.1 s lag. | |
| Q2 | Click a button that changes scene: Start → pick a used slot (or Continue). Then Pause ▸ Exit back to the menu. | The click is heard in full both times, not cut off by the scene load. | |
| Q1 | **Running check for all sessions.** Click and pad-press every kind of button you meet: main menu, slot select, name prompt keys, Settings/Controls Back + Reset, pause menu, confirm dialogs, shop Buy/Burn/Reshuffle/Continue, replace popup, Credits Back, tutorial Skip, death screen. | Every press clicks once. Note any button that's silent or clicks twice. | |
| R4 | Main menu ▸ CREDITS. Once it starts rolling: scroll with the mouse wheel; release and wait. Then drag the text with the left mouse button. Then push the stick / D-pad up and down. | Each method scrolls by hand. After you stop, the roll waits about 3 s, then carries on. | |
| R5 | Close Credits with the **Back** button. Reopen, close with **Esc**. Reopen with the pad (A on CREDITS), close with **B**. | All three close it. After the pad close, focus is back on the CREDITS button. | |
| R6 | Pad only: open Credits and look at it. | **Back** is focused and A on it closes the panel. Headings are gold, text is readable, nothing is cut off at the sides. | |

### 1b. Settings and Controls look, rebinding (mouse first, then pad)

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| O1 | Main menu ▸ Settings, then Controls. Later in a dungeon, Pause ▸ Settings / Controls, and compare. | Same window (cig container sprite, dark grey), same Back/Reset buttons and text colours; buttons grow + tint the same way in both places. | |
| O5 | Controls ▸ **Gamepad** tab with the **mouse**: click a binding. Try once with the pad unplugged, once plugged in. | Hint: "Controller binding: press a button on the pad · ESC to cancel". With no pad: "...connect a pad and press a button...". | |
| O6 | From O5 press **Esc**. Repeat O5 and press **Start** on the pad. | Both cancel; the binding is unchanged. | |
| O7 | From O5 press **B** on the pad. | B gets **bound** (it's a valid pad button, not cancel). Reset to defaults afterwards. | |
| O2 | Controls ▸ **Keyboard** tab, using the **pad**: A on a binding. | Row says "Press a key...". Hint: "Keyboard binding: press a key or mouse button · B to cancel" (CIRCLE on PS). | |
| O3 | From O2 press **B**. Repeat O2 and press **Esc**. | Both cancel: binding unchanged, still on the Controls panel (B doesn't also close it). | |
| O4 | Repeat O2 and press a keyboard key. | It binds (swap rules as before). Reset to defaults afterwards. | |
| O8 | Keyboard tab with the keyboard; Gamepad tab with the pad. | Hints as before: "...· ESC to cancel" / "Press a button to bind · START (MENU/OPTIONS) to cancel". | |
| J16 | Pad. Main menu ▸ Settings: go Down to **Reset to defaults**, press **Right**. Repeat in Controls. (Repeat both from the pause menu in Session 3.) | Focus moves to **Back**; Left returns to Reset. Up from either goes to the last row; Down wraps to the top row. | |
| I3 | Pad. Settings ▸ Gameplay: navigate down to **Psychedelic Blood Intensity**, press Left/Right. | Focus reaches it; each press moves it 5%. | |
| F7 | Pad. Main menu ▸ Settings ▸ **B**. | Back to the main menu. | |

### 1c. Pad hints and cursor (start with the mouse, pad plugged in)

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| E1 | **Unplug the pad.** Open pause (in a dungeon later is fine), Settings, Controls, the main menu. | No hint rows anywhere, no RB glyph. Mouse/keyboard behave as before. | |
| N7 | Restart Play from Boot with only the pad plugged in; don't touch the mouse. First pad press on the main menu. | Hints appear on that first press, not before. | |
| E2 | With the pad in use, open main menu, Settings, Controls (and later pause, shop, death screen). | Each shows its icon + label hint row (Settings/Controls centred under the window, the rest bottom-left). | |
| E2c | Open Settings **and** Controls (main menu and pause): no pad, then Xbox, then PS if you have one. | No "LB / RB or Q / E" text. Either side of the tabs: **Q ◀ … ▶ E** with keyboard/no pad, **LB ◀ … ▶ RB** on Xbox, **L1 ◀ … ▶ R1** on PS. Icons swap live and look crisp. | |
| E3 | Pause menu open with hints showing, **unplug** the pad. Plug it back in, press a button. | Accepted as is: unplugging/plugging alone changes nothing; the next mouse/key input hides the hints, the next pad input shows them. ✅ already |
| N1 | Pad plugged in, open the pause menu with **Esc**. | No hint row (last input was the keyboard). | |
| N2 | From N1, press D-pad down or move the stick. | Hint row appears straight away. | |
| N3 | From N2, nudge the mouse. Then press a pad button. | Mouse: hints vanish straight away. Pad: they're back. | |
| N4 | Same in Settings / Controls (pause and main menu). | Tab glyphs swap live: Q ◀ ▶ E after mouse/keyboard, LB ◀ ▶ RB (L1/R1 on PS) after the pad. | |
| N6 | Unplug the pad while hints show; plug it back in without pressing anything. | Accepted as is: unplugging/plugging alone changes nothing; the next mouse/key input hides the hints, the next pad input shows them. ✅ already |
| N8 | Main menu, pause, slot select: use the pad. | The **mouse cursor disappears** with the first pad input and hints show. Only the pad-selected button is highlighted, even with the hidden cursor resting on another button. | |
| N9 | From N8, nudge the mouse; separately press a key. | Cursor reappears where it was, hints hide, hover highlights work again. | |
| E10 | *(Optional, PS pad)* Repeat E2 with a DualShock/DualSense. | Cross/Circle icons and R1. | |
| E11 | Unassign one hint sprite in the glyph set (`ControllerGlyphSet` asset), open a menu with the pad. Put it back after. | That hint (icon + label) is hidden; **one** warning in the Console, not one per frame. | |

### 1d. Virtual keyboard (pad only unless a case says otherwise)

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| A19 | Main menu ▸ Start (slot select), before picking a slot. | The hint row shows bottom-left. When the name prompt or the delete confirm opens, they cover it. | |
| A20 | On slot select with a used slot: focus its card and press **X**. Repeat with focus on its Delete button, on an **Empty** slot, and on Back. | Used slot: delete confirm opens; No keeps it, Yes empties it. Empty slot / Back: nothing. The hint row shows a 5th hint "Delete" with the X icon. | |
| A1 | Pick an **Empty** slot. | Name prompt opens with the on-screen keyboard. Focus on **Q**. Field shows "Slot N". SHIFT reads **SHIFT ON**. | |
| A2 | Press A on any letter. | "Slot N" is **replaced** by that capital letter; Shift turns off (keys show lowercase). | |
| A3 | Type a few more letters, then press **X**. | Letters are lowercase. X deletes one character. | |
| A4 | Press **Y**. | A space is added. | |
| A5 | Press **BACK** until the field is empty. | Field empties and SHIFT turns back **on**. | |
| A6 | Type until nothing more is added. | Stops at **20** characters. | |
| A7 | Press **CLEAR**. | Field empties, SHIFT on. | |
| A8 | D-pad around the keyboard. SHIFT/SPACE/BACK/CLEAR/DONE are a column on the **right**. | Right off a row's end (or Left off its start) lands on the special key at that height. From a special key, Left → end of the nearest row, Right → its start. Up/Down wrap. Focus never leaves the keyboard; nothing overlaps the name box. | |
| A17 | Move around with the D-pad. | The selected key is **inverted** (white key, dark text); exactly one at a time. A still dips it slightly. | |
| A18 | Look at SPACE / BACK / DONE. | Each shows its shortcut icon on the left (SPACE = Y, BACK = X, DONE = Start; Triangle/Square/Options on PS), label to the right. | |
| A21 | With the prompt open, press **X**. | Backspace only; the slot isn't deleted. | |
| A16 | Keep the stick still and look at the keyboard for a while. | It stays up (no flicker). A jittery mouse on the desk hiding it is expected. | |
| A13 | Move the **mouse**. | Keyboard hides; the text field is focused and you can type on the physical keyboard. | |
| A14 | From A13, press any pad button. | Keyboard comes back with focus on Q; typed text kept. | |
| A11 | Press **B**. | Prompt cancels; back on slot select, nothing created. | |
| A12 | Open the prompt again and press **DONE** without typing. | The slot is named "Slot N". (Then Exit back to the menu.) | |
| A9 | Second empty slot: type "Test", press **DONE**. | Prompt closes, the run starts in that slot; the slot later shows "Test". | |
| A10 | Make another slot and finish with **Start** instead of DONE. | Same as A9. | |
| A15 | Unplug the pad; mouse/keyboard only. Open the prompt. | No on-screen keyboard. The field is focused and Enter confirms. | |

---

## Session 2 — Tutorial (fresh profile)

**Setup:** **Tools ▸ Save ▸ Delete All Saves** (or **Tools ▸ Tutorial ▸
Replay Tutorial On Next New Run**). Play from Boot, Start → an empty slot →
name it. Keyboard + mouse first.

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| G3 | Walk into Room 1 and move with WASD. | The bubble and cat fade in with "**[W/A/S/D]** to move", text inside the bubble. After ~1 s of moving the bubble switches to the switch-weapon step. | |
| G19 | Press your switch-weapon key. | Bubble read "**[key]** to switch weapons" with your real binding. One press switches weapon **and** completes the room: "Nice! Now go to the next room", door opens. Before the press, the door stays shut. | |
| G4 | Plug in the pad and move a stick, then go back to the mouse. | Key names in the hint switch to pad names (e.g. **[RB]**) and back, live. | |
| G5 | Room 3: dash. | "{Dash} to dash" with your dash key; dashing completes it. | |
| G6 | Every room: read the hints. | Text wraps or shrinks and never spills outside the bubble; nothing overlaps the tail. | |
| H4 | Room 2: try to shoot the spawner itself **before** walking fully in. | If it dies before spawning, the room completes once you've fired. No soft-lock. | |
| H6 | Room 4: stomp into the clump. | Many die at once. The room completes only after **all 14** are dead **and** you've stomped once; then the teleporter appears. | |
| K2 | **Replay the tutorial with the pad** (Main menu ▸ TUTORIAL). Rooms 1, 3 and 4. | Hints read "**[RB]** to switch weapons", "**[LT]** to dash", "**[LB]** to blast them all at once" (R1 / L2 / L1 on PS). RB completes Room 1, LB completes Room 4. | |
| H8 | Stop Play. Open `Tutorial.unity` in the Scene view and select a hint zone. | Red lines from Room 2/4's zone to its spawners; the label reads e.g. "Fire + clear room". | |

---

## Session 3 — Dungeons

**Setup:** a slot with the tutorial finished. Play from Boot, Continue or
start a run. Headphones on. Pad and mouse both available.

### 3a. Audio while playing

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| P12 | Listen from the main menu, through the dungeon panel (silent), Skip (music in), shop, next dungeon, boss, back to menu. | Crossfades are smooth: no clicks, no gap, no sudden jump. Tracks loop without an audible seam. Music sits under the SFX. | |
| Q4 | Dash a few times; then try with no charges left, and while standing still. | Dash sound on every real dash; none when no dash happens. | |
| Q5 | Stomp; then try with no charges. | Stomp sound on real stomps only. | |

### 3b. Controls and pause

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| K1 | Pad, in a dungeon: press LT, LB, RB. | LT dashes, LB stomps, RB switches weapon. A and Y no longer stomp or switch. | |
| K5 | Pause → Settings, LB/RB between tabs, then resume. | Tabs switch; on resume you haven't stomped or switched weapon. | |
| F1 | Pad. Pause, press **B**. | Game resumes. | |
| F2 | Pause → Settings → **B**, then B again. | First B: back to the pause menu. Second: gameplay. | |
| F3 | Pause → Controls → B → B. | Same as F2. | |
| F4 | Pause → Exit (warning) → **B**, then B again. | Warning closes, still paused; second B resumes. | |
| F5 | Keyboard: pause with **Esc**, then Esc. | Resumes once and stays resumed. | |
| B1 | Mid-dungeon, pause. | Pause menu shows **Give Up** and **Exit** (no Save & Exit). | |
| B3 | Give Up → answer **No**; again and press **B**. | Dialog closes, still paused, run untouched. | |
| B6 | Give Up, cancel, then pick **Exit**. | The dialog now shows the checkpoint warning; its Yes button has its normal label (not "GIVE UP"). Cancel it. | |
| — | **J16 from the pause menu** (see Session 1). | | |

### 3c. Settings in play

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| I4 | Settings ▸ Psychedelic Mode **on**, Blood Intensity **100%**, kill enemies. | Splatter and floor blood cycle through the rainbow. | |
| I5 | With blood on the floor, pause and drag Blood Intensity to **50%**. | Floor blood shifts live to a muted, red-leaning version; new splatters match. | |
| I6 | Blood Intensity **0%**, mode on. | Normal red blood. | |
| I7 | Psychedelic Mode **off**, any intensity. | Normal blood; the slider has no effect. | |
| I8 | Damage Flash **0%**, take a hit. | No red vignette pulse (sprite blink, hitstop and shake still happen). | |
| I9 | Damage Flash **100%**, take a hit. | Red vignette pulses twice. | |

### 3d. HUD, walls, teleporter, difficulty

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| X3 | Stomp and watch the stomp HUD. If you have Chain Stomp, stomp twice. | Cooldown fill empties and refills over the cooldown; Chain Stomp pips drop and refill one by one. | |
| X5 | Fire until the clip runs low, reload, switch weapons. Repeat in the boss arena. | Ammo count updates on every shot, reload and switch, in **both** scenes. | |
| X6 | Run and dash into walls at different angles; stomp enemies standing next to a wall. | Nobody ends up inside a wall; knockback stops at the wall. | |
| X8 | Fight fast enemies, dash near thin walls. | Nothing tunnels through a wall. | |
| N10 | Gameplay with the pad, then the mouse. | No OS cursor either way; crosshair unchanged. | |
| V1 | Find the teleporter in a dark room. | Glowing cyan disc (~1.6 units) turning slowly, two pixel rings orbiting opposite ways, gold sparks, pixels drifting up, flickering cyan light on the floor. | |
| V2 | Walk into the teleporter and watch it while the 1 s teleport runs. (Requires Key is off on the dungeon teleporter, so entering always teleports. To see the in-range "charged" state, tick Requires Key on the prefab temporarily and walk in without the key.) | It spins up hard, the rings speed up, particles and light intensify. In the Requires-Key test: it spins up while you stand in it and settles back when you leave. | |
| V4 | Stand next to it. | Size looks right next to the player; pixel squares look crisp, not blurry. | |
| V7 | Kill enemies near it so blood lands around it; find one near a wall. | Particles draw above floor blood and below walls; the light doesn't wash out the room. | |
| T7 | Play dungeons 1–3 normally. | Noticeably easier than before (slower waves) but still a fight; the 120 s timer is beatable. | |
| E2b | With the pad: die once; and start a new dungeon so the instructions panel shows. | Each shows **one** hint only: A / Cross "Select". | |

---

## Session 4 — Shop

**Setup:** in a run, clear a dungeon so the shop opens. Have some coins
(Boot ▸ `[Persistent]` ▸ GameSession ▸ Starting Coins can be raised for a
test run; put it back to 50). Pad first, then mouse.

### 4a. Pad

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| J1 | From any offer card (or Buy) press **Left**. | Focus lands on the **cat**; the "Pet" tooltip appears in the centre of the cat panel. | |
| J2 | Press **A** a few times on the cat. | Each press: the cat hops and cycles its pet frames; the bubble says a pet line. Tooltip stays put. | |
| J3 | From the cat press **Right**. | Back to the selected card; none selected → first card; all bought → bottom row. | |
| J5 | Select an offer (A), then press **B**. | Highlight goes, Buy greys out, detail text resets to "Select a cig to see its details." | |
| J6 | Select an offer, move focus to **Buy**, press **B**. | Selection clears; focus jumps back to that card. | |
| J7 | Select a pack cig (lifts + highlight), press **B**. Then select one, move to **Burn**, press **B**. | Both clear; from Burn, focus returns to the cig. | |
| J8 | Select an offer, then move focus around without pressing A. | Selection stays; only B or A elsewhere changes it. | |
| J10 | Trigger a brand-conflict buy (the replace popup). Press **B**. Trigger it again and press **A** at once. Trigger again and choose **Replace**. | B: popup closes, nothing bought, focus back on Buy, detail restored. Immediate A hits **Cancel** (default focus). Replace swaps the cig and charges once. | |
| E6 | With the replace popup open, press **RB**. | Nothing; the popup stays with focus on it. | |
| E4 | Press **RB**. Then quit to the menu and Continue the slot. | Same as clicking Continue. The slot resumes at the **start of that next level**. | |
| E5 | In a shop: pause ▸ Settings, press **RB**. Back out to the pause menu and press RB. | RB switches Settings tabs; the shop doesn't continue either time. | |
| E7 | Hold **RB** through the transition into the next level. | Nothing fires on spawn; the player stands still. | |
| E8 | In a shop, press RB repeatedly fast. | Only one Continue; no double level skip. | |
| E9 | Open Settings over the shop with the pad. | Two hint rows (shop's dimmed + Settings'). Expected; note if it bothers you. | |
| K4 | Press RB once. | Continues only; the weapon doesn't switch. | |
| F6 | Pause over the shop → **B**. | Back to the shop, still usable. | |
| M2 | Pause ▸ Save & Exit in the shop, Continue the slot, and use the shop with the pad straight away. | Focus is on the shop (not an invisible Skip); the cat animates; buying works. | |
| N5 | Use the pad, then the mouse. | The RB glyph by Continue shows after pad use, hides after mouse use; RB continues either way. | |

### 4b. Pause over the shop

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| L1 | Press **Esc**; separately **Start** on the pad. | Pause menu, its dark overlay and buttons draw **on top** of the shop. | |
| L2 | From L1, go through Save & Exit, Give Up (confirm), Settings, Controls (cancel out of each). | Each shows above the shop and takes clicks; nothing on the shop reacts underneath. | |
| L3 | Close pause. | Shop works as before (focus, buying, cat, Continue). | |
| C6 | Open pause over the shop and close it again a few times. | Focus, buying and Continue still work afterwards. | |
| L6 | Pause in the shop; look at **Save & Exit**, hover it with the mouse, select it with the pad. | Same sprite and 30 pt label as the other pause buttons; grows 1.15× with the reddish tint. | |
| L4 | Pause mid-dungeon (not in the shop), open Settings / Controls. | No visual change from before. | |
| L5 | In the tutorial, pause and open Settings. | Shows normally. | |

### 4c. Mouse and look

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| J4 | Hover the cat, then click it. | Tooltip follows the cursor; click pets. | |
| J9 | Select an offer with the mouse, press **Esc**. | Pause opens; the selection is **not** cleared. | |
| J11 | Hover a pack cig, then move off. | Hover: border **and** lift. Off: both go. | |
| J12 | Hover the **bottom edge** of a pack cig and hold still. | No flicker / bounce. | |
| J13 | Click a pack cig, move away. | Highlight shows and it stays lifted; Burn still works. | |
| J14 | Mouse, then pad, over every button in pause, quit warning, Settings, Controls, death, victory and tutorial Skip. | Same look either way: grows 1.15× with a reddish tint, no darkening on the pad, press flashes light red, greyed buttons don't react. | |
| J15 | Same over the shop's Buy / Burn / Reshuffle / Continue, cards and pack cigs. | Buttons grow 1.1× with a light reddish tint, same on mouse and pad; cards and cigs keep their own hover. | |
| X4 | Burn a cig, Continue, and watch its burn bar through the next dungeon. | The bar shows that cig's art and burns down with the timer to ~65% (filter stays), with a pixel cherry, embers and ash at the burning end. Gone after that dungeon. | |

---

## Session 5 — Editor-only check

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| D10 | Set the Enemy Spawner prefab's Placement to **Center** (or Interior), select an instance in the Scene view. **Put it back to Corner after.** | A cyan square gizmo shows the wall-clearance area. | |

---

## Session 6 — Windows build and Steam

**Setup:** File ▸ Build Profiles ▸ Windows, **Development Build off**, build
to a fresh folder. S14–S16 **fail today** until the three release fixes in
`RemainingBeforeDemo.md` #1–3 are done; build after those.

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| S1 | Copy the whole build folder to another PC without Unity (or at least another drive), run the exe. | Starts to the main menu; no error dialogs or missing-DLL messages. | |
| S2 | Before launching, rename `%USERPROFILE%\AppData\LocalLow\ElderMonkInteractive\OneBitKill(Beta)` (rename it back after). | A true first launch works: new run goes through the **tutorial**; the save folder and `settings.json` get created. | |
| S3 | Play start to finish: tutorial → 3 dungeons + shops → boss → Demo Complete. | Nothing blocks progress; Demo Complete makes clear the demo is over and returns to the menu. | |
| S4 | Repeat S3 **pad only**, launch to quit, never touching mouse/keyboard (slot naming, settings, credits, every menu, QUIT). | Possible end to end with Xbox prompts everywhere. | |
| S5 | From the main menu press **QUIT**, once with the mouse and once with the pad. | Closes cleanly, no hang or error. | |
| S6 | Mid-dungeon press **Alt-F4**, relaunch, Continue. Repeat mid-shop. | Closes without hanging; the slot loads at its last checkpoint; no corrupted save. | |
| S7 | Fullscreen: Alt-Tab out mid-fight ~10 s, then back. Also minimize and restore. | No black screen, wrong resolution or lost input. While out the game is **frozen** (no damage, timer stopped); it resumes on return. | |
| S8 | Run at 1280×720, 1920×1080, 2560×1440 and an ultrawide if possible, windowed and fullscreen. | HUD, menus, shop, credits and tooltips fit and stay readable. | |
| S9 | Once the demo app exists, launch **through Steam** and press **Shift+Tab**. | Overlay opens and closes; the game doesn't take its clicks or keys. | |
| S10 | Through Steam: Xbox pad, then a PS pad (Steam Input on). | Both work. A PS pad showing Xbox glyphs is the known Steam Input caveat (`SteamReleaseNotes.md` §4). | |
| S11 | Use every Settings option and Controls rebinding, then relaunch. | All work in the build and are kept after relaunch. | |
| S12 | Leave the build idle on the main menu, and paused mid-run, 10+ minutes each; watch Task Manager. | Memory flat; no crash or audio glitches (music keeps looping). | |
| S13 | Compare the store page's listed features with the build. | Everything listed is really there (English only, single-player, controller support as declared). | |
| S14 | Mid-dungeon press **I**, then take damage. | No god mode. | |
| S15 | Press **H**. | Nothing happens (no test flash / hitstop). | |
| S16 | Look at the dungeon HUD. | No debug buttons. | |

---

## Session 7 — Re-check of the first pass's fixes (2026-10-08)

Do this one first. Mute When Unfocused (P13) is gone; the hint unplug
behaviour (E3, N6) was accepted as is.

| ID | Steps | Expected | ✓ |
|---|---|---|---|
| Y1 | Open each confirm popup: slot **Delete** (main menu), pause ▸ **Main Menu** and pause ▸ **Give Up** (dungeon), the shop's **brand conflict** (buy a Mild with a Regular held, or the reverse). | Each box has a thin white border; its buttons look like the main menu's (bordered grey bar, black pixel-font capitals). Nothing overlaps or is cut off. | |
| Y2 | Same popups on the pad. | Focus starts on the safe button (CANCEL / NO); Left/Right swaps; the focused one grows. B cancels. | |
| Y3 | Controls ▸ both tabs: hover bindings with the mouse, move through them with the pad, start a rebind. | Focused binding is **white with black text**; the rest dark grey with white text. Stays white during "Press a key...", back to normal after. Greyed-out bindings never highlight. | |
| Y4 | Settings ▸ Audio. | No Mute When Unfocused row, no gap; pad up/down runs through the sliders to the footer. | |
| Y5 | Alt-Tab / click outside with music playing. | Sound keeps playing. | |
| Y6 | Kill the boss. | **DEMO COMPLETE** title, thanks + wishlist message, kills/coins/dungeons, a main-menu-style **MAIN MENU** button. Nothing overlaps. | |
| Y7 | Y6 on the pad only. | MAIN MENU already selected. Mash the D-pad / stick first: focus stays on it (no softlock). Then A goes to the main menu. | |
| Y8 | Tutorial and boss arena: look at the HUD; in the boss arena have a burning cig and held cigs from the last shop, fire and reload. | Same layout as the dungeon HUD (cig pack row, burning bar, ammo count, coins/stomp). Ammo updates; the burning bar shows if one is burning.  | |
