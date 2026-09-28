# Dungeon Paint Reveal — Social Media Showcase Scene

## What this is

A standalone showcase scene that generates a dungeon instantly and then paints it onto the tilemaps room by room, tile by tile, over ~8–10 seconds, for recording a reel.

**This is not a gameplay feature.** It is a marketing gimmick. The design goal is *containment*: gameplay code stays effectively untouched, and the whole feature can be deleted by removing one folder and reverting a handful of trivial lines.

## Ground rules

- One commit.
- All new files live in `Assets/_Showcase/` (scripts, scene, any assets). Nothing new elsewhere.
- The showcase scene is **not** added to Build Settings.
- Changes to gameplay code are limited to the list in §1. Nothing else in `DungeonMapGenerator` or any other gameplay script changes. If you think something else needs changing, stop and ask.
- Do not call `GenerateNewMap` from the showcase. It spawns spawners, relics, teleporter, and paints tiles — none of which we want.
- Visual-only logic (spill, foliage) may be duplicated inside the showcase code. That is deliberate: drift from gameplay doesn't matter for a reel, and it keeps gameplay code untouched. Wall autotiling must NOT be duplicated (see §1) because wrong wall sprites would be visible.
- If anything here conflicts with the repo, stop and ask.

## 1. Allowed changes to DungeonMapGenerator

Exactly these, nothing more:

1. **A data-only generation entry point.** A new public method that takes a seed and a node count and returns `MapData`. It must: reset the four room ID counters (same as `GenerateNewMap` does), create the private `rng` from the given seed, call `GenerateDungeon`, and return the result. No logging loops, no spawning, no painting, no static resets.
   - Reason: `GenerateDungeon` is public but depends on `rng`, which is only assigned in `GenerateNewMap`. Calling it cold throws. Counters also need resetting for repeated takes.
2. **Wall autotile access.** Make the existing wall tile selection method callable from the showcase (public, or a thin public wrapper). No logic change.
3. **Read-only access to the tile arrays** (floor, wall, foliage) so the showcase uses the exact same assets. Getters only; no change to serialization.

Key spawning: in the showcase scene, the generator instance has `shouldKeyBeSpawned` unchecked and `generateOnStart` unchecked in the Inspector. No code change for this. Verify no key object appears in the hierarchy after generation.

## 2. Scene setup

