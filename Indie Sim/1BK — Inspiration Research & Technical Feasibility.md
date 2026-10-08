# 1BK — Inspiration Research & Technical Feasibility

Sep 27, 2026 · @Sarbo

## Summary

The best ideas for MonoKill are the ones that cost code and data, not art: modifiers on existing sprites, telegraphs drawn with shapes, and hand-authored rooms stitched by a generator. Almost none of this should enter the Oct 28 Steam demo; content lock is Oct 7 and engine work stops Oct 15. Treat this doc as the post-demo backlog, with a small demo-safe list at the end.

**Constraints scored against:**

- **Art** — no strong artist, so every idea is costed in 1-bit sprite frames. Low = reuse or recolour/pattern an existing sprite; Med = 1 new sprite with 2–6 frames; High = many new sprites, tiles or bespoke animation.
- **QA / balancing** — little experience tuning. Low = one number, easy to feel out; Med = interacts with 2–3 systems; High = combinatorial (synergies, stacking stats) and needs data or bots to tune.
- **Eng** — S = under a day on top of the current architecture (GameSession, GameEvents bus, ScriptableObject defs); M = 2–4 days; L = a week or more.

Existing baseline assumed: Revolver/Pistol, Shotgun, AK47 (cone + raycast), stomp, dash, relics, upgrades, coins, EnemyScaling hooks, one data-driven BossArena.

## Reference titles

Fifteen titles across the four genres; the right-hand column is the one thing worth stealing from each.

