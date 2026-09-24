# Settings Menu, Sound Manager, Input Manager & Controller Support — Plan

> **Ground rules (from `UpgradeSystemSpec.md` / `shop-ui-mascot-and-cards.md`):** Claude writes only C# scripts. The user does scenes, prefabs, the mixer asset and the `.inputactions` asset by hand, following numbered checklists. No new packages. No new `DontDestroyOnLoad`: global services are parented under the `[Persistent]` root in `Boot.unity`. UI animations use unscaled time.

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
