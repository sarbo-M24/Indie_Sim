# Shop UI — Editor Setup

Scene/prefab wiring for the shop UI rework (commit `ffa9b2e`) and the brand-conflict Replace panel. All logic is in scripts; everything below is Inspector/hierarchy work in `RoguelikeMode.unity` under `Player Canvas RoguelikeMode` → shop panel.

Scripts involved:

| Script | Path | Role |
|---|---|---|
| `ShopUIController` | `Assets/Scripts/Upgrades/` | Orchestrator (modified) |
| `UpgradeCardView` | `Assets/Scripts/Upgrades/` | One Buy offer card |
| `CursorTooltip` | `Assets/Scripts/UI/` | Shared cursor-following tooltip |
| `ShopMascot` | `Assets/Scripts/Upgrades/` | The cat (pet + speech bubble) |
| `ShopDialogueSet` | `Assets/Scripts/Upgrades/` | ScriptableObject line pools |
| `ReplaceConfirmPanel` | `Assets/Scripts/Upgrades/` | Brand-conflict "Replace?" popup |
| `PointerHoverRelay` | `Assets/Scripts/UI/` | Hover events for the popup's stats button |
| `PackStatsPreview` | `Assets/Scripts/Upgrades/` | Stat line formatting (no setup) |

> Until step 1 is done, the shop shows **no Buy cards** — `buyCardSlots` was replaced by `offerCards`, so the old references are gone.

---

## 1. Offer cards (3)

1. Under the shop panel's Buy area, **delete** 2 of the 5 Buy card containers (delete, don't disable). Leave the 5 Burn slots alone — they still use `CigCardUI`.
2. On each of the 3 remaining Buy cards:
   - Remove the `CigCardUI` component.
   - Add `UpgradeCardView`.
   - Make sure the card's **pivot is center** (0.5, 0.5) — scale animates around the pivot.
   - Card face should show only **name** and **cost**: add/keep two TMP texts for those; remove or hide the old icon / rarity icon images.
3. Wire `UpgradeCardView` fields:
   - **Name Text** → the name TMP text.
   - **Cost Text** → the cost TMP text.
   - **Button** → the card's Button (click = select, same as before).
   - **Selected Highlight** → the existing select highlight object (shown while the card is selected for Buy).
   - **Background** → the card's background Image. The hover border is a Unity `Outline` on this Image. Add `Outline` yourself to tune it, or leave it — the script adds one at runtime if missing. Outline must be on the background only, not on the text.
   - **Border Color** → match the 1-bit palette. **Border Distance** → start around (3, -3).
   - **Neutral / Focused / Unfocused Scale** → 1 / 1.1 / 0.9. **Tween Duration** → 0.12.
4. On the `ShopUIController`:
   - **Offer Cards** → the 3 cards, in display order.
   - **Offer Count** → 3.
   - **Tooltip** → the `CursorTooltip` from step 2.

> If the Outline looks soft or misaligned at the game's pixel scale, flag it — fallback is a child Image with a 9-sliced frame sprite toggled on focus.

## 2. Cursor tooltip (shared)

1. Create a new UI object (e.g. `CursorTooltip`) as a child of the canvas / shop panel. It should be the **last sibling** so it draws on top (the script also calls `SetAsLastSibling()` on every show). It must sit **after** the Replace panel in the hierarchy.
2. Add `CursorTooltip` to it. Structure:

```
CursorTooltip            (RectTransform, CursorTooltip)
└─ Panel                 (Image bg, VerticalLayoutGroup, ContentSizeFitter)
   ├─ Small              (label group)
   │  └─ SmallLabel      (TMP)
   └─ Large              (VerticalLayoutGroup)
      ├─ Title           (TMP)
      ├─ Description     (TMP)
      └─ Stats           (TMP)
```

3. Panel components:
   - `VerticalLayoutGroup`: Control Child Size width + height **on**, Child Force Expand **off**, padding to taste.
   - `ContentSizeFitter`: Horizontal **Preferred Size**, Vertical **Preferred Size**.
   - `Large` also gets a `VerticalLayoutGroup` (same settings, some spacing) so its three texts stack.
4. All TMP texts: **word wrapping on**. The script caps each text at `Max Text Width` so long descriptions wrap.
5. Wire `CursorTooltip` fields:
   - **Panel** → `Panel` (if left empty it uses its own RectTransform).
   - **Cursor Offset** → ~(20, 20).
   - **Max Text Width** → ~420.
   - **Small Root** → `Small`, **Small Label** → `SmallLabel`.
   - **Large Root** → `Large`, **Title Text** / **Description Text** / **Stats Text** → the three TMP texts.
6. Raycast Target: the script forces it **off** on every Graphic under the tooltip at startup, so you don't have to — but don't add anything under it that needs clicks.
7. Starts hidden automatically.

