# Shop UI Update — Mascot Cat, 3 Upgrade Cards, Cursor Tooltips

## Ground rules (read first)

- This is a modification of the existing `ShopUIController`, not a rewrite. Read it fully before changing anything and preserve all existing behavior not listed below (coin deduction, purchase validation, reroll if present, open/close flow, sold-out handling).
- One commit for this whole task.
- If anything below conflicts with what exists in the repo, or a decision isn't specified here, **stop and ask**. Do not improvise architecture.
- No new third-party packages. If DOTween (or another tween lib) is already in the project, you may use it; otherwise write a minimal coroutine-based tween.
- Respect the lifetime scopes: everything in this task is **Level-scoped UI**. Nothing new goes under the persistent root, nothing becomes `DontDestroyOnLoad`.
- Do not touch the cursor system beyond what's described in section 3. Cursor was unified in an earlier phase; the tooltip must plug into it, not compete with it.

## 0. Pre-flight investigation (report findings before writing code)

Answer these in your first message, then proceed unless something blocks:

1. Does the shop set `Time.timeScale = 0` while open? (Determines unscaled vs scaled time for all animations — see rule below.)
2. What is the current purchase interaction — single click buys immediately, or click selects then a confirm button buys?
3. Where are upgrade definitions stored (ScriptableObject? plain class?) and how is an upgrade applied — data-driven stat modifiers, or an imperative method per upgrade?
4. Where is the authoritative source of current player stats (e.g. `GameSession.CurrentRun`, a stats component, `UpgradeManager`)?
5. Where is the hardcoded "5" for the number of offers, and how are offers chosen (random from pool, weighted, no-duplicates)?
6. Canvas render mode of the shop canvas (Overlay / Camera / World) and its Canvas Scaler settings.
7. Does the TMP font used in the shop contain the `→` glyph?

**Hard stop condition:** if upgrades are applied imperatively (e.g. each upgrade has custom code that mutates the player) with no way to compute the resulting stats without applying them, stop and ask. Do not duplicate upgrade math inside UI code to fake a preview.

**Timing rule:** all shop animations (hover scale, squish, dialogue timers) use unscaled time regardless of the answer to Q1, so they keep working if pausing behavior changes later.

## 1. Component breakdown

Keep `ShopUIController` as the orchestrator. Split new behavior into small, single-purpose components rather than growing the controller:

- **ShopUIController (modified)** — owns offer generation, purchase logic, and the focus state of the card group (which card is hovered). Exposes local C# events for "upgrade selected" and "upgrade purchased" (and "purchase failed", see §5).
- **UpgradeCardView (new)** — one per container. Displays name + cost, reports pointer enter/exit/click up to the controller, and animates its own scale and border when told its focus state. Cards never reference each other.
- **ShopMascot (new)** — the cat. Handles pet hover, pet click squish, and speech bubble. Listens to the controller's local events.
- **CursorTooltip (new, shared)** — a single tooltip that follows the cursor. Used by both the cat ("Pet") and the cards (full description).
- **ShopDialogueSet (new ScriptableObject)** — line pools for the cat.

Use the controller's local events for mascot reactions, **not** the static `GameEvents` bus. This is shop-local UI chatter; the global bus is for cross-system gameplay events. If some other system later needs "upgrade purchased", that should be raised on the bus by the purchase logic separately.

## 2. Offer count: 5 → 3

- Replace the hardcoded count with a serialized field on the controller, default 3.
- The scene/prefab has exactly 3 card containers (remove the extra two from the prefab/scene; don't just disable them).
- If the available pool yields fewer than 3 valid offers, hide the unused cards instead of showing empty ones or duplicates.
- Keep the existing selection rules (weighting, no duplicates, exclusions) exactly as they are.

## 3. CursorTooltip (shared)

Behavior:
- One tooltip instance, lives on the shop canvas as the last sibling so it renders on top.
- Two presentation modes: **Small** (single short label, e.g. "Pet") and **Large** (title, description paragraph, stat preview block). Implement as one component with a mode switch, not two separate tooltip systems.
- Follows the cursor each frame while shown, positioned at a small offset from the cursor. Convert screen position to canvas-local position using the correct camera for the canvas render mode (null for Overlay).
- **Edge clamping:** if the panel would go off the right or bottom edge, flip its pivot/offset to the other side of the cursor so it stays fully on screen. The Large panel will hit this often.
- Uses a ContentSizeFitter / layout so the Large panel grows to fit text, with a max width so long descriptions wrap.
- **All graphics in the tooltip must have Raycast Target turned off.** Otherwise the tooltip blocks the pointer, the hovered element receives pointer-exit, the tooltip hides, pointer-enter fires again — infinite flicker.
- Read cursor position from the same input source the unified cursor system uses. If the cursor system exposes a position, use it; if it hides the hardware cursor and draws a custom one, the tooltip anchors to the custom cursor's position.
- Show/hide API: show with mode + content, hide. Last caller wins; hiding is ignored if the caller isn't the current owner (prevents a card's exit event hiding a tooltip the cat just opened).

## 4. Upgrade cards

### Display
- Each card shows upgrade **name** and **cost** only. Nothing else on the card face.
- Unaffordable state: keep whatever the existing controller does for unaffordable offers.

