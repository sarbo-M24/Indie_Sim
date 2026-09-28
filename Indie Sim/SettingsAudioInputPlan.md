# Settings Menu, Sound Manager, Input Manager & Controller Support — Plan

> **Ground rules (from `UpgradeSystemSpec.md` / `shop-ui-mascot-and-cards.md`):** Claude writes only C# scripts. The user does scenes, prefabs, the mixer asset and the `.inputactions` asset by hand, following numbered checklists. No new packages. No new `DontDestroyOnLoad`: global services are parented under the `[Persistent]` root in `Boot.unity`. UI animations use unscaled time.
>
> **Exception for the input work (2026-09-28):** Sarbo asked Claude to do the input side end to end. So Claude edited `PlayerControls.inputactions` and `InputSystem.inputsettings.asset` directly. Every other scene and prefab change is done at runtime from code, so no `.unity`/`.prefab` files were touched. Sarbo tests all scenes in Play mode; Claude only recompiles and checks for errors.

**Status (2026-09-28) — Input half of Phase A mostly done: gamepad bindings, one shared `PlayerControls`, all gameplay/shop/pause/death/countdown input on the new Input System, gamepad-navigable menus. Committed (`4b9d9b9`). Settings/pause/audio backend now written (below); Settings panel UI and rebinding not started.**

**Done:**
- **`PlayerControls.inputactions`:**
  - Control schemes `Keyboard&Mouse` and `Gamepad`.
  - Player map adds `Aim` (right stick, `StickDeadzone(min=0.2)`) and `Reload` (R / X).
  - Gamepad bindings on existing actions: Move = left stick (`StickDeadzone(min=0.15)`), Fire = RT, **Dash = LT** (not "LT or RB"), Stomp = A, SwitchWeapon = Y.
  - **Weapon switching is one action (2026-09-28):** `SwitchWeaponScroll` was merged into `SwitchWeapon`, which always goes to the next weapon. Defaults are **Mouse Wheel** (`<Mouse>/scroll/y`, either direction) and **Y**. Tab and LB/RB no longer switch weapons. `WeaponInventory.SwitchToPreviousWeapon` and `scrollThreshold` were removed. The HUD key hint `Controls (2)` now reads "Scroll".
  - New **UI map**: Navigate, Submit, Cancel, Point, Click, RightClick, MiddleClick, ScrollWheel and **Pause (Esc / Start)**.
  - The "empty-path Stomp binding" from the plan was already fine (Space).
- **`Scripts/Input/InputManager.cs`:** a **static class**, not the planned `InputManager.Instance` MonoBehaviour under `[Persistent]`, so it needs no Boot setup.
  - `InputManager.Controls` is the one shared `PlayerControls` instance.
  - The 5 old `new PlayerControls()` owners now subscribe in `OnEnable` and unsubscribe in `OnDisable`, so a destroyed player can't leave callbacks behind.
  - Per-source `SetPlayerBlocked(source, bool)` turns the Player map off while the shop, pause, death screen or countdown is open.
  - Also provides `UsingGamepad` (last device used), `PointerPosition` and `TryGetGamepadAim()` (holds the last stick direction).
  - On every scene load it points each `InputSystemUIInputModule` at this asset's UI map. Those modules used Unity's `DefaultInputActions` before, so UI bindings can now be rebound too.
  - A future `SettingsService` should apply binding overrides to `InputManager.Controls`.
- **Pause lives in the UI map, not the Player map.** The UI map is always on, so the same action can resume while the Player map is off.
- **Legacy `Input.*` removed from gameplay:**
  - `OptionsMenu` Esc is now the Pause action.
  - `WeaponAmmoManager` R is now the Reload action.
  - `PlayerConeShooter`: mouse aim now reads the pointer through the Input System, and the legacy fire fallback is deleted.
  - `ShopUIController` clicks go through the UI map.
  - `CursorTooltip`, `CustomCrosshair` and `CameraLead` read the pointer through the Input System.
  - **Still legacy:** the debug keys (`PlayerHealth`, `UpgradeDebugHUD`, `DamageIndicator`, `PsychedelicBloodController`) and `SimpleTouch`. The Debug map hasn't been made.
- **Unified aim (basic):**
  - `SimplePlayerRotation`, `PlayerConeShooter`, `CameraLead` and `CustomCrosshair` use the right stick while the gamepad is the active device.
  - Camera lead on the pad is full lead along the aim direction and holds after release. The tilt curve, the ease toward movement direction after 1.5 s, and `gamepadSmoothSpeed` are **not done**.
  - The crosshair sits `gamepadReticleDistance` (4) along the aim, and hides whenever the Player map is off.
- **Gamepad menus:**
  - `Scripts/UI/UIFocus.cs`: auto-selects on gamepad, and `LinkVertical` builds explicit top-to-bottom navigation.
  - `Scripts/UI/GamepadMenuPanel.cs`: added at runtime to the pause panel, the death panel and the countdown panel. While the panel is open it blocks the Player map, shows the cursor, links and selects the buttons, and adds the grow effect.
  - `Scripts/UI/ButtonFocusScale.cs`: hover/focus grow on buttons.
  - `CursorController` gained a per-source `SetCursorOverride(source, bool)`. The old bool overload still works.
- **Shop on gamepad:**
  - Selection stands in for hover: focus, tooltip and stat preview all work.
  - The tooltip anchors to the focused card on the pad.
  - Explicit navigation is rebuilt every frame and skips hidden or disabled items. It had to be explicit because the pack cigs are rotated ~180° in the scene, and Unity's automatic navigation turned Up into Down.
  - Pack cigs get a focus border (`PackCigView.SetFocused`).
  - Buy, Burn, Reshuffle and Continue grow on focus.
  - After a buy or burn, focus moves to a sensible next spot.