Stat preview arrow: uses `→` if the Stats text's font (or its fallback) can render it, otherwise `>`. The shop's LiberationSans SDF lacks `→` but its dynamic fallback can add it. If you switch to a pixel font without the glyph, expect `>`.

## 3. Shop mascot (cat)

1. Place the cat under the shop panel so it opens/closes with the shop. Suggested structure:

```
Mascot                   (RectTransform, ShopMascot)
├─ Cat                   (Image — the hit area)
└─ SpeechBubble          (Image bg, near the cat's head)
   └─ BubbleText         (TMP)
```

2. `ShopMascot` can go on `Mascot` (root) or directly on the cat Image.
3. Raycast Target: the script forces it **on for the cat Image only** and off for everything else under the mascot.
4. Pivot: the script moves the cat Image's pivot to **bottom-center** at runtime (position-compensated, so it doesn't jump) so the squish goes toward its feet. You can also set it in the editor; then the runtime step is a no-op.
5. Create the dialogue asset: Project window → **Create → Shop → Dialogue Set**. Placeholder lines come pre-filled (OnSelected, OnPurchased, OnPurchaseFailed; OnPet empty and unwired).
6. Wire `ShopMascot` fields:
   - **Shop** → the `ShopUIController` (optional — falls back to `ShopUIController.Instance`).
   - **Tooltip** → the `CursorTooltip`.
   - **Cat Image** → `Cat`.
   - **Pet Label** → "Pet".
   - **Squish Scale Y** 0.85, **Squish Down Duration** 0.06, **Squish Return Duration** 0.12, **Squish Overshoot** 1.5.
   - **Dialogue** → the Dialogue Set asset.
   - **Bubble Root** → `SpeechBubble`, **Bubble Text** → `BubbleText`.
   - **Bubble Duration** → 2.
7. The bubble starts hidden automatically.

Cat reactions: card clicked → OnSelected line; purchase succeeds → OnPurchased; Buy fails (not enough coins or pack full) → OnPurchaseFailed.

## 4. Replace-confirm panel (brand conflict)

Shown when Buy hits the Mild/Regular same-slot rule (e.g. buying a Regular stomp upgrade while holding a Mild one).

1. Add `ReplaceConfirmPanel` to an object that **stays active while the shop is open** — e.g. the shop panel itself. It must be enabled to hear the conflict event.
2. Build `panelRoot` as a child:

```
ReplacePanel             (panelRoot)
├─ Blocker               (full-screen Image, Raycast Target ON, semi-transparent)
└─ Box                   (Image bg)
   ├─ Message            (TMP)
   ├─ StatsButton        (Image/Button, Raycast Target ON, PointerHoverRelay)
   ├─ ReplaceButton      (Button)
   └─ CancelButton       (Button)
```

   - The **Blocker** stops the shop behind from being clicked while the question is open.
   - **StatsButton** needs `PointerHoverRelay` and a raycast-target Image (e.g. a small "stats" / "i" icon).
3. Wire `ReplaceConfirmPanel` fields:
   - **Shop** → the `ShopUIController` (optional — falls back to `ShopUIController.Instance`).
   - **Tooltip** → the `CursorTooltip`.
   - **Panel Root** → `ReplacePanel`.
   - **Message Text** → `Message`.
   - **Replace Button** → `ReplaceButton`, **Cancel Button** → `CancelButton`.
   - **Stats Hover** → the `PointerHoverRelay` on `StatsButton`.
4. Hierarchy order: the **tooltip must come after** `ReplacePanel` so it draws above the popup.
5. Starts hidden automatically.

Behavior: Replace → removes the held conflicting cig and buys the new one (coins checked first, so a failed buy never drops the old cig). Cancel → nothing changes. Closing the shop with the popup open counts as Cancel. Hovering the stats button shows `Old → New` with the new cig's description and a `current → after` stat list including what's lost from the replaced cig.

## 5. After wiring

- Commit the `.meta` files Unity generated for the new scripts along with the scene changes.
- Check the Console on open / close / reopen of the shop and across a scene reload — there should be no new errors or warnings.

## 6. Manual test checklist

1. Open shop, sweep the mouse fast across all three cards and off again — focused card grows with an outline, others shrink, all return to neutral, no jitter.
2. Hover each card near the right and bottom screen edges — tooltip flips, never clips, never flickers.
3. Buy an upgrade; next shop, confirm a similar upgrade's stat preview reflects the new current values.
4. Try to buy something unaffordable — failed line from the cat.
5. Spam-click the cat 10+ times, then stop — it settles at normal scale.
6. Buy two items quickly — the second line interrupts the first.
7. Close and reopen the shop; reload the scene; no errors, no dialogue firing twice.
8. Hold a Mild stomp cig, try to buy a Regular stomp cig — Replace panel appears; hover stats shows the swap; Replace swaps and charges coins; Cancel leaves everything unchanged.
9. Open the Replace panel, then close the shop — reopening shows no leftover pending replace.
