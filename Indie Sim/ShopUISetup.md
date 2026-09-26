# Shop UI — Editor Setup

Scene/prefab wiring for the shop UI rework (commit `ffa9b2e`) and the brand-conflict Replace panel. All logic is in scripts; everything below is Inspector/hierarchy work in `RoguelikeMode.unity` under `Player Canvas RoguelikeMode` → shop panel.

Scripts involved:

| Script | Path | Role |
|---|---|---|
| `ShopUIController` | `Assets/Scripts/Upgrades/` | Orchestrator (modified) |
| `UpgradeCardView` | `Assets/Scripts/Upgrades/` | One Buy offer card |
| `PackCigView` | `Assets/Scripts/Upgrades/` | One cig held in the pack (was `CigCardUI`) |
| `CursorTooltip` | `Assets/Scripts/UI/` | Shared cursor-following tooltip |
| `ShopMascot` | `Assets/Scripts/Upgrades/` | The cat (pet + speech bubble) |
| `ShopDialogueSet` | `Assets/Scripts/Upgrades/` | ScriptableObject line pools |
| `ReplaceConfirmPanel` | `Assets/Scripts/Upgrades/` | Brand-conflict "Replace?" popup |
| `PointerHoverRelay` | `Assets/Scripts/UI/` | Hover events for the popup's stats button |
| `PackStatsPreview` | `Assets/Scripts/Upgrades/` | Stat line formatting (no setup) |

> Until step 1 is done, the shop shows **no Buy cards** — `buyCardSlots` was replaced by `offerCards`, so the old references are gone.

---

## 1. Offer cards (3)

1. Under the shop panel's Buy area, **delete** 2 of the 5 Buy card containers (delete, don't disable). Leave the 5 pack cig slots alone — see step 1b.
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
   - **Rarity Config** → the shared `RarityConfig` asset (tints the tooltip by rarity).

> If the Outline looks soft or misaligned at the game's pixel scale, flag it — fallback is a child Image with a 9-sliced frame sprite toggled on focus.

## 1b. Two separate prefabs: Buy cards vs pack cigs

The Buy offers and the cigs in the pack now use **different prefabs**:

| | Buy offer card | Pack cig |
|---|---|---|
| Script | `UpgradeCardView` | `PackCigView` |
| Prefab | new, e.g. `Prefabs/Upgrades/UpgradeCard.prefab` | existing `Prefabs/Upgrades/CigCard.prefab` (rename to `PackCig` if you like) |
| Look | framed box: cig image, name, cost | just the cig image, no box |
| Click | select, then shared **Buy** button | select (lift comes later), shows that cig's own **Burn** button |
| Hover | tooltip: name, description, stat preview | tooltip: name, description, Tier / Rarity |

`CigCardUI` was renamed to `PackCigView` and the `.meta` kept its GUID, so `CigCard.prefab` and the 5 scene slots keep the component, and the `icon` / `button` / `selectedHighlight` references stay wired. The controller's slot array was renamed `burnCardSlots` → `packCigSlots` with `FormerlySerializedAs`, so the scene's slot references carry over too.

**Buy card prefab**
1. Make one of the 3 finished offer cards (step 1) into a prefab, e.g. `UpgradeCard.prefab`, then turn the other two into instances of it.
2. Face: background Image (the box) → cig Image + name TMP + cost TMP inside it. `UpgradeCardView` has no icon field yet. The cig image is static art for now. Say if it should show each offer's `CigData.icon`.

**Pack cig prefab (`CigCard.prefab`)**
```
PackCig                  (RectTransform, Image = cig art, Button, PackCigView)
├─ SelectedHighlight     (optional)
└─ BurnButton            (Button + label, placed above the cig)
```
1. Remove the leftover rarity image if it's still there (the `rarityIcon` field is gone).
2. The root Image must be a **raycast target**, since it drives both hover and click.
3. Add a `BurnButton` child. Wire `PackCigView` → **Burn Button**. It is hidden until the cig is selected.
4. **Icon** → the cig Image (filled from `CigData.icon`). **Button** → the root Button.

**Scene**
1. Delete the old shared **Burn** button under the shop panel. `ShopUIController.burnButton` no longer exists; every cig has its own button now.
2. `ShopUIController` → **Rarity Icons** is gone too; nothing to rewire.
3. Check **Pack Cig Slots** still lists the 5 slots.

Behavior: click a pack cig → it's selected, its Burn button appears (only one cig selected at a time). Click it again → deselected. Burn → burns it and the list refreshes. Hover any pack cig → the tooltip shows name, description, Tier/Rarity.

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
   - **Background** → leave empty (uses `Panel`'s Image), or drag in a different background Image.
   - **Tint Strength** → 0.3. How far the background blends toward the cig's rarity colour when hovering a card or pack cig. Raise for stronger colour, lower if text gets hard to read. The Image's own colour is the untinted base (used for the cat's "Pet" and the replace-confirm stats).
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
4. Pivot: the script moves the cat Image's pivot to **bottom-center** at runtime (position-compensated, so it doesn't jump) so the feet stay planted when pet frames differ in size. You can also set it in the editor, in which case the runtime step does nothing.
5. Create the dialogue asset: Project window → **Create → Shop → Dialogue Set**. It comes with placeholder lines for OnShopOpened (greeting), OnSelected, OnPurchased, OnNotEnoughCoins, OnPackFull, OnPackCigSelected and OnPet. An existing asset keeps its old OnPurchaseFailed lines, now under OnNotEnoughCoins; OnPackFull, OnPackCigSelected and OnPet fill with defaults.
6. Wire `ShopMascot` fields:
   - **Shop** → the `ShopUIController` (optional — falls back to `ShopUIController.Instance`).
   - **Tooltip** → the `CursorTooltip`.
   - **Cat Image** → `Cat`. Its sprite at startup is the resting sprite that comes back after a pet.
   - **Pet Label** → "Pet".
   - **Pet Frames** → the 3 petting sprites, in order. **Pet Frame Duration** 0.1, **Pet Duration** 0.6 (frames loop until it ends).
   - **Bounce Height** 12, **Bounce Count** 2 — the cat hops on its Y position during the pet.
   - **Squish Amount** 0.2, **Squish Portion** 0.35 — before each hop the cat squishes on Y toward its feet (0.2 = down to 80% height), for the first 35% of the hop. Set Bounce Height to 0 for squish only, or Squish Amount to 0 for bounce only.
   - **Dialogue** → the Dialogue Set asset.
   - **Bubble Root** → `SpeechBubble`, **Bubble Text** → `BubbleText`.
   - **Characters Per Second** → 40 (typewriter speed).
7. The bubble is always visible while the shop is open — keep `SpeechBubble` active in the scene.

Cat reactions: shop opens → OnShopOpened greeting; offer card clicked → OnSelected; purchase succeeds → OnPurchased; Buy or Reshuffle short on coins → OnNotEnoughCoins; Buy with a full pack → OnPackFull; pack cig clicked → OnPackCigSelected; cat clicked → OnPet. Lines type out one character at a time, a finished line stays up until the next one replaces it, and a new reaction cancels the line in progress and starts typing the new one.

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
10. Click a pack cig — its Burn button appears; click another — Burn moves to it; click the same one again — Burn hides.
11. Hover a pack cig — tooltip shows name, description, Tier/Rarity; burn a cig while hovering it — the tooltip closes.