- **Bugs fixed along the way:**
  - **Pads not detected at all:** Input System **Supported Devices** was `AndroidGamepad, Joystick, Keyboard, Mouse`, which filtered out XInput/DualShock pads. Added `Gamepad`.
  - **Machine gun silent after Retry:** `TutorialManager` disables `PlayerConeShooter`, and when its `Start` ran before `WeaponInventory`'s, the shooter missed the one-time starting-weapon event. The shooter now re-syncs its weapon in `OnEnable`.
  - Esc/Start is now ignored while something else has frozen the game (death screen, countdown). Before, pausing over the death screen and resuming would unfreeze the game behind it.

**Settings / pause / audio backend (2026-09-28, compiles, awaiting test):**
- **`Scripts/Settings/GameSettings.cs` + `SettingsService.cs`:** a **static** service, like `InputManager`, so there's no Boot object.
  - `SettingsService.Current`, `Change(s => s.field = v)` (raises `OnChanged`), `Save()`, `ResetAudio()` / `ResetGameplay()`.
  - `settings.json` is written through `settings.json.tmp` + `File.Replace`. A corrupt file falls back to defaults. The file is also saved on quit.
  - Migrates the old `SoundEnabled` PlayerPref (0 → master 0) once, then deletes the key.
  - Phase A fields only: audio (master/music/sfx/ui, muteWhenUnfocused), gameplay (damageNumbers, psychedelicMode, screenShake, cameraLead, flashIntensity), `bindingOverridesJson`.
- **`Scripts/Managers/PauseController.cs`:** a static class and the **only writer of `Time.timeScale`**.
  - `SetFrozen(source, bool)` works per source. Hit-stop is `BeginHitStop(scale)` / `EndHitStop()` and never overrides a freeze. `ResetAll()` clears both. It resets itself on every Single scene load. `OnFrozenChanged` fires when the game freezes or resumes.
  - **Every** old writer moved over, not just the pause menu and `DamageIndicator`: `OptionsMenu`, `DamageIndicator` (fixes the unpause-during-hit-stop bug), `DemoCompleteScreen`, `TutorialManager`, `PlayerHealth`, `StatTracker`, `RetryButton`, `GameManager`, `RoguelikeManager`. `ShopUIController` and the Esc guard now read `PauseController.IsFrozen`.
- **`Scripts/Audio/AudioManager.cs`:** static. It loads **`Assets/Resources/MainMixer.mixer`**, so the mixer goes in `Resources/`, not `Assets/Audio/`.
  - Applies `20·log10` dB volumes on every change, on focus change (mute when unfocused) and on every scene load.
  - On scene load it routes scene AudioSources that have no output group to SFX.
  - `MusicManager` routes ambient/power → Music and teleporter/death → SFX.
  - With no mixer, only Master works, through `AudioListener.volume`.
- **`OptionsMenu`:** the `AudioListener.volume` hack and the dead mixer/AudioSource fields are gone. The existing Sound button now toggles Master volume 0 ↔ previous through `SettingsService`, until the sliders exist.
- **Live setting listeners:**
  - `DamageNumberManager.Spawn` is gated by `damageNumbers`.
  - `CameraShake` and `CameraLead`'s recoil/shake offset are scaled by `screenShake`. `CameraLead`'s lead is scaled by `cameraLead`.
  - `PsychedelicBloodController` follows `psychedelicMode`. Its P key now works only in editor and dev builds, and the `enableToggleInBuild` field was removed.
  - `DamageIndicator`'s flash colour and intensity blend toward normal by `flashIntensity`.
- `InputManager` loads `bindingOverridesJson` when it creates the controls.
- **Bug found in test, then fixed: soft-lock when a hit killed the player.** `StatTracker.ShowDeathStats()` froze time the moment the player died, before `PlayerHealth`'s scaled-time `deathDelay`, so the death panel never appeared and Esc was ignored. Before, it only worked by accident: the hit-stop put back its saved `timeScale = 1` 0.1 s later. StatTracker no longer freezes, and `PlayerHealth` freezes when the panel shows.

**Manual checklist (Sarbo): mixer**
1. Create `Assets/Resources/MainMixer.mixer` (Project window → Create → Audio Mixer). It **must** be in `Resources/` and named exactly `MainMixer`.
2. In the Audio Mixer window, under `Master` add child groups named exactly `Music`, `SFX` and `UI`.
3. For each of the 4 groups: select it, right-click **Volume** in the Inspector → *Expose "Volume" to script*. Then in the window's **Exposed Parameters** dropdown, rename them to `MasterVol`, `MusicVol`, `SfxVol` and `UiVol`.
4. Set the AudioSource **Output** to `SFX` on these prefabs: `Prefabs/Enemies/Enemy.prefab`, `Enemy 1`, `Enemy 2`, `Enemy 3`, `Prefabs/Teleporter.prefab`, `Prefabs/Temp -Player.prefab`. Scene-baked sources are routed automatically.
5. Play: the console should **not** show "[AudioManager] No Resources/MainMixer.mixer yet".

**Test (backend):** take damage and press Esc/Start during the hit-stop. The game must stay paused. Also check the pause → Retry and pause → Main Menu flows, the death screen then Retry, the countdown skip, demo complete → main menu, and that the shop still works after resuming from pause. The Sound button mutes and unmutes and survives a restart.

**Settings panel scripts (2026-09-28, compile clean, prefab not built yet):**
- `Scripts/UI/Settings/SettingBindings.cs`: the `FloatSetting` / `BoolSetting` enums map to `GameSettings` fields. Each row picks its setting from a dropdown.
- `SliderRow`:
  - Slider + optional `%` readout.
  - Runs in whole steps (20 = 5% per D-pad press; holding repeats).
  - Min, Max and Whole Numbers are overwritten at runtime.
- `ToggleRow`: Toggle + optional ON/OFF readout.
- `SettingsPanel`:
  - Tabs (Audio/Gameplay) switch with LB/RB or Q/E, or by clicking the tab buttons.
  - Up/Down walks the current tab's rows, then Reset, then Back.
  - B/Esc/Start closes it and saves `settings.json`.
  - While open it blocks the Player map and shows the cursor.
  - `BlocksPauseInput` stops the Esc that closes Settings from also resuming the game.