### Hover focus (group behavior)
- Three visual states per card: **Neutral** (no card hovered), **Focused**, **Unfocused**.
- Suggested values (serialized, tweakable): Neutral scale 1.0, Focused 1.1, Unfocused 0.9. Tween duration ~0.12s, ease-out.
- On pointer enter a card → controller sets focused index → Focused card animates up and shows its border, the other two animate down.
- On pointer exit → controller clears focus → all return to Neutral. Moving directly between cards fires exit then enter in the same or next frame; the tween absorbs this, no special handling needed unless it visibly pops (if it does, defer the "clear focus" by one frame and cancel it if another enter arrives).
- New tweens start from the current scale, never snap to a start value. Rapid hovering must not jitter.
- Scale via `localScale` only. This does not fight the layout group (layout uses size, not scale). Pivot at center.
- Hidden or sold-out cards are excluded from focus.

### Border
- Use Unity's built-in **Outline** component on the card's background Image. Disabled by default, enabled when Focused, disabled otherwise.
- Serialized color (match the 1-bit palette) and effect distance (start around 3–4 units, tune in editor).
- Outline sits on the background Image only, not on the card's text.
- If the Outline looks soft or misaligned at the game's pixel scale, stop and flag it — fallback is a child Image with a 9-sliced frame sprite toggled on/off. Don't switch silently.

### Tooltip content (Large mode)
On hover, the card shows the Large tooltip with:
1. Upgrade name (header).
2. Full description text.
3. Stat preview block, one line per affected stat, formatted as: `Stat Name: current → upgraded`. Only list stats that actually change. Optionally color the upgraded value if the palette allows (1-bit may mean using inversion/underline instead — ask if unsure).

Stat preview data rules:
- Current values come from the authoritative stats source found in pre-flight Q4. Do not read from a UI label or a cached copy.
- Upgraded values come from a **side-effect-free preview query** on the upgrade definition/system that takes current stats and returns the resulting stats (or a list of stat deltas). Add this query next to where upgrades are applied so the apply path and the preview path share the same math.
- If the upgrade is not a stat change (e.g. a new mechanic), show the description only and omit the stat block.
- If `→` is not in the font (pre-flight Q7), use `>` and note it in the summary.

## 5. Shop mascot (cat)

### Pet hover
- Pointer enter on the cat image → show CursorTooltip in Small mode with text "Pet". Pointer exit → hide.
- The cat image needs Raycast Target on (it's the hit area). Everything else on the cat that isn't the hit area has it off.

### Pet click (squish)
- On click: Y scale squashes to ~0.85, then returns to 1.0 with a slight overshoot is fine. Total ~0.15–0.2s. X scale unchanged.
- **Set the cat image's pivot to bottom-center** so it squishes down toward the floor instead of shrinking toward its middle. Adjust anchored position so it doesn't visually move when the pivot changes.
- Rapid clicks restart the squish from the current scale; they never stack or leave the cat permanently squashed.
- Petting does not trigger dialogue in this pass (leave a clearly named empty hook/pool for it, see below).

### Dialogue
- Speech bubble anchored near the cat's head: a panel with TMP text, hidden by default. Raycast Target off.
- `ShopDialogueSet` ScriptableObject with separate line pools:
  - **OnSelected** — card clicked (only used if the shop has a select-then-confirm flow per pre-flight Q2; if click buys instantly, this pool is unused and OnPurchased fires instead).
  - **OnPurchased** — purchase succeeded.
  - **OnPurchaseFailed** — clicked but can't afford.
  - **OnPet** — empty for now, not wired.
- Fill pools with 2–3 placeholder lines each (e.g. "Good choice.", "Excellent choice.", "Heh. Bold."). Final dialogue comes later — don't polish the writing.
- Pick a random line from the pool, avoiding immediately repeating the previous line when the pool has more than one.
- Bubble stays visible for a serialized duration (default ~2s, unscaled), then hides. A new line interrupts and replaces the current one and resets the timer.
- Mascot subscribes to the controller's local events on enable and unsubscribes on disable.

## 6. Out of scope

- Controller/keyboard navigation of cards (existing P2 item).
- Final dialogue writing, voice/SFX, cat idle animation.
- Changing upgrade balance, costs, or the offer selection algorithm.
- Any change to GameSession, SaveSystem, persistent root, or the global event bus.

## 7. Acceptance criteria

- [ ] Shop shows exactly 3 offers (fewer only if the pool can't supply 3); extra containers removed from the prefab/scene.
- [ ] Hovering a card: it grows with an outline, the other two shrink; leaving returns all to neutral. No jitter on fast mouse movement across cards.
- [ ] Hovering a card shows the Large tooltip with name, description, and correct current → upgraded stats that match what actually happens after purchase.
- [ ] Tooltip follows the cursor, never clips off-screen, never flickers.
- [ ] Hovering the cat shows "Pet"; clicking squishes it down toward its feet; spam-clicking never leaves it squashed.
- [ ] Purchase (and select, if applicable) makes the cat say a line; failed purchase uses the failed pool; lines auto-hide and interrupt cleanly.
- [ ] All animations work with `Time.timeScale = 0`.
- [ ] Existing purchase, coin, and sold-out behavior unchanged.
- [ ] No new persistent objects; no new console errors or warnings on open/close/reopen of the shop, or across a scene reload.

## 8. Manual test checklist (for the summary)

1. Open shop, sweep the mouse fast across all three cards and off again.
2. Hover each card near the right and bottom screen edges — tooltip must flip, not clip.
3. Buy an upgrade; reopen the shop (or next shop); confirm the stat preview for a similar upgrade reflects the new current values.
4. Try to buy something unaffordable.
5. Spam-click the cat 10+ times, then stop — it must settle at normal scale.
6. Buy two items quickly — second line interrupts the first.
7. Close and reopen the shop; reload the scene; check for errors and duplicate subscriptions (dialogue firing twice).

When done, summarize: pre-flight answers, files changed/added, any deviations from this doc and why, and anything you had to stop on.
