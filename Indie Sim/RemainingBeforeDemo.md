# Remaining before the demo — as of 2026-10-07

Sarbo's own list: **main menu UI rework (new assets), SFX, BGM, and finishing
`PlaytestCases.md` with its bug fixes.** Everything below was found by going
through the docs and code on 2026-10-07 and isn't covered by that list.
Nothing here has been changed yet.

## Ships as-is unless fixed

| # | What | Where | Fix |
|---|---|---|---|
| 1 | Debug buttons visible on the HUD, one calls `CompleteDungeon` (skip dungeon), one does nothing | `Player Canvas RoguelikeMode.prefab` ▸ `Debug Buttons ` (active) | Hide outside the editor / development builds |
| 2 | God mode toggles on **I** in release builds | `Temp -Player.prefab` ▸ `PlayerHealth.enableToggleInBuild = true` | Untick it |
| 3 | **H** fires a test damage flash + hitstop | `DamageIndicator.Update()` (no build guard) | Guard with `UNITY_EDITOR \|\| DEVELOPMENT_BUILD` |
| 4 | **Brand conflict has no confirm popup.** Buying a cig whose brand clashes with a held one for the same slot (Mild/Regular exclusivity) is blocked and the detail text says "confirm to proceed", but nothing can confirm: `ReplaceConfirmPanel.cs` exists and is wired to `OnBrandConflictDetected` → `ConfirmReplacePurchase` / `CancelReplacePurchase`, but it isn't placed in any prefab or scene. Those buys can never complete. While one is pending, B and RB-Continue do nothing and clicking elsewhere doesn't clear it; picking another card or Continue gets out. | `ShopUIController` (pending state), `ReplaceConfirmPanel.cs` (unused) | Build the popup on the shop (panel root with a full-screen blocker, message, Replace/Cancel, optional stats hover) and add the component; also give it pad focus + B = Cancel. Then run E6 and J10. |

## Possible bugs to check

| # | What | Notes |
|---|---|---|
| 5 | Ammo UI not showing in `BossArena` | Open in `PROGRESS_REPORT.md` ▸ Known Issues; no fix committed since. `WeaponAmmoManager.cs:65` still says the `"ReloadIcon"` name is a guess. Matters for boss screenshots. |
| 6 | Older "awaiting test" items not in `PlaytestCases.md` | From `SettingsAudioInputPlan.md`: pause-menu navigation; death screen (Retry focused, cursor shown, crosshair hidden); A skips the countdown; machine-gun-after-Retry fix; Controls-panel rebinding list. |

## Steam (time-critical)

Per `DemoBeforeIGDC.md`: build review submitted **~Oct 8–10**, store page
Coming Soon by **~Oct 14**, last engine day **Oct 16**. Every box in
`SteamReleaseNotes.md` §5 is unticked: App ID, store page, content survey
(gore + AI disclosure), capsules, 5+ screenshots, trailer, Steam Input
settings, controller questionnaire, pad tests (USB/Bluetooth, from Steam and
direct), build review, **Release App**. Tick what's already done.

**Name before the first build:** `productName` is `OneBitKill(Beta)` (v0.2.2).
It's the exe name, window title and save-folder path; renaming after release
makes existing saves seem to vanish (`PlaytestCases.md` § S).

## Housekeeping

- **Commit.** Nothing since `aece207`; A–O of `PlaytestCases.md`, the save/tutorial work and today's fixes are all uncommitted.
- **Undecided backlog** (`DemoBeforeIGDC.md`): boss variants, UI/player animations — cut or still wanted?
- **Active Input Handling = Both.** Fine for the demo; items 2–3 and `PsychedelicBloodController`'s toggle use the old `Input` API, so they'd break on New-only.
- **Fine to leave until after the demo:** Phase 8 verification pass, save-system 4E, the ~0.5–1 s startup lag, shared-canvas cleanup.