- `OptionsMenu.OpenSettings()` and `MainMenu.OpenSettings()` hide their own buttons while Settings is open, then give focus back to the Settings button.
- `MainMenu` gains a `menuButtons` field, which also gets `GamepadMenuPanel`, so the main menu now works on a pad.
- `UIFocus.LinkInOrder(list)`: a new helper.
- **CycleRow, ScrollToSelected, and UI sounds are not built.** Phase A's two tabs need none of them.

**Built by Claude (2026-09-28), steps 1–4 below are DONE.** Sarbo asked Claude to build and wire the prefab and scenes; it was built by a one-off editor script. It uses placeholder art (flat greys, no sprites), and Sarbo adds the images later.
- **`Prefabs/UI/Settings Panel.prefab`:** built as in the tree below and saved inactive.
  - Each row's background is its focus highlight: the Slider/Toggle tints it from transparent to faint white.
  - Its window has a white `Outline` border.
- **`Player Canvas RoguelikeMode.prefab`:**
  - A new `Options Panel/Settings` button (a copy of Resume) at (0, −125) → `OptionsMenu.OpenSettings`. `Exit` moved to (0, −240).
  - A Settings Panel instance is the canvas's last child, assigned to `OptionsMenu.settingsPanel`.
  - Verified in RoguelikeMode and BossArena, with no scene overrides.
- **`Main menu.unity`:**
  - `Button Panel` and `Achievemnt btn` are now grouped under a new stretch `Menu Buttons` object.
  - A new `Settings btn` (a copy of the achievement button: grey, no sprite, "SETTINGS" text) sits 170 px left of it → `MainMenu.OpenSettings`.
  - A Settings Panel instance is the canvas's last child. `MainMenu.menuButtons` and `settingsPanel` are assigned.
  - The EventSystem now uses `InputSystemUIInputModule` (actions = `PlayerControls`).
- **`Store Scene.unity`:** the EventSystem now uses `InputSystemUIInputModule`.
- **Code changes that came with the build:**
  - `GamepadMenuPanel` now keeps a stack of open panels, and only the most recently opened one holds focus. Without this, the main menu would steal focus from the Achievements panel.
  - `AchievementMenuController` adds `GamepadMenuPanel` to the Achievements panel.

**Controls (key bindings) panel (2026-09-28, built by Claude; compiles, not yet play-tested):**
- **Opening it:** it's a separate panel from Settings. The pause menu (`Options Panel`) has a dedicated **Controls** button → `OptionsMenu.OpenControls`. The main menu has no Controls button, as asked.
  - **Main menu (2026-09-28):** added a `Controls btn` → `MainMenu.OpenControls`, plus a Controls Panel instance as the canvas's last child. `MainMenu` gained a `controlsPanel` field and a shared `OpenSubMenu`.
  - Settings and Controls now stack in the right-edge column above Achievements: Achievements at y 116, Settings at 276, Controls at 436 (bottom-right anchor).
  - **Fix:** the Settings button used to sit under the Help Panel, which draws on top and was blocking clicks on it.
  - Pause layout: Resume 0, Settings −115, Controls −220, Main Menu −320.
- **`Scripts/UI/TabbedMenuPanel.cs`:** new shared base class for the tabbed panels.
  - `SettingsPanel` and `ControlsPanel` both derive from it. It holds the tabs, Back, Reset, LB/RB tab switching, the Player-map block and the cursor.
  - It keeps the selected row visible inside a `ScrollRect`.
  - `BlocksPauseInput` moved here.
  - The serialized field names are unchanged, so the existing Settings Panel prefab still loads its wiring.
- **Reset to defaults is now per row:** it resets every row on the current tab. `GameSettings.ResetAudio/ResetGameplay` were removed; `SettingBindings.GetDefault` replaces them.
- **`Scripts/UI/Controls/`:**
  - **`RebindRow`:** targets a binding by action, group and composite part. A fixed row keeps its button non-interactable.
  - **`BindingLabels`:** shows Xbox names by default, and PlayStation names when a DualShock/DualSense is connected.
  - **`ControlsPanel`:** runs `PerformInteractiveRebinding`, restricted by device.
    - Esc or Start cancels.
    - If the key is already used on the same tab, the two bindings swap.
    - The UI map is off while it waits for a key.
    - Overrides are saved to `bindingOverridesJson`, and `InputManager` loads them at startup.
- **`Prefabs/UI/Controls Panel.prefab`:** "KEYBOARD & MOUSE" and "GAMEPAD" tabs, each a scroll list with a slim scrollbar. Placeholder art.
  - **Keyboard & Mouse:** Move Up/Down/Left/Right, Fire, Dash, Stomp, Reload and Switch Weapon (Mouse Wheel).
  - **Gamepad:** Fire, Dash, Stomp, Reload and Switch Weapon (Y).
  - Every row can be rebound; there are no fixed or greyed-out rows. Move and Aim on the sticks, and Pause, aren't listed.
  - **Swap Sticks (Move / Aim)** toggle at the bottom of the Gamepad tab (2026-09-28).
    - The setting `GameSettings.swapSticks` is shown with a `ToggleRow` (`BoolSetting.SwapSticks`).
    - `InputManager.ApplyStickSwap` applies it as overrides on the gamepad bindings of Move and Aim, so Move reads the right stick and Aim the left. It runs at startup after the saved overrides, and again on every settings change.
    - The setting always wins over any stick override saved in `bindingOverridesJson`.
    - Each action keeps its own dead zone (Move 0.15, Aim 0.2), and menus still navigate with the left stick.
    - The Gamepad tab's Reset to defaults turns it off.
  - On the keyboard tab, scrolling the wheel while it's listening binds the wheel. The listener only accepts buttons, so `ControlsPanel` catches the wheel itself.