| Title | Genre | What MonoKill should take |
| --- | --- | --- |
| [Enter the Gungeon](https://www.boristhebrave.com/2019/07/28/dungeon-generation-in-enter-the-gungeon/) | Top-down roguelike shooter | Hand-authored room graph ("flows") + gun synergies |
| [Nuclear Throne](https://nuclear-throne.fandom.com/wiki/Enemies) | Top-down roguelike shooter | Small enemy roster, each with one clear verb (shoot, charge, burrow, play dead) |
| [The Binding of Isaac](https://www.boristhebrave.com/2020/09/12/dungeon-generation-in-binding-of-isaac/) | Roguelike | Grid floorplan in \~50 lines; special rooms on dead ends |
| Spelunky | Roguelike platformer | Guaranteed solution path through a grid of room templates |
| [Vampire Survivors](https://vampire.survivors.wiki/w/Evolution) | Swarm / survivors-like | Weapon evolution: max weapon + matching passive → evolved weapon |
| [20 Minutes Till Dawn](https://en.wikipedia.org/wiki/20_Minutes_Till_Dawn) | Swarm, manual aim | Survivors-like with manual shooting; near-monochrome; Unity; built in \~2 months |
| Brotato | Swarm, wave arena | Short timed waves + shop between waves |
| Halls of Torment | Swarm | Retro-limited art carrying a huge enemy count |
| [ULTRAKILL](https://ultrakill.wiki.gg/wiki/Style) | Boomer shooter | Style meter with weapon "freshness" that rewards switching |
| DUSK / Mullet Madjack | Boomer shooter | Speed as defence; kills refill a timer or resource |
| [Downwell](https://www.gamedeveloper.com/design/downwell-design-analysis) | Roguelike, 3-colour | Gun doubles as movement; air-combo rewards; palette unlocks |
| [Gato Roboto](https://www.gamedeveloper.com/design/q-a-exploring-the-design-of-cat-in-a-mech-metroidvania-i-gato-roboto-i-) | 1-bit metroidvania | 1-bit chosen to save art time; audio carries area identity |
| [Hades](https://hades.fandom.com/wiki/Boons) | Action roguelike | Upgrades bound to action slots (attack/dash/special), pick 1 of 3, rarity tiers |
| [Risk of Rain 2](https://riskofrain2.wiki.gg/wiki/Difficulty) | Roguelike shooter | Time + stage difficulty coefficient; credit-based spawn director |
| [Left 4 Dead](https://left4dead.fandom.com/wiki/The_Director) | Horde shooter | Director cycles build-up → peak → relax (30–45 s) |

Titles without a link are from general genre knowledge, not a page opened for this doc.

## Idea bank: weapons and upgrades

The cheapest weapon content is behaviour layered on the three guns you already have. A projectile modifier is code; a new gun is a sprite, a sound, an icon and a balance line.

**New guns (each needs 1 sprite + 1 icon + SFX)**

- **Railgun / sniper** — hold to charge, pierces everything in a line. Reuses the raycast path. (Nuclear Throne, Gungeon)
- **Grenade / bouncing launcher** — delayed AoE; gives the player a crowd tool against swarms.
- **Flamethrower / short cone DoT** — reuses cone shooting; the flame is a dithered particle, no sprite work.
- **Boomerang / returning disc** — hits twice; rewards positioning.
- **Melee kick or bat** — parries bullets back (Nuclear Throne's assassin, ULTRAKILL parry). Pairs with stomp and dash.

**Projectile modifiers (no new art)**

- Pierce, bounce off walls, split on hit, homing, explode on kill, chain to nearest enemy, bigger bullets, ricochet.
- These are the Gungeon/Isaac engine: one `ProjectileModifier` ScriptableObject list on the bullet, applied by relics and upgrades.

**Upgrade structures**

- **Slot-bound upgrades (Hades)** — each upgrade attaches to Shoot, Dash or Stomp; pick 1 of 3; one per slot, so stacking stays bounded. Best fit: it caps the combinatorics you'd have to balance.
- **Weapon evolution (Vampire Survivors)** — max a gun + hold its matching relic → the gun evolves (e.g. Shotgun + "Buckshot" relic → Dragon's Breath). Evolved gun = same sprite, inverted or with a dithered trail.
- **Synergies (Gungeon)** — hard-coded pairs that unlock a bonus, flagged with a small icon. Content-rich but a balancing trap: N items give N² pairs to check.
- **Brotato-style stat shop** — flat stats bought between rooms. Easy to build, notoriously easy to break (stacking).

**Boomer-shooter movement tech**

- Dash-cancel reload, stomp resets dash, kills refill dash charge. ULTRAKILL-style **style meter** with weapon freshness: each shot with a gun lowers its multiplier and raises the others, so switching pays.
- Downwell's rule: the gun is also movement. Shotgun recoil knockback as a mobility tool.

## Idea bank: enemy types

Variety comes from mixing a few one-verb enemies, not from a big roster. Nuclear Throne gets a whole area out of 4–5 archetypes, each doing one readable thing.

| Archetype | Behaviour | Source | Why it fits 1-bit |
| --- | --- | --- | --- |
| Swarmer | Weak, fast, runs at you in packs | NT maggots, survivors-likes | Tiny sprite, 2-frame wiggle |
| Rifleman | Keeps distance, single aimed shot | NT bandits | Baseline; teaches dodging |
| Charger | Telegraphs, then dashes in a straight line (can break walls) | NT Big Bandit | Telegraph = a line drawn on the floor |
| Exploder | Walks up and detonates; bursts into bullets on death | NT Explo Freak, Ballguy | Flashes white/black before popping |
| Splitter | Dies into 2–3 smaller copies | NT Giant Maggot, Isaac | Same sprite scaled down |
| Sniper | Slow; laser sight shows the shot before it fires | NT Sniper, Laser Crystal | The laser is the art |
| Turret / totem | Static, fires fixed radial patterns | Gungeon, bullet-hell | Geometric shape, no animation |
| Ambusher | Plays dead or hides as a prop, then springs | NT Assassin | Reuses a prop or corpse sprite |
| Summoner / necromancer | Revives corpses or spawns minions; kill priority | NT Necromancer | Creates target priority without new AI |
| Shielded | Blocks from the front; flank or stomp to kill | Gungeon shotgun kin variants | Shield = one white arc |
| Elite modifier | Any enemy + trait (fast, armoured, explodes, splits) | Risk of Rain, Isaac champions | Outline, invert or dither overlay; no new sprite |

**Telegraph rules for 1-bit:** colour cues are unavailable, so use shape and motion. Flash the sprite inverted, draw a warning line or circle on the floor, pause before the attack, and pair every telegraph with a sound. A hit after a clear telegraph reads as the player's mistake ([Bugnet](https://bugnet.io/blog/how-to-design-enemy-attack-telegraphs)).

**Bosses:** your BossDefinition SO can carry phase lists of existing enemy attacks. A boss = big sprite + 3 phases reusing turret, charger and summon behaviours.

## Idea bank: dungeon generation

Use hand-authored rooms placed by a simple generator. Every successful title here does this; fully procedural tiles look worse and are harder to balance.

**Option A — Isaac grid (recommended first)**

1. Fixed grid (Isaac uses 9×8). Start room in the centre.
2. Breadth-first expansion from a queue; skip a cell if it already has 2+ neighbours, so no loops form.
3. Room count per floor = a small random range plus level × a growth factor (Isaac: `random(2) + 5 + level × 2.6`).
4. Cells that fail to expand become dead ends. Boss goes on the farthest dead end; shop and treasure on others.
5. Doors sit at the exact centre of each wall, so any room prefab fits any slot. Room prefabs come from easy/medium/hard pools by depth.

**Option B — Gungeon flows**

Hand-authored graphs of 4–8 flows per floor, including loops and forks, then laid out automatically. Better pacing (treasure behind one-way loops), but graph layout and corridor pathfinding is a week-plus job ([Boris the Brave](https://www.boristhebrave.com/2019/07/28/dungeon-generation-in-enter-the-gungeon/)).

**Option C — Spelunky solution path**

A 4×4 grid; walk a guaranteed path from entry to exit, then fill side cells with optional rooms. Room templates have random "chunks" (cover, pits) swapped in per spawn. Good for a single arena-sized floor.

**Room-level variety without art**

- Obstacle chunks: each room template has 2–4 marked zones filled with random cover layouts.
- Room modifiers: dark room (only muzzle flash lights it — 20 Minutes Till Dawn's darkness), inverted palette room, swarm room, sniper room, "no dash" room.
- Challenge rooms: survive 30 s of waves for a reward (survivors-like beat inside a roguelike).
- Pacing director (Left 4 Dead): track a stress value from damage taken and nearby kills; after a peak, stop spawns for a relax window before the next build-up.

## Idea bank: run structure, meta-progression and feel

**Run structure**

- **Difficulty coefficient (Risk of Rain 2)** — `coeff = (1 + minutes × timeFactor) × 1.15^floorsCleared`. A spawn director earns credits from `coeff` and spends them on enemies priced by threat. One formula replaces hand-tuned per-room spawn tables and slots into your EnemyScaling hook.
- **Curses / heat (Hades Heat, Gungeon curse)** — player-chosen difficulty modifiers for more reward. Replay value from existing content.
- **Gem high (Downwell)** — chain kills without taking damage to enter a bonus state (double coins). Rewards aggression, which is the boomer-shooter point.

**Meta-progression**

- Unlocks between runs: new guns entering the drop pool, starting loadouts, characters as stat + starting-gun presets (no new sprite if the player is a silhouette).
- **Palette unlocks (Downwell)** — swap the two 1-bit colours. Nearly free, and it is a real reward in a 1-bit game.
- Achievements already exist; wire them to unlocks.

**Game feel (the cheapest quality in the genre)**

- Hit-stop (2–4 frames) on kills, screen shake scaled by gun, muzzle-flash frame, full-screen invert flash on big kills.
- Corpses and shell casings that persist in the room. Knockback on enemies.
- ULTRAKILL-style rank text (D → S) on screen during combos — pure UI, no sprites.
- Audio carries identity when visuals can't: Gato Roboto leaned on sound to tell areas apart.

## Feasibility matrix

Of 22 ideas, 3 are demo-safe, 13 go to the post-demo backlog, and 6 are parked because balancing risk outweighs their value. Sorted by verdict, then by lowest total cost.

| Idea | Eng | Art (1-bit) | Balance risk | Verdict | Note |
| --- | --- | --- | --- | --- | --- |
| Game-feel pass (hit-stop, shake, invert flash, casings) | S | Low | Low | Demo-safe | Biggest perceived-quality gain per hour |
| Run telemetry (floor reached, cause of death, picks) | S | None | Lowers it | Demo-safe | Demo players become your balance data |
| Palette unlocks | S | None | None | Demo-safe | Only if finished before Oct 7 content lock |
| Difficulty coefficient + credit spawn director | M | None | Med | Post-demo | One formula replaces per-room spawn tuning |
| Obstacle chunks in room templates | S | Low | Low | Post-demo | Reuses existing tiles |
| Isaac-style grid generator | M | Low | Med | Post-demo | Needs 15–25 hand-built rooms |
| Elite modifiers (fast, armoured, splits) | S | Low | Med | Post-demo | Outline/invert shader on existing sprites |
| Projectile modifiers (pierce, bounce, explode) | M | Low | Med | Post-demo | Foundation for relics and evolutions |
| Slot-bound upgrades, pick 1 of 3 | M | Low | Med | Post-demo | After SO upgrade-persistence fix |
| Room modifiers (dark, swarm, no-dash) | S | Low | Med | Post-demo | Replay value from same rooms |
| New enemies: splitter, turret, exploder | S each | Low–Med | Med | Post-demo | Splitter reuses sprite at smaller scale |
| New enemies: charger, sniper | M each | Med | Med | Post-demo | Telegraph line/laser does the art work |
| New guns: railgun, launcher, flamethrower | S each | Med | Med | Post-demo | 1 sprite + icon + SFX each |
| Weapon evolution (gun + relic) | M | Low | Med | Post-demo | Evolved gun = inverted sprite + trail |
| Style meter with weapon freshness | M | Low (UI) | Med | Post-demo | Only if it grants something (heal, coins) |
| Melee parry / kick | M | Med | Med | Post-demo | Strong boomer-shooter verb |
| Meta unlocks between runs | M | Low | Med | Post-demo | Needs refactor Phase 4 SaveSystem |
| Gungeon synergies | M | Low | High | Park | N items → N² pairs to test |
| Brotato stat shop | S | Low | High | Park | Stacking stats break fast |
| Summoner / necromancer | M | Med | High | Park | Spawn loops wreck enemy-count scaling |
| L4D pacing director | M | None | High | Park | Hard to feel-test without experience |
| Gungeon flow graphs | L | Low | Med | Park | Grid generator gets 80% of the value |

## Mitigations

### Art: make 1-bit a system, not a sprite list

- **One shader does variety.** A sprite material with invert, outline, dither-fade and flash parameters turns every enemy into elites, hit-flashes, evolved guns and spawn-ins. Build this once.
- **Silhouette first.** Enemies must be identifiable at 1× zoom by shape alone: swarmer = small blob, charger = wide and low, sniper = tall and thin. Test by shrinking a screenshot to 25% and naming each enemy.
- **Solid = foreground, dither = background** ([Bandur Art](https://bandurart.com/the-art-of-1-bit-game-development/)). Floors and walls use patterns; anything that can hurt you is solid.
- **4–6 frame animations maximum.** Movement reads from motion and squash, not detail.
- **One accent colour, reserved.** If you break pure 1-bit, reserve it for danger only (enemy bullets, telegraphs). Never spend it on decoration.
- **Effects over frames.** Particles, screen shake, flashes and sound carry impact that you can't animate. Gato Roboto chose 1-bit to save art time and let audio carry identity.
- **Buy the base.** Commercial 1-bit tile packs on itch.io cover floors, walls and props; spend your own pixel time on the player, enemies and guns only.

### QA and balancing: replace intuition with data

- **Balance through formulas, not numbers.** Enemy HP, damage and spawn budget all derive from one difficulty coefficient (Risk of Rain 2). You tune 3 constants, not 60 fields.
- **One tuning sheet.** Export all ScriptableObject stats to a CSV or a single "BalanceConfig" SO. Every number visible side by side; outliers become obvious.
- **Instrument every run.** Log floor reached, time per room, cause of death, damage taken per enemy type, upgrade offered vs picked, gun used at death. Slay the Spire balanced from pick rate and win rate, logged from prototype onward ([Game Developer](https://www.gamedeveloper.com/design/how-i-slay-the-spire-i-s-devs-use-data-to-balance-their-roguelike-deck-builder)).
- **Read the data with two rules.** Upgrade picked >70% of the time → too strong or others too weak. Enemy causing >40% of deaths → check its telegraph before its damage.
- **Cap combinatorics.** Slot-bound upgrades (1 per Shoot/Dash/Stomp) and no free-stacking stats keep the build space testable. This is why synergies and stat shops are parked.
- **Weapon test scene + debug console.** Already on your roadmap: spawn any enemy, grant any upgrade, set floor and coefficient. It cuts a 20-minute repro to 20 seconds.
- **Playtest script.** Give testers one goal per session ("reach the boss using only the shotgun"), watch silently, record video. Five focused testers beat fifty unfocused ones.

## Recommended shortlist and sequencing

Ship the demo with what exists, add telemetry and feel, then build the post-demo backlog in the order that lowers balancing risk first.

**Before Oct 7 content lock (demo)**

1. Game-feel pass: hit-stop, shake, invert flash, persistent casings.
2. Run telemetry: Unity Analytics custom events or a simple POST endpoint. Demo players on Steam are the largest free playtest you will get.
3. Palette unlocks only if the first two are done. Otherwise cut.

**Post-demo, in order**

1. Difficulty coefficient + credit spawn director — makes every later enemy cheaper to balance.
2. Sprite-effect shader (invert, outline, dither, flash) — unlocks elites and evolved guns for free.
3. Isaac grid generator + obstacle chunks — replaces fixed levels; needs 15–25 rooms.
4. Enemy wave 1: splitter, turret, exploder, then elites.
5. Projectile modifiers → slot-bound upgrades (pick 1 of 3) → weapon evolution.
6. Enemy wave 2 and guns: charger, sniper, railgun, launcher, flamethrower, melee parry.
7. Style meter, room modifiers, meta unlocks.

**Parked** until telemetry shows the core is balanced: synergies, stat shop, summoners, pacing director, flow graphs, curses.

## Demo visual polish

In a 1-bit game, "pop" comes from motion, timing and what the room remembers, not from sprite detail. About 30 hours of effects work fits before the Oct 7 lock, alongside the bug fixes. Everything below is code, shaders or particles on existing art.

### Tier 1 — do these first (about 12 h)

| Effect | What the player sees | Unity approach | Hours |
| --- | --- | --- | --- |
| Hit-stop | Game freezes 2–4 frames on kills; longer on the last enemy in a room and boss hits | One HitStop service listening on GameEvents; drop timeScale near zero briefly; buffer input so shots aren't lost | 1 |
| Hit flash | Enemy sprite goes fully inverted for 1–2 frames when hit | Flash parameter on a shared sprite material | 1 |
| Screen shake + kick | Shotgun shakes hard, AK buzzes lightly, camera nudges back from each shot | Cinemachine Impulse per gun, strength in the weapon SO; trauma value that decays | 2 |
| Muzzle flash | One-frame oversized white burst at the barrel | Single sprite enabled for one frame, random rotation | 1 |
| Persistent corpses + splats | Rooms end covered in bodies and black/white splatter | Stamp splat sprites into a room-sized render texture on death; corpses stay as static sprites | 4 |
| Shell casings | Brass (white) casings spray out and stay on the floor | Pooled particles or rigidbodies that stop and freeze; cap around 200 | 1 |
| Knockback + recoil | Enemies get shoved by shotgun blasts; player slides back slightly | Impulse on hit scaled by gun; short recoil on the player | 2 |

### Tier 2 — identity (about 12 h)

| Effect | What the player sees | Unity approach | Hours |
| --- | --- | --- | --- |
| Custom 2-colour palette | Not #000/#FFF but a chosen pair (e.g. deep navy + warm bone) | Full-screen shader maps black/white to two palette colours; screenshots instantly look designed | 3 |
| Dither transitions | Room and scene changes wipe with an ordered dither pattern | Threshold animation on a Bayer-matrix texture; also hides load hitches | 2 |
| Spawn-in telegraph | A dithered circle fills, then the enemy pops in | Reuse the dither shader; also prevents cheap hits | 2 |
| Invert flash | Whole screen inverts for 1 frame on room clear, boss phase change, player hit | Palette shader swaps its two colours for one frame | 1 |
| Squash and stretch | Player stretches on dash, squashes on stomp landing; enemies squash on spawn | Scale tweens on the sprite child, not the root collider | 2 |
| Stomp shockwave + dash dust | Expanding ring on stomp, dust puffs on dash, debris on bullet-wall hits | Particle systems with 1–2 simple sprites | 2 |

### Tier 3 — UI and first impression (about 6 h)

- **Chunky HUD:** health as pips, ammo as bullet icons that pop out when fired, and a bounce on every HUD change.
- **Title screen:** animated logo with the invert flash and a dither fade. This is what Steam viewers and IGDC passers-by see first.
- **Pixel-perfect camera:** Pixel Perfect Camera plus the Cinemachine pixel-perfect extension. Mixed pixel sizes and sub-pixel jitter make 1-bit look amateur faster than anything else.

### Don't

- **Bloom, chromatic aberration, CRT filters:** they blur crisp 1-bit edges into grey mush.
- **Damage numbers:** clutter in a two-colour frame. Use the hit flash instead.
- **Unlimited flashing:** full-screen inverts can trigger photosensitive seizures. Cap them at under 3 per second and add Settings toggles for screen shake and flashes before the demo ships.

**Suggested order:** Tier 1 in the order listed, then the palette shader. After that, the rest of Tier 2 reuses the same shader. Record trailer footage only after Tier 1 is in.

## Sources

- [Dungeon Generation in Enter the Gungeon — Boris the Brave](https://www.boristhebrave.com/2019/07/28/dungeon-generation-in-enter-the-gungeon/)
- [Dungeon Generation in Binding of Isaac — Boris the Brave](https://www.boristhebrave.com/2020/09/12/dungeon-generation-in-binding-of-isaac/)
- [Nuclear Throne Wiki — Enemies](https://nuclear-throne.fandom.com/wiki/Enemies)
- [Vampire Survivors Wiki — Evolution](https://vampire.survivors.wiki/w/Evolution)
- [Enter the Gungeon — Synergies (NamuWiki)](https://en.namu.wiki/w/Enter%20the%20Gungeon/%EC%8B%9C%EB%84%88%EC%A7%80)
- [ULTRAKILL Wiki — Style](https://ultrakill.wiki.gg/wiki/Style)
- [Hades Wiki — Boons](https://hades.fandom.com/wiki/Boons)
- [Risk of Rain 2 Wiki — Difficulty](https://riskofrain2.wiki.gg/wiki/Difficulty)
- [Left 4 Dead Wiki — The Director](https://left4dead.fandom.com/wiki/The_Director)
- [20 Minutes Till Dawn — Wikipedia](https://en.wikipedia.org/wiki/20_Minutes_Till_Dawn)
- [Downwell Design Analysis — Game Developer](https://www.gamedeveloper.com/design/downwell-design-analysis)
- [Gato Roboto design Q&A — Game Developer](https://www.gamedeveloper.com/design/q-a-exploring-the-design-of-cat-in-a-mech-metroidvania-i-gato-roboto-i-)
- [How Slay the Spire's devs use data to balance — Game Developer](https://www.gamedeveloper.com/design/how-i-slay-the-spire-i-s-devs-use-data-to-balance-their-roguelike-deck-builder)
- [How to Design Enemy Attack Telegraphs — Bugnet](https://bugnet.io/blog/how-to-design-enemy-attack-telegraphs)
- [The Art of 1-Bit Game Development — Bandur Art](https://bandurart.com/the-art-of-1-bit-game-development/)