- New scene `Assets/_Showcase/DungeonRevealShowcase.unity`.
- Contains: a Grid with floor, wall, and foliage tilemaps (copy the gameplay tilemap setup: sorting, materials, renderer settings), a `DungeonMapGenerator` instance with the same `MapParametersSO` and tile arrays as gameplay, the shadow caster if lighting is desired, a camera, and a `DungeonRevealShowcase` controller object.
- **No colliders** on the showcase tilemaps (remove TilemapCollider2D / CompositeCollider2D). Nothing needs collision, and they rebuild on every tile change, which stutters.
- No player, no enemies, no managers. The scene must run standalone when opened and played directly (check that `SceneBootstrapGuard` doesn't force-load Boot or require GameSession here; if it does, stop and ask how to exempt this scene).

## 3. The paint plan (built instantly, before any reveal)

On start of a take:

1. Seed `UnityEngine.Random` AND a showcase-local System.Random from the take's seed.
2. Get `MapData` from the data-only entry point in §1.
3. Clear all three tilemaps.
4. Build the full paint plan: every (layer, position, tile) that should end up on screen.
   - **Floor tiles:** every position in `floorTiles`, plus floor under every wall position (matches gameplay), plus spill positions (duplicate the spill logic from `PaintTiles`, using the showcase RNG).
   - **Wall tiles:** every position in `wallTiles`, with the tile chosen by the generator's autotile method against the **complete** floor and wall sets. This is the critical rule: all wall sprites are decided upfront, so walls appear with their final sprite and never flicker as neighbors arrive.
   - **Foliage:** duplicate the foliage chance logic, using the showcase RNG.
5. Nothing is painted during plan building.

The same seed must produce an identical final picture on every take.

## 4. Grouping and reveal order

Group every plan entry into ordered **reveal groups**. Each tile position per layer belongs to exactly one group: first group in reveal order to claim it wins, later claims are skipped.

Order:
1. Traverse rooms breadth-first from the start room along `connections`.
2. For each room visited, emit:
   - **Room floor group:** floor tiles inside the room rect (same rect math as `AddRoomTiles`), ordered as a ripple outward by distance from the room center.
   - **Room wall group:** wall tiles adjacent (8-neighborhood) to that room's floor tiles, ordered the same ripple way.
   - For each connection to a not-yet-visited room: a **corridor group** containing that corridor's floor tiles (recompute the corridor line with the same math as the generator), ordered by distance from the current room along the line, then its adjacent walls. Only include tiles that actually exist in the plan — the recomputation is for grouping only, never adds tiles.
3. **Sweep group:** any floor or wall entry not claimed by any group above. This guarantees completeness even if the grouping math drifts from the generator. Log the count; it should be zero or near zero.
4. **Dressing group (final):** all spill floor tiles and all foliage, ordered as one ripple by distance from the start room center.

## 5. Timing

All values serialized on the controller:

- Total reveal duration for rooms + corridors + sweep: default 8s.
- Minimum time per room group: default 0.08s (so tiny corner rooms register).
- Dressing duration: default 1.5s.
- Final hold before anything else: default 1s.
- Pre-roll delay after pressing start (so the recording starts on an empty frame): default 0.5s.

Budget distribution: each group gets time proportional to its tile count, with room groups floored at the minimum; scale everything so the sum matches the total duration.

Each frame, paint every entry whose scheduled time has passed. Paint a frame's entries per tilemap in one bulk call, not one `SetTile` at a time.

## 6. Finale

- After the dressing group completes, regenerate shadows once (if the shadow caster is present). This acts as a "lights on" moment. Do not regenerate shadows during the reveal.
- Then hold.

## 7. Audio (optional but wire it)

- Serialized optional AudioClip for "room revealed", played at the start of each room group, pitch rising slightly with each room (clamped to a sane max).
- Serialized optional AudioClip for the finale.
- Null clips = silent, no errors.

## 8. Camera

- Default: fixed framing on the final map bounds (computed from the plan, plus padding) for the whole take.
- Serialized aspect mode: Landscape or Portrait.
- **Portrait:** the generator's artery always heads right, so a 9:16 frame crops it. Do NOT change the generator. Instead rotate the camera 90° on Z and frame accordingly.
  - Flag in the summary: rotating the camera also rotates the wall and floor art. If the tile art has directional shading or perspective that makes rotated walls look wrong, say so — we'll decide separately whether to add a start-direction option.
- Optional slow push-in during the final hold (serialized toggle, default on, subtle).
- Respect the Pixel Perfect Camera if the gameplay camera uses one; if it fights the framing zoom, report it rather than hacking around it.

## 9. Controls (editor/dev only)

- Space: start a take (ignored while a take is running).
- R: restart with the same seed.
- N: new random seed, then start.
- Seed is serialized (so a good-looking map can be locked in) and shown in a small on-screen label, with a toggle key (H) to hide the label for recording. Hidden by default.

Use whichever input system the project already uses.

## 10. Out of scope

- Player spawn / cut to gameplay.
- Spawners, relics, key, teleporter, enemies, gore.
- Any change to generation logic, the artery direction, or gameplay painting.
- Using this reveal as an in-game level intro.
- Seed montage and parameter sweep reels (separate task later).

## 11. Acceptance criteria

- [ ] Only the three allowed changes exist in `DungeonMapGenerator`; gameplay generation behaves identically (enter a real level and confirm).
- [ ] Showcase scene runs standalone by pressing Play.
- [ ] Reveal goes room by room outward from the start room, corridors trace between rooms, tiles ripple within each room.
- [ ] Walls never change sprite after appearing.
- [ ] Final picture matches the full generated map: sweep group count is zero or near zero and the count is logged.
- [ ] Same seed = identical final picture across takes; N produces a different map.
- [ ] Total reveal lands close to the configured duration.
- [ ] No key, spawner, relic, or teleporter objects appear.
- [ ] No console errors; no stutter from colliders or shadow rebuilds during the reveal.
- [ ] Deleting `Assets/_Showcase/` plus reverting §1 removes the feature completely.

When done, summarize: files added, the exact generator diff, sweep group count on a few seeds, how portrait rotation looked, and anything you stopped on.