- **Test:**
  - Pause → Controls, then LB/RB between tabs.
  - Rebind Dash to Space: Stomp should take Shift (the swap).
  - Esc or Start cancels a rebind without closing the panel.
  - Reset to defaults restores the tab.
  - B or Esc goes back to the pause menu.
  - A rebind survives restarting Play mode.
  - On the keyboard tab, a pad user scrolls down to see the fixed rows.

### Settings panel — setup checklist (reference; done by Claude, see above)

**1. Build the prefab.** Do it in `Main menu.unity` under its Canvas, then drag it to `Assets/Prefabs/UI/Settings Panel.prefab`.
```
Settings Panel            ← full-screen Image (bg), SettingsPanel component. Save the prefab INACTIVE.
  Header
    Title                 TMP "SETTINGS"
    Tabs                  Horizontal Layout Group
      Audio Tab           Button + TMP "AUDIO"
        Marker            Image (underline / invert block), shown only on the active tab
      Gameplay Tab        Button + TMP "GAMEPLAY"
        Marker
    Hint                  TMP "LB / RB"  (optional)
  Body
    Audio Content         Vertical Layout Group (spacing ~12)
      Master Row          SliderRow  (Setting = MasterVolume)
        Label             TMP "Master"   (LayoutElement preferred width ~300)
        Slider            UI ▸ Slider
        Value             TMP "100%"
      Music Row           SliderRow  (MusicVolume)
      SFX Row             SliderRow  (SfxVolume)
      UI Row              SliderRow  (UiVolume)
      Mute Row            ToggleRow  (MuteWhenUnfocused)
        Label             TMP "Mute when unfocused"
        Toggle            UI ▸ Toggle (delete its built-in Label child)
        State             TMP "OFF"
    Gameplay Content      Vertical Layout Group, same row style
      Damage Numbers      ToggleRow  (DamageNumbers)
      Psychedelic Mode    ToggleRow  (PsychedelicMode)
      Screen Shake        SliderRow  (ScreenShake)
      Camera Lead         SliderRow  (CameraLead)
      Flash Intensity     SliderRow  (FlashIntensity)
  Footer
    Reset Button          Button + TMP "RESET TO DEFAULTS"
    Back Button           Button + TMP "BACK"
```
- **Each row:** Horizontal Layout Group (Child Alignment Middle Left, Control Child Size on, Child Force Expand width off). On the `SliderRow`/`ToggleRow` component, drag in its Slider/Toggle and value TMP, and pick the **Setting**. Leave **Steps** at 20.
- **Pad focus must be visible:** on every Slider and Toggle, set Transition = Color Tint and give **Selected Color** a clear contrast (e.g. inverted white). Buttons get the grow-on-focus automatically.
- Slider Min/Max/Whole Numbers and every Navigation setting are overwritten at runtime, so leave them alone.
- **`SettingsPanel` component:**
  - Set **Tabs** size to 2.
  - Element 0: Category = Audio, Button = Audio Tab, Content = Audio Content, Active Marker = Audio Tab/Marker.
  - Element 1: the same for Gameplay.
  - Reset Button and Back Button: drag in the footer buttons.
- Don't add `GamepadMenuPanel` to it; the panel does that job itself.

**2. Pause menu:** `Prefabs/UI/Player Canvas RoguelikeMode.prefab`, used by RoguelikeMode and BossArena.
1. Open the prefab. Drag `Settings Panel` in as the **last child of the canvas root**, so it draws above Options Panel.
2. In `Options Panel`, duplicate `Resume` → rename it `Settings` with text "SETTINGS". Place it between Resume and Retry. Its OnClick: remove the copied Resume call, drag in the object that has **OptionsMenu**, and pick `OptionsMenu.OpenSettings`.
3. On **OptionsMenu**, drag the Settings Panel instance into the new **Settings Panel** field.
4. Save. Open RoguelikeMode and BossArena and check that the new button shows. A scene-level override on Options Panel could hide it.

**3. Main menu** (`Main menu.unity`):
1. **EventSystem:** click *Replace with InputSystemUIInputModule* on the Standalone Input Module, or remove it and add Input System UI Input Module. Leave its Actions asset as is; `InputManager` points it at `PlayerControls` at runtime. Without this, the pad can't drive the main menu.
2. Put the `Settings Panel` prefab under the menu Canvas as its last child.
3. Add a `Settings` button next to Play. Its OnClick → `MainMenu.OpenSettings`.
4. On **MainMenu**:
   - **Menu Buttons** = the object that holds the menu's buttons. It hides while Settings is open and gets pad navigation. It must **not** contain the Settings Panel.
   - **Settings Panel** = the instance.
   - If the buttons sit directly on the Canvas, group them under an empty `Menu Buttons` object first.

**4. Store Scene:** do the same EventSystem swap as 3.1.

**5. Test:**
- **Pad:** Start → Settings → LB/RB between tabs.
  - Sliders move 5% per press, and holding repeats.
  - A toggles.
  - Reset restores the current tab's defaults.
  - B returns to the pause menu with Settings focused. B does **not** resume the game; Start from the pause menu does.
- **Esc:** Esc from Settings returns to the pause menu; Esc again resumes.
- **Mouse:** click tabs, drag sliders, click Back.
- **Changes apply live:**
  - Master/Music/SFX sliders change volume (Music/SFX/UI need the mixer checklist above).
  - Damage numbers off → no popups.
  - Psychedelic on → the blood cycles colours.
  - Shake 0% → no shake.
  - Camera lead 0% → the camera stays on the player.
- **Persistence:** quit Play mode and play again; the values stick.
- **Main menu:** everything above, plus pad navigation of the main menu buttons.

**Tested by Sarbo:** gamepad gameplay (move, aim, fire, dash, stomp); shop navigation, highlight and grow.
**Awaiting test:** pause-menu navigation; the death screen (focus on Retry, cursor, crosshair hidden); A to skip the countdown; the machine-gun-after-Retry fix; pausing inside the shop keeping the shop cursor.

**Left in Phase A (input side):**
- Swap `StandaloneInputModule` → `InputSystemUIInputModule` in `Main menu.unity` and `Store Scene.unity`.
- Gamepad support for remaining panels, e.g. `DemoCompleteScreen` and the main menu. `GamepadMenuPanel` fits any simple button-list panel.
- `CursorTooltip` is already anchored for the shop. The OS cursor still shows in the shop on the pad, and the plan's "hide the cursor on gamepad" isn't done.

**Open notes:**
- Unity sees 3 `XInputControllerWindows` devices on Sarbo's machine (a Cosmic Byte Ares in XInput mode, likely plus Steam Input or other virtual copies). It's harmless so far.
- The "Joystick reconnected" console line comes from the legacy Input Manager, because Active Input Handling is still **Both**.
- A "Screen position out of view frustum" error was seen twice during testing. It hasn't been traced, and may come from `CameraLead`/`PlayerConeShooter`'s `ScreenToWorldPoint`.

## Context

The game ("one bit kill") is a 2D top-down roguelike shooter aiming for a Steam demo before IGDC on **Oct 28, 2026**, about 5 weeks from now. It has no real settings screen. `UI/OptionsMenu.cs` is a pause panel with one Sound ON/OFF toggle, and that toggle works by zeroing `AudioListener.volume`. Missing pieces:
- volume sliders
- quality-of-life toggles
- controller support

A Steam demo without full gamepad support and volume sliders reads as a student project. Steam Deck users will try it with a pad.

**What already exists (reuse, don't rebuild):**
| Thing | Where | Status |
|---|---|---|
| Input System package 1.14.2 | `Packages/manifest.json` | Installed. Active Input Handling = **Both** |
| `PlayerControls.inputactions` | `Assets/UnityInputSystem/` | One `Player` map (Move, Look, Fire, Dash, Stomp, SwitchWeapon, SwitchWeaponScroll). **Keyboard and mouse only, no control schemes, no Pause or Reload actions** |
| `new PlayerControls()` | `PlayerController.cs:100`, `PlayerStompController.cs:59`, `PlayerConeShooter.cs:76`, `SimplePlayerRotation.cs:26`, `WeaponInventory.cs:33` | **5 separate instances**. Rebinding or disabling the map on pause can't work this way |
| Legacy `Input.*` still in use | `PlayerConeShooter.cs:119,123,875` (fire fallback + **mouse aim**), `CameraLead.cs:416`, `CustomCrosshair.cs:115`, `CursorTooltip.cs:172`, `OptionsMenu.cs:277` (Esc), `WeaponAmmoManager.cs:168` (R reload), debug keys in `PlayerHealth.cs:139`, `UpgradeDebugHUD.cs:66`, `DamageIndicator.cs:240`, `PsychedelicBloodController.cs:132` | Needs migrating |
| Music | `Managers/MusicManager.cs` | Singleton that creates 5 AudioSources at runtime (lines 101–131) |
| SFX | about 10 components with their own `AudioSource.PlayOneShot` (Enemy, EnemyDeath, EnemySpawner, BossEnemy, WeaponVFXHandler, WeaponAmmoManager, Teleporter, PlayerKeyManagement) | No mixer asset, no SFX manager |
| Psychedelic mode | `Enemy/PsychedelicBloodController.cs` | **Already built.** Has a `SetActive(bool)` API (line 187) and a P debug key. Only needs wiring |
| Damage numbers | `UI/DamageNumberManager.cs` `Spawn()`. Callers: `PlayerConeShooter.cs:913`, `PlayerStompController.cs:247` | One gate in `Spawn()` turns them all off |
| Screen shake | `CameraTouch/CameraShake.cs` `ShakeCamera(intensity, duration)`, plus shake inside `CameraLead.cs:239,352` | Scale it with a setting |
| Hit-stop / damage flash | `UI/DamageIndicator.cs:127–145` (vignette via Volume) | ⚠️ Restores the old `timeScale`, so it **unpauses the game** if you pause during a hit-stop |
| JSON save | `Managers/SaveSystem.cs` (`persistentDataPath` + `JsonUtility`) | Settings follow the same pattern in a separate file |
| EventSystem UI modules | `InputSystemUIInputModule` in RoguelikeMode and BossArena. **Legacy `StandaloneInputModule`** in `Main menu.unity` and `Store Scene.unity` | Must switch before a pad can drive the menus |
| Camera | Perspective + Cinemachine 3 | The screen-to-world aim math in `PlayerConeShooter.GetMouseAimDirection()` (867–881) must be kept |

---

## Architecture

Three new services sit under `[Persistent]` in `Boot.unity`, using the project's `static Instance` + `Awake` duplicate-guard pattern:

```
SettingsService ──OnChanged──► AudioManager (mixer volumes)
      │                     ├─► InputManager (rebinds, deadzone, aim assist, rumble)
      │                     └─► scene listeners (DamageNumberManager, CameraShake,
      │                          PsychedelicBloodController, DamageIndicator, CRT…)
      ▼
settings.json  (persistentDataPath, separate from the save file)

PauseController ── the only owner of Time.timeScale for pause and hit-stop
```

### 1. `SettingsService` + `GameSettings` (new, `Scripts/Settings/`)
- `GameSettings` is a `[Serializable]` plain class holding every value with its default, plus a `version` int for migration.
  - **Audio:** master, music, sfx, ui (0–1 each); `muteWhenUnfocused`.
  - **Video:** `fullscreenMode`, resolution index, `vSync`, `targetFps`, `screenShake` (0–1), `flashIntensity` (0–1), `crtFilter`, `showFps`.
  - **Gameplay:** `damageNumbers`, `psychedelicMode`, `cameraLead` (0–1), `crosshairStyle`.
  - **Controls:** `bindingOverridesJson`, `aimAssist` (0–1), `stickDeadzone`, `rumble` (0–1), `autoFireOnAim`.
- `SettingsService.Instance` holds `Current`, plus:
  - `Set<T>(ref field, value)` updates a value and raises `OnChanged`.
  - `Save()` writes the file safely: write `settings.tmp`, then `File.Replace` / move over the old file.
  - `Load()` falls back to defaults if the file is corrupt.
  - `ResetCategory(cat)` restores one category's defaults.
- Changes apply **live** and are saved when the panel closes, not on every slider tick.
- Migrates the old `SoundEnabled` PlayerPref once, then deletes the key.

### 2. Sound: `AudioManager` + `SfxCue` (new, `Scripts/Audio/`)
- **Mixer asset (manual):** `Assets/Audio/MainMixer.mixer`.
  - Groups: `Master` → `Music`, `SFX`, `UI`.
  - Exposed volume params: `MasterVol`, `MusicVol`, `SfxVol`, `UiVol`.
  - Add a Lowpass on Music and SFX.
  - Snapshots: `Default` and `Paused` (lowpass around 800 Hz, SFX −6 dB).
- **Volume:** `AudioManager` converts slider values to decibels with `20·log10(max(v, 0.0001))` and applies them to the exposed params.
- **Pause:** on pause it transitions to the `Paused` snapshot over 0.2 s. The game sounds muffled while paused, which is a cheap, high-impact touch.
- **Unfocused:** if `muteWhenUnfocused` is on, `OnApplicationFocus` mutes Master.
- **`SfxCue` ScriptableObject** (asset made by hand) fields:
  - `clips[]` (random pick)
  - `volume` and a `pitchRange` (e.g. 0.95–1.05, so repeated gunfire doesn't sound robotic)
  - `mixerGroup`
  - `maxVoices` and `minInterval` (so shotgun pellets hitting 6 enemies don't play 6 copies of the same sound in one frame)
  - `spatial` flag
- **Playback API:**
  - `AudioManager.Play(SfxCue cue, Vector3? pos = null)` plays from a pool of about 24 AudioSources, reused round-robin, with per-cue voice limiting.
  - `PlayUi(SfxCue)` is for button hover, select, back and slider ticks.
- **Migration in two tiers (keeps the risk low before the demo):**
  1. **Must-have:** route every existing AudioSource to the `SFX` group. For `MusicManager` that's code: set `outputAudioMixerGroup` where it creates its sources (lines 101–131). For prefab AudioSources it's manual: set the Output field. Sliders then work with **no rewrite of the SFX calls**.
  2. **Polish:** move the most frequent sounds to `AudioManager.Play(cue)`: gunshot (`WeaponVFXHandler.cs:350`), enemy hit and death (`Enemy.cs:181`, `EnemyDeath.cs:67`), boss (`BossEnemy.cs`). They gain pitch variation and voice limiting.
- **Old sound toggle:** `OptionsMenu`'s Sound toggle and its `AudioListener.volume` hack are removed.

### 3. Input: `InputManager` (new, `Scripts/Input/`)
- **Asset changes (manual, exact list in the checklist):**
  - Add two control schemes: `Keyboard&Mouse` and `Gamepad`.
  - **Player map additions:**
    - `Aim` (Value/Vector2) = `<Gamepad>/rightStick`
    - `Reload` = R / `<Gamepad>/buttonWest`
    - `Pause` = Esc / `<Gamepad>/start`
    - Gamepad bindings for existing actions:
      - Move = leftStick
      - Fire = rightTrigger
      - Dash = leftTrigger or rightShoulder
      - Stomp = buttonSouth
      - SwitchWeapon = buttonNorth
    - Also fix Stomp's empty-path binding.
  - **New `UI` map:** Navigate, Submit, Cancel, Point, Click, ScrollWheel, TabLeft/TabRight (the shoulder buttons, for switching settings tabs).
  - **New `Debug` map:** the I, F1, H and P keys, enabled only in editor and dev builds.
- **Shared instance:** `InputManager.Instance` owns the **single** `PlayerControls` instance. The 5 scripts drop their `new PlayerControls()` and read `InputManager.Instance.Controls.Player.*` instead, with the same action names, so each change is a few lines.
- **Map switching:** `EnablePlayer()` / `EnableUI()`. The pause menu and shop enable only the UI map, so pressing A in a menu can't also fire Stomp.
- **Device tracking:** `CurrentScheme` + `event OnSchemeChanged`, driven by `InputSystem.onActionChange` / `InputUser.onChange`, based on which device last sent meaningful input.
  - Switching to Gamepad hides the OS cursor and custom crosshair, then shows the gamepad aim reticle and pad button icons.
  - Switching back reverses this.
- **Unified aim:** `Vector2 GetAimDirection(Vector2 origin)` is the single source of aim direction.
  - Mouse: moves the screen-to-world plane math out of `PlayerConeShooter.GetMouseAimDirection()`.
  - Gamepad: right stick after a radial deadzone. It **keeps the last direction** when the stick is released.
  - **Aim assist** (gamepad only, scaled by the `aimAssist` setting): if an enemy is within about 12° and in range, the aim bends part of the way toward it. `EnemyKillTracker` or an overlap query can supply candidates.
- **Aim callers switch to `GetAimDirection`:**
  - `PlayerConeShooter` (fire direction; also delete the legacy fire fallback at 119–123)
  - `SimplePlayerRotation`
  - `CameraLead` (class `CinemachineCursorLead`) gets its own gamepad path. See **Camera lead on gamepad** below.
  - `CustomCrosshair`: on gamepad the reticle sits at `player + aimDir * reticleDistance`
- **Camera lead on gamepad (so controller players aren't at a disadvantage):**
  - **How it works now:** `CalculateTargetPosition()` (`CameraLead.cs:206–225`) computes `offset = ClampMagnitude(mouseWorld − player, maxLeadDistance = 3) × cursorInfluence (0.5)`, so the camera shifts at most 1.5 units. The cursor is almost always more than 3 units from the player, so **mouse players are nearly always at full lead**, pointed wherever they aim. The mouse is already capped at a radius, so it needs no extra clamp.
  - **The problem:** a stick only gives a direction, plus a tilt amount that springs back to 0 when released. Using tilt directly would give less lead at partial tilt and snap the camera back to the player on release. That is the disadvantage.
  - **Fix:** replace `GetMouseWorldPosition()` with `InputManager.Instance.GetLeadOffset(player.position, maxLeadDistance)`, which returns a world offset clamped to `maxLeadDistance`:
    - **Mouse:** `ClampMagnitude(mouseWorld − player, maxLeadDistance)`, the same as today.
    - **Gamepad aiming:** `aimDir × maxLeadDistance × leadCurve(tilt)`. The curve reaches full lead at about 50% tilt, so a light tilt gets the same look-ahead as the mouse.
    - **Gamepad stick released:** **hold** the last lead, just as a mouse cursor stays where you left it. After about 1.5 s with no aim, ease the lead toward the **movement direction** at about 60% strength, so running still looks ahead.
  - **Smoothing:** add a `gamepadSmoothSpeed` field (about 3, versus `smoothSpeed` 5) so quick stick flicks don't whip the camera. Aim assist changes the aim only, never the lead.
  - The `cameraLead` setting multiplies the result for both devices.
  - **Tuning check:** play the same room with mouse and with pad, and confirm how far ahead you can see while shooting is about the same. Show the green target gizmo (`OnDrawGizmos`, line 452) to compare.
- **Other old-input call sites:**
  - `CursorTooltip` anchors to the selected card on gamepad instead of the mouse.
  - Reload and Pause become action callbacks.
  - The debug keys move to the Debug map.
  - Once no `Input.*` calls remain, set Active Input Handling to **Input System Package (New)**, a manual Player Settings change.
- **Rumble:** `InputManager.Rumble(low, high, duration)` is scaled by the `rumble` setting and uses unscaled time.
  - It stops on pause, on focus loss and when the pad disconnects.
  - Hooks: taking damage (`DamageIndicator`), stomp impact, boss slam, shotgun blast (small).
- **Controller disconnect:** when the active pad is lost during a run, the game auto-pauses with a "Reconnect controller" toast.
- **Rebinding:** `RebindRow` UI uses `PerformInteractiveRebinding()`, with Esc/Select cancelling.
  - It checks for duplicate bindings, and each row has a "Reset" button.
  - Overrides are stored via `SaveBindingOverridesAsJson()` in `GameSettings.bindingOverridesJson`.

### 4. `PauseController` (new, `Scripts/Managers/`)
- One owner of `Time.timeScale`.
  - Pause uses reason tokens: `Pause(PauseReason.Menu)` / `Resume(PauseReason.Menu)`.
  - Hit-stop is `RequestHitStop(duration, scale)` and can never override a real pause. This fixes the `DamageIndicator` unpause bug.
- Existing direct writes (`DemoCompleteScreen`, `TutorialManager`, `PlayerHealth`, `StatTracker`, `RetryButton`, `RoguelikeManager`) move over **opportunistically**. The must-have change is only the pause menu and `DamageIndicator`.
- It raises `OnPauseChanged`, which drives the mixer snapshot, the input map switch and the rumble stop.

### 5. Settings UI (new `Scripts/UI/Settings/`, uGUI + TMP, one prefab reused in Main menu and pause)
- `SettingsPanel` has tabs, switched with Q/E or LB/RB.
- Rows are small reusable components (`SliderRow`, `ToggleRow`, `DropdownRow`/`CycleRow`, `RebindRow`) that bind to one settings field by name/delegate. **Adding an option means adding one row and one field.**
- A `CycleRow` (◄ value ►) is used instead of dropdowns, because dropdowns are awkward on a gamepad.

| Tab | Options |
|---|---|
| **Audio** | Master, Music, SFX, UI volume · Mute when unfocused |
| **Video** | Window mode (Fullscreen / Borderless / Windowed) · Resolution · VSync · FPS cap (30/60/120/144/Unlimited) · CRT filter · Show FPS |
| **Gameplay** | Damage numbers · **Psychedelic mode** · Screen shake 0–100% · Camera lead 0–100% · Crosshair style · Flash intensity (photosensitivity) |
| **Controls** | Rebind (keyboard and gamepad lists; the device you're using shows first) · Aim assist 0–100% · Stick deadzone · Auto-fire while aiming (twin-stick) · Rumble 0–100% |

**Menu behaviour:**
- Changing resolution or window mode opens "Keep these settings? Reverting in 10s".
- Each tab has "Reset to defaults".
- Back/Cancel (B or Esc) closes a tab or panel one level at a time.
- Every panel sets a **first-selected** button, handled by a `UIFocus` helper. When the pad is active and nothing is selected, it re-selects that button, so focus is never lost.
- Buttons and rows show a clear selected state (scale plus the 1-bit invert highlight) and play UI hover and confirm sounds.
- **Scrolling:** a small `ScrollToSelected` component on each tab's ScrollRect keeps the selected row in view. Unity doesn't do this for you.
- **Slider step on gamepad:** `SliderRow` handles left/right itself. Each press moves a fixed step (5% by default, set per row), and holding repeats after 0.4 s at about 10 steps per second, using unscaled time. Unity's default slider moves too little per press.

**Pause menu:** `OptionsMenu.cs` is refactored into a `PauseMenu`.
- Options: Resume / Settings / Retry / Main Menu.
- It uses the `Pause` action, `PauseController` and the UI map.
- The existing `RetryRun` / `ReturnToMainMenu` calls stay.

**Setting listeners** (each is a small `OnEnable` subscription plus an initial apply):
- `DamageNumberManager.Spawn`: return early if `!damageNumbers`.
- `CameraShake.ShakeCamera` and the `CameraLead` shake: multiply the amplitude by `screenShake`.
- `CameraLead`: lead distance × `cameraLead`.
- `PsychedelicBloodController`: `SetActive(settings.psychedelicMode)`. The P key becomes debug-only.
- `DamageIndicator`: vignette/flash alpha × `flashIntensity`.
- CRT: toggle the `crt.mat` full-screen pass (a `ScriptableRendererFeature.SetActive` or Volume toggle, whichever it uses; check in the Editor).
- `FpsCounter` (new, tiny).

**Shop and controller:** the offer cards use `PointerHoverRelay` for focus. Also implement `ISelectHandler`/`IDeselectHandler` on the same components so gamepad selection triggers the same hover focus, tooltip and stat preview.

---

## "Beyond a student project" polish list (ranked by impact for effort)
1. **Button icons that follow the device.** A `GlyphLibrary` SO maps action → sprite for Keyboard, Xbox and PlayStation (pick from `Gamepad.current` type). A `ActionPrompt` TMP component fills in text like "Press {Stomp}" and updates on `OnSchemeChanged`. This is the most visible sign of a professional game.
2. **Muffled audio when paused** (mixer snapshot) plus **UI sounds** on hover, confirm, back and slider ticks.
3. **Pitch variation and voice limiting** on SFX. Gunfire goes from "harsh" to "punchy".
4. **Rumble** tuned per event.
5. **Photosensitivity notice** on first boot (the game has psychedelic, CRT and flash effects), linking to the Flash intensity and Shake settings. Steam reviewers notice this.
6. **Steam Deck friendly:** full menu navigation without a mouse, readable at 1280×800, auto-pause on disconnect or focus loss.
7. **Hit-stop on enemy kills** (30–50 ms via `PauseController.RequestHitStop`). Currently hit-stop only happens when the player is hurt.
8. **Settings that can't be lost:** saved with a temp-file-then-replace write, corrupt-file fallback, versioned for migration.
9. **Menu transitions** using unscaled-time tweens (slide/fade) and a selected-row indicator in the 1-bit style.
10. **Remembered last device:** on launch, start in the scheme the player last used, so the first menu already shows the right button icons.

---

## Phases (fits the Oct 28 deadline; A is required for the demo)

**Phase A (demo-critical, about 1.5 weeks)**
- **Tasks:**
  - `SettingsService`
  - Mixer + `AudioManager` volumes (tier-1 routing only)
  - `InputManager` with gamepad bindings and unified aim
  - Migrate the 5 `PlayerControls` owners and all gameplay `Input.*` calls
  - `PauseController` + `PauseMenu`
  - `SettingsPanel` with the Audio and Gameplay tabs
  - Switch the EventSystem module in Main menu and Store Scene
  - `UIFocus` first-selected on every panel
  - Shop select handlers
- **Acceptance:**
  - A full run can be played from boot to boss with **only a gamepad**, including menus and shop.
  - Volume sliders work and persist across restarts.
  - Damage numbers and psychedelic toggles work live.
  - Pausing during a hit-stop stays paused.
- **Checkpoint:** commit, then playtest with a pad.

**Phase B (about 1 week)**
- **Tasks:**
  - Controls tab with rebinding
  - Button icons that follow the device
  - Rumble
  - Aim assist
  - Video tab (resolution with revert, VSync, FPS cap, CRT, FPS counter)
  - Muffled-audio snapshot on pause
  - UI sounds
- **Acceptance:**
  - Button icons update the moment you touch the other device.
  - A rebind survives a restart.

**Phase C (polish, if time allows)**
- **Tasks:**
  - Tier-2 SFX migration (pitch variation and voice limiting)
  - Kill hit-stop
  - Photosensitivity notice
  - Disconnect auto-pause
  - Menu tweens
  - Remaining `timeScale` writers moved to `PauseController`
  - Switch Active Input Handling to New only

## Critical files
- **New:** `Scripts/Settings/{GameSettings,SettingsService}.cs`, `Scripts/Audio/{AudioManager,SfxCue}.cs`, `Scripts/Input/{InputManager,GlyphLibrary,ActionPrompt}.cs`, `Scripts/Managers/PauseController.cs`, `Scripts/UI/Settings/{SettingsPanel,SliderRow,ToggleRow,CycleRow,RebindRow,UIFocus,ScrollToSelected,FpsCounter}.cs`
- **Modified:** `PlayerConeShooter.cs`, `PlayerController.cs`, `PlayerStompController.cs`, `SimplePlayerRotation.cs`, `WeaponInventory.cs`, `WeaponAmmoManager.cs`, `CameraLead.cs`, `CameraShake.cs`, `CustomCrosshair.cs`, `CursorTooltip.cs`, `PointerHoverRelay.cs` / `UpgradeCardView.cs`, `DamageNumberManager.cs`, `DamageIndicator.cs`, `PsychedelicBloodController.cs`, `MusicManager.cs`, `OptionsMenu.cs` → `PauseMenu`
- **Manual (Editor checklist in the root doc):**
  - `PlayerControls.inputactions` bindings and schemes
  - `MainMixer.mixer`
  - `SfxCue` assets
  - Settings prefab
  - `[Persistent]` children in Boot
  - EventSystem modules in 2 scenes
  - AudioSource Output groups on prefabs
  - Active Input Handling

## Verification
- **Editor Play Mode with an Xbox or PS pad:**
  - Navigate Main menu → Settings → every tab → back, without the mouse.
  - Start a run: move, aim, fire, dash, stomp, reload, switch weapon.
  - Open the shop, buy with the pad, and check the tooltip follows the selection.
  - Pause with Start and resume.
- **Device switching:** move the mouse mid-run and confirm the cursor, crosshair and icons switch within a frame. Touch the pad and they switch back.
- **Audio:** each slider at 0 silences its group only. Restart and the values persist. Delete or corrupt `settings.json` and the game boots with defaults.
- **Toggles:** damage numbers off → no popups from gun or stomp. Psychedelic on → the blood cycles colours. Shake 0% → no shake.
- **Camera lead:** on the pad, aim with a light tilt, then release the stick. The camera stays led in the aim direction and doesn't snap back. It should look about as far ahead as mouse aiming does.
- **Hit-stop regression:** take damage and press Pause in the same frame; the game must stay paused.
- **Old input removed:** grep `Input\.Get|Input\.mouse` in `Assets/Scripts` and only intentionally kept debug code should remain. Make a build with Active Input Handling = New and smoke-test it.
