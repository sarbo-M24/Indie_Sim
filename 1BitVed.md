# 1BitVed Bible

**by Sarbojit Mandal**
**Revision 1.0 — 2026-09-28**
**Engine:** Unity 6 (6000.2.6f2), Universal Render Pipeline 2D, Cinemachine 3, Input System 1.14

> A living design document for **1BitVed**, a top-down, 1-bit-styled roguelike shooter, written in the spirit of the original *DOOM Bible*: part specification, part notebook, part honest list of what does not work yet.
>
> Every number in this document was read from the game's actual data (prefabs, ScriptableObjects, scenes) or its source code. Where something is designed but not built, it is tagged **[PLANNED]**. Where it is built but rough, it is tagged **[PARTIAL]**. Untagged items are in the current build. The complete tuning data lives in the companion workbook `Indie Sim/GameData.xlsx`.

---

## Contents

**Game Specs**

1. Development & Debug Switches
2. Intro and Demo Loop
3. Control Panel (Menus, Settings, Controls)
4. Play Loop
5. End of Game

**Game Info**

6. Characters
7. Chapter One: The Dungeons
   - 7.1 Setting
   - 7.2 Actors
   - 7.3 Unique Bits
   - 7.4 Maps
8. Chapter Two: The Boss Arena
9. Stuff: Weapons, Upgrades, Items, Etc.
10. Pitch / Press Blurb
11. Random Notes
12. Development Calendar

**Appendices**

- A. Glossary
- B. Project Layout and File Types
- C. Tools and Technology
- D. Random Extremely Important Info Too Small to Rate Its Own Section
- E. Known and Unfixed Bugs

---

# GAME SPECS

## 1. Development & Debug Switches

The original DOOM had command-line switches. 1BitVed has a handful of development-only toggles. All of them exist to speed up testing and are scheduled to be stripped or locked to development builds before a release build.

| Switch | What it does |
|---|---|
| **I** (key) | Toggles invincibility (`PlayerHealth`). Currently also enabled in non-development builds (`enableToggleInBuild`). **[stripping planned]** |
| **F1** (key) | Toggles the Upgrade Debug HUD: live `PackStats`, held cigs, equipped weapon. |
| Psychedelic debug key | Toggles the rainbow blood effect (Editor and development builds only; players use the *Psychedelic Mode* setting). |
| `DebugUpgradeInjector` | Component that grants chosen upgrades straight into the pack at scene start, bypassing coins and the shop. Currently present in the RoguelikeMode scene, granting Chain Dash, Primary Up (crit) and Stomp Circle at Tier 1. |
| Context menus | Right-click debug entries in the editor: *Skip Current Dungeon*, *Defeat Boss*, *Print Progression Stats*, *Reset All Progression*, *Buy/Burn Test Cig*, *Print Pack*, *Print Offers*. |
| Play-from-any-scene | `SceneBootstrapGuard` loads the persistent `Boot` scene additively if you press Play from any scene, so every scene is playable directly. |

## 2. Intro and Demo Loop

The build boots into a black `Boot` scene that creates the persistent services (game session, save system, achievements, cursor control) and immediately hands off to the Main Menu.

**Main Menu.** A "Welcome to the Demo" panel states plainly what the build is: *"This Demo is made for the purpose of testing the combat and the feel of the game, many of the assets are temporary, also the game isn't balanced as of yet."*

**The demo loop** is a single run:

```
Main Menu -> Dungeon 1 -> Shop -> Dungeon 2 -> Shop -> Dungeon 3 -> Boss Arena -> Demo Complete -> Main Menu
                                        (death at any point -> Retry, or Main Menu)
```

There is no attract-mode cinematic or high-score screen. **[PLANNED]**

## 3. Control Panel (Menus, Settings, Controls)

### 3.1 Main Menu

| Button | Function |
|---|---|
| **Roguelike Mode** | Starts a new run. |
| **Achievements** | Shows the three achievements with progress (see 9.6). |
| **Settings** | Opens the Settings panel. |
| **Controls** | Opens the Controls (rebinding) panel. |

Every menu is fully operable with mouse, keyboard, or gamepad. On a gamepad, focus is placed automatically and navigation is explicit, top to bottom.

### 3.2 Settings

Settings are saved to `settings.json` and applied live. Tabs switch with **LB / RB** or **Q / E**.

| Tab | Options (default) |
|---|---|
| **Audio** | Master Volume (100%), Music Volume (80%), SFX Volume (100%), UI Volume (100%), Mute When Unfocused (off) |
| **Gameplay** | Damage Numbers (on), Psychedelic Mode (off), Screen Shake (100%), Camera Lead (100%), Flash Intensity (100%, damage-flash strength, for photosensitivity) |

A *Reset to Defaults* button restores everything.

### 3.3 Controls

Every action is rebindable for both keyboard/mouse and gamepad (separate tabs), plus a **Swap Sticks** toggle that puts Move on the right stick and Aim on the left. Bindings are stored as JSON overrides.

| Action | Keyboard & Mouse | Gamepad |
|---|---|---|
| Move | W A S D | Left stick |
| Aim | Mouse position | Right stick |
| Fire (hold) | Left mouse button | Right trigger |
| Dash | Left Shift | Left trigger |
| Stomp | Space | A (south) |
| Reload | R | X (west) |
| Switch weapon | Mouse wheel (either direction) | Y (north) |
| Pause | Esc | Start |
| UI Confirm / Back | Enter / Esc | A / B |

### 3.4 Pause, Death and Tutorial Panels

- **Pause:** Resume, Settings, Controls, Main Menu. Esc / Start toggles it; it is ignored while another panel (death, countdown) has already frozen the game.
- **Death:** "You died", a run summary (Kills, Coins, Stats), **Retry** and **Main Menu**.
- **Tutorial panel:** shown when a run starts. Displays control hints (Move, Shoot, Shift, Space, Scroll, Switch) and two objectives, *"Survive & Find Teleporter"* and *"Defeat Boss"*. It counts down from 5 seconds and has a **Skip** button. The game is frozen and player input is blocked while it shows.

## 4. Play Loop

1BitVed is a top-down, real-time shooter. The camera follows the player (Cinemachine) and **leads toward the aim direction** (`Camera Lead` setting), so you see more of where you are shooting than where you are standing.

**Per dungeon:**

1. The player starts in an isolated start room of a freshly generated dungeon.
2. Enemies do not roam the whole map. **Enemy spawners** sit in the corners of rooms (every room except the start and end room) and only begin producing enemies when the player comes within 7 units. Enemies only act within 10 units of the player, so distant rooms cost nothing.
3. The player moves, aims, and shoots (hitscan cone), dashes through danger, and stomps to clear crowds and enemy bullets.
4. Enemies drop coins when killed. **Coins are also your health** (see 4.2). Killing enemies keeps you alive and also funds the shop.
5. The dungeon ends when the player steps on the **teleporter**, placed in the last room of the main path. It requires no kills. Entering it clears every remaining enemy, spawner, coin and relic on the level.
6. After dungeons 1 and 2 the **Shop** opens. After dungeon 3 the player goes straight to the **Boss Arena**.
7. A dungeon **timer** (120 s) starts with every dungeon. Running out of time kills the player. It resets on the next dungeon.

### 4.1 Combat model

- **Weapons are hitscan.** A shot damages targets instantly if they are inside the weapon's *cone*, a trapezoid whose half-angle changes with distance (all three current weapons taper, so they are widest at the muzzle). Bullet tracers are cosmetic.
- **Crits** roll per hit, so each shotgun pellet and each bounce can crit independently.
- **Damage formula:** `round((weapon base damage + slot bonus damage) x dash-window multiplier)`; a crit multiplies that by the crit multiplier.
- **Fire interval** is `1 / (weapon fire rate x (1 + fire-rate bonus))`.
- **Screen feel:** camera recoil per weapon, screen shake (scaled by setting), hit-marker crosshair, floating damage numbers, enemy squash-and-flash on hit, a damage flash on the player, and persistent blood painted onto the floor (see 7.3).

### 4.2 Health is coins

There is no separate health bar. **The player's coin balance is their health.**

- You start each run with **50 coins**. The purse holds at most **200**.
- **Any** damage source (melee, ranged bullet, boss body, boss bullet, pentagram) costs a flat **30 coins**, then grants **1 second** of invulnerability. A full purse survives six hits and dies on the seventh; your starting 50 coins survive one hit and die on the second.
- Coins you lose are shown as coins bursting off the player (visual only, they cannot be recovered).
- The shop spends the same coins. **Buying upgrades directly lowers your survivability.**
- The HUD shows the coin counter and a heart row (by default each heart = 25 coins).
- Reaching 0 coins on a hit is death.

> **Design note (open):** enemy `attackDamage` and bullet `damage` values exist in the data but the health system currently ignores them and always charges 30. See Appendix E.

### 4.3 Dash

Dash direction is the movement direction. It travels 4 units (20 u/s for 0.2 s), makes the player **invulnerable and able to pass through enemies** for the duration, and clears a 1-unit radius (no damage) when it ends. Cooldown is **3 s**. A dash into a wall stops short and is cancelled if less than 1 unit of room exists.

### 4.4 Stomp

A ground-slam around the player: **50 damage in a 2.5-unit radius**, shoves enemies out beyond the radius, destroys enemy projectiles in the radius, and shakes the camera. Cooldown **2 s**. Walls block it. It is a guaranteed kill on the base melee enemy (50 HP). Stomp has an optional coin cost in code (10 coins) that is currently switched off.

### 4.5 Reload and ammo

Each weapon has a magazine and infinite reserve. Magazines auto-reload when empty; **R / X** reloads early. **Switching weapons instantly refills every magazine.**

## 5. End of Game

A run ends in one of three ways, and each has a single code path.

- **User abort.** Pause -> Main Menu. The run ends as *not completed*; lifetime stats are saved.
- **Death.** Reaching 0 coins on a hit, or the dungeon timer expiring. The player sprite changes to a death pose, and after a 3-second delay the Death panel appears with the run summary. **Retry** starts a fresh run in Dungeon 1 (there is no boss-only retry, even if you died in the arena). **Main Menu** ends the run.
- **Victory.** Defeating the boss ends the run as *completed*, saves, and shows **Demo Complete!** with Kills, Coins and Dungeons Cleared. Its only exit is the Main Menu. There is no post-boss content in this build.

---

# GAME INFO

## 6. Characters

### The Player

No name, no backstory yet. **[PLANNED]** What the game establishes mechanically: a single fighter who carries an **assault rifle (AK47)** and a **shotgun** from the start, can **dash** and **stomp**, and whose life is literally the money in their pocket.

| | |
|---|---|
| Move speed | 4 units/s (no speed upgrades exist) |
| Starting coins (= health) | 50 (cap 200) |
| Loadout | AK47 (primary, equipped at start), Shotgun (secondary) |
| Hit cost / i-frames | 30 coins / 1 s |
| Dash | 4 u, 0.2 s, 3 s cooldown, invulnerable |
| Stomp | 50 dmg, 2.5 u radius, 2 s cooldown |
| Upgrade pack | 5 slots |

### The Shopkeeper Cat

A wise-guy cat who runs the shop and is the game's only speaking character. Its dialogue is a data asset with line pools per situation, for example:

- Shop opens: *"You come to me on my daughters Wedding ..."*
- Item selected: *"Nice choice."* / *"Ooh, that one?"* / *"Heh. Bold."*
- Purchased: *"Excellent choice."* / *"Pleasure doing business."* / *"Smoke 'em well."*
- Too poor: *"Get more coins."* / *"Can't afford that, pal."* / *"Come back with more coins."*
- Pack full: *"Your pack's full."* / *"No room in that pack."* / *"Burn something first."*
- Burning explained: *"Burn it and it hits its full potential... for this level only."*
- **Petting the cat** is an interaction with its own line pool and a squish animation.

### The Boss

An unnamed shielded, wall-bouncing boss (working file name *MiniBoss1*). See Chapter Two.

## 7. Chapter One: The Dungeons

### 7.1 Setting

The dungeons are dark, procedurally assembled stone-and-sand complexes drawn in a **1-bit palette**: white shapes on black, with blood as the only strong mark. The enemies read as occult and abyssal (a slime, a squid, an all-seeing eye that summons pentagrams). The world is drawn from monochrome tile sets, and a tutorial-panel hint reads *"Collect the Green and Blue relic to restore colour"*, which suggests colour is meant to return to the world as the player progresses. **[PLANNED]** There is no written story yet. Narrative is an open item.

The upgrade economy is themed around **cigarettes**: upgrades are *cigs*, held in a *pack*, bought by *brand* (Mild, Regular, Electric...), and *burned* for a one-level power surge. That theme is the game's main piece of original identity, and it is carried by the shop, the mascot, and the naming of every system.

### 7.2 Actors

Numbers are from the current prefabs. "u" = world units.

#### Fodder Slime ("Enemy 1", the current melee enemy)

| | |
|---|---|
| HP | 50 |
| Speed | 4 u/s |
| Behaviour | Chases the player. Within 2 u it **winds up** for 0.4 s (pulls back 0.2 u, squashing), then **lunges** (impulse 20, up to 0.6 s, slows with drag 5). Attack cooldown 1.5 s. Keeps separation (radius 1) from other slimes so they do not stack. |
| Drops | 4-8 coins (average 6) |
| Notes | The only melee enemy that currently spawns. Comes in **groups of 8** from a spawner, at most 12 per spawner. Dies to 2 AK shots, 1 shotgun blast, or 1 stomp. |

#### Enemy 2 and Enemy 3 (authored, not yet spawned)

Heavier melee variants: **100 HP** and **150 HP**, speed 4, drops 1-3 coins, with **flow-field pathfinding** (jump-flood grid, 30x30) so they route around walls. They are built but not in the spawner roster. **[PARTIAL]**

#### Squid ("Ranged Enemy", triangle body)

| | |
|---|---|
| HP | 150 |
| Speed | 3 u/s |
| Behaviour | Roams with wander turns and keeps a 2-4 u band from the player. When it has line of sight it stops, **spins to point its rear at you** (320 deg/s), then fires a **2-bullet burst** (10 deg apart) once per second. Bullets travel 4 u/s for 1 s (4 u range). If sight breaks it resumes roaming. |
| Drops | Nothing (no coin drop) |
| Notes | Up to 2 per spawner. Its bullets are destroyed by your stomp (and would be reflected by *Dash Deflect* once that cig ships). |

#### Cthulhu Eye

| | |
|---|---|
| HP | 300 |
| Speed | 2 u/s |
| Behaviour | Wakes within 20 u of the player (2 s delay). Hovers at about 5 u (+/-1). Every **5 seconds** it places a pentagram on top of the player's position: 50% an **Escape Pentagram**, 50% a **Prison Pentagram**. |
| Escape Pentagram | Radius 0.7 u. 1.5 s warning, then it punishes (30 coins) anyone still inside. Leave the circle. |
| Prison Pentagram | Radius 0.7 u. 0.8 s warning; if you are still inside, you are **held in place for 3 s**. |
| Drops | Nothing. It does not register as a kill in the run stats. **[bug]** |
| Notes | At most 1 per spawner, and only one within 25 u of another. It is the most expensive spawn (budget 100). |

#### Enemy Spawner ("the pinata")

A destructible structure fixed in room corners.

| | |
|---|---|
| HP | 300 (you can and should kill it) |
| Budget | Base 400, raised in play to about 750-900 (see 7.3) |
| Interval | Random 1-3 s per spawner, then scaled down toward 0.5 s |
| Roster | Slime (cost 10, weight 2, group of 8, max 12), Squid (cost 50, weight 1, max 2), Eye (cost 100, weight 1, max 1) |
| On death | Drops coins (remaining budget x 0.01) and greys out |
| Notes | Each spawner produces **at most 15 enemies over its whole life**. There are 1-3 spawners per eligible room. |

### 7.3 Unique Bits

**Procedural dungeons.** Every dungeon is generated from a room graph (see 7.4). Walls autotile from the final tile set; floors and foliage are picked from tile arrays. Blood is painted onto the floor in chunks (128 px chunk textures, 10 u chunks), so kills leave **persistent gore** that accumulates over a run. *Psychedelic Mode* makes that blood cycle through colours.

**Relics.** Eight relic prefabs exist. Picking one up shows a "Red Relic collected" style message. Relics have **no gameplay effect yet**; they only count toward the *Relic Hunter* achievement, and a **duplicate** relic is worth 50 coins toward your lifetime coin total. In the current RoguelikeMode scene only Relic 1 is assigned to the generator. **[PARTIAL]**

**Coins.** A coin is worth 1. The player has a magnet radius (2.6 u) that pulls coins in, collects them at 0.6 u, and pickups beyond the 200 cap are lost.

**The dungeon timer.** 120 seconds to reach the teleporter or die, reset every dungeon. Its warning turns the counter red at 15 s.

**Difficulty over a run.** After each shop the level number rises. Dungeon size grows by one main-path room with a 50% chance each time. Spawner budget rises with level, and the spawn interval shortens until it hits a 0.5 s floor. Because the generator also applies its own fixed boost, effective spawner budgets are about **750 / 850 / 900** for dungeons 1 / 2 / 3, and the interval usually sits at the 0.5 s floor. (Real scaling is still being designed. **[PLANNED]**)

**Showcase scene.** A marketing-only scene paints a generated dungeon onto the tilemaps room by room for social-media reels. It is not part of the build.

### 7.4 Maps

Dungeons are not hand-made. The generator (`DungeonMapGenerator`, configured by a `MapParametersSO`) builds a **room graph**:

- A **start room** (isolated, about 12 u from the main path) and an **end room** at least 40 u from the start.
- A **main artery** of `nodeCount` rooms, with a 30% chance per segment of an L-shaped turn. The current run builds **5 main-path rooms**, plus one more with a 50% chance after each shop.
- **Distributive nodes** off the artery (85.8% per segment), each with 1-3 **leaf rooms** on 6-u branches.
- **Corner rooms** at L-turns.
- **Corridors** 4 tiles wide.

The shipping parameter set:

| Room type | Base size (tiles) |
|---|---|
| Start | 6 x 6 |
| Main artery | 7 x 7 |
| Distributive | 5 x 5 |
| Leaf | 5 x 5 |
| Corner | 4 x 4 |
| End | 8 x 8 |

Sizes vary by +/-33%, rooms may rotate (50%), and placement retries up to 30 times to avoid overlaps. Two more presets exist (*Big* and a dense, wide-corridor variant) for later use.

Spawners go on corner-type edge tiles (edge tiles with exactly two adjacent walls), at least 5 u apart. A relic is rolled for each eligible room (25%), never in the key room. The **teleporter** is placed in the last main-path room. A **key** system exists and is switched off (the teleporter is always usable).

## 8. Chapter Two: The Boss Arena

After the third dungeon the teleporter loads the **Boss Arena**, a single scene reused for any boss (the boss is a data asset, `BossDefinition_01`). The player carries coins, upgrades, ammo and equipped weapon in from the dungeons.

### 8.1 The boss (working name *MiniBoss1*)

A shielded pinball. The fight is about **exploiting a rhythm**, not out-DPSing a health bar.

| | |
|---|---|
| Shield | 1000 (must break first) |
| HP | 4000 (protected while the shield is up) |
| Coins on death | 200-250 |
| Contact | Hits the player on touch (1.5 s cooldown) |

**The cycle:**

1. **Shielded pinball.** The boss ricochets around the arena. Each wall bounce adds +1 u/s of speed, from a base of 6 up to 20. Damage goes into the shield.
2. **Shield breaks.** The boss becomes vulnerable and continues until it hits the next wall.
3. **Wall stick.** It sticks to the wall for about **5.2 seconds**, changes colour, and fires **rings of 18 bullets** (one every 20 degrees, then a second ring offset by 10 degrees 0.3 s later, 1.3 s per cycle, 4 cycles per stick) at 8 u/s for 5 s. Bullets are not spawned into walls. Your HP damage lands here.
4. **Reset.** When it leaves the wall the **shield fully regenerates** (1000). Boss HP does **not** regenerate.

So the boss must be killed across several shield-break windows. A stomp deletes bullets in radius. Bullet-reflecting *Dash Deflect* is designed for exactly this fight but is not yet in the shop.

**Defeat:** the boss dies, drops its coins, and the run completes (Demo Complete screen).

### 8.2 Coming later

More bosses reuse the same arena scene via `BossDefinition`. Variants of the current boss (same logic, different tuning) are the plan. **[PLANNED]**

## 9. Stuff: Weapons, Upgrades, Items, Etc.

### 9.1 Weapons

Two weapons are in the player's loadout. A third exists as an asset and is not equipped.

| | **AK47** (primary) | **Shotgun** (secondary) | *Pistol* (not equipped) |
|---|---|---|---|
| Type | Standard: hits the closest target | Shotgun: one pellet per enemy in a spread | Piercer: line through enemies |
| Fire rate | 10 shots/s | 3 shots/s | 2 shots/s |
| Damage per shot | 25 | 150 (per enemy hit) | 100 |
| Pellets | 1 | 6 (+ upgrades) | 1 |
| Magazine | 60 | 2 | 12 |
| Reload | 0.8 s | 1.0 s | 0.5 s |
| Cone (half-angle, muzzle to max range) | 25 deg tapering to 11.25 deg, range 30 u | 20 deg to 10.7 deg, 30 u | 15 deg to 10.5 deg, 30 u |
| Pierce | n/a | n/a | 10 enemies |
| Extras | | Recoil pushes the player back (knockback 10) | |
| Burst DPS per target hit | 250 | 450 (on each of up to 6 targets at once) | 200 (on each of up to 10 targets) |

Shooting works in a **trapezoid cone**: the half-angle moves from `coneAngle x baseWidth` near the gun to `coneAngle x topWidth` at maximum range (for the current weapons that means narrowing). **Note:** a shotgun blast gives each *different* enemy at most one pellet, so 6 pellets means "up to 6 targets," not 6x damage on one; extra pellets from upgrades only help against groups.

### 9.2 The Pack and the Cigs (upgrades)

A run's power comes entirely from the **pack**: 5 slots holding *cigs*. There are **12 cigs in the shop pool** (one *lineage* each), plus one more effect (Dash Deflect) that is coded but has no asset yet. Buying immediately activates. There are no unlocks and nothing carries between runs.

**Rules:**

- A cig bought once is removed from the shop pool for the whole run.
- Buying a **higher-tier** roll of a lineage you hold **replaces** it in the same slot (no stacking).
- **Brand exclusivity:** *Mild* and *Regular* cigs targeting the same slot are mutually exclusive; buying the second warns and asks you to replace the first. *Electric* cigs (and future brands) never conflict.
- **Tier (1-4)** is rolled per offer, uniformly. **Rarity** (Common, Uncommon, Rare, Epic, uniform) is rolled only for cigs that use it and multiplies the cig's *damage-type* values by +0% / +10% / +25% / +50%.
- **Burning.** Any held cig can be burned in the shop: for the **next level only** it works at **Tier 4** (keeping its rarity), then it is **removed** and the slot is freed. Burning is the only way to free a slot.
- Buy price is 10 coins for every cig except *Inc Secondary Pellets* (0, likely an oversight).
- The shop shows **3 offers**; **Reshuffle** costs 5 coins, +50% each time you reshuffle in the same visit.

### 9.3 The catalog

| Slot | Cig | Brand | Rarity? | Effect (Tier 1 / 2 / 3 / 4) |
|---|---|---|---|---|
| Primary | **Primary Up** | Mild | yes | Crit chance +10 / 20 / 30 / 45%. Crit multiplier 1.5x, boosted by rarity (up to 1.75x at Epic). |
| Primary | **Primary Bullet Bounce** | Regular | yes | +5 / 10 / 15 / 20 flat damage, and bullets bounce to 1 / 2 / 3 / 4 more enemies. |
| Primary | **Primary Firerate** | Electric | no | +10 / 20 / 30 / 45% fire rate. |
| Secondary | **Secondary Up** | Mild | yes | Same crit as Primary Up, for the shotgun. |
| Secondary | **Secondary Bullet Bounce** | Regular | yes | Same as Primary Bullet Bounce, for the shotgun. |
| Secondary | **Inc Secondary Pellets** | Electric | no | +1 / 2 / 3 / 4 shotgun pellets. |
| Stomp | **Stomp Power** | Mild | yes | +2 / 5 / 9 / 14 damage and +1 / 2 / 3 / 4 u radius. |
| Stomp | **Stomp Circle** | Regular | yes | +2 / 5 / 9 / 14 damage and a ring of 4 / 6 / 8 / 10 bullets (each does the full stomp damage). |
| Stomp | **Chain Stomp** | Electric | no | +1 / 2 / 3 / 4 extra stomp charges, at +0.5 / 1 / 1.5 / 2 s added to the cooldown. |
| Dash | **Dash PostDmg** | Mild | no | +25 / 50 / 75 / 100% damage for 2 s after a dash (player glows red). |
| Dash | **Dash AOE Dmg** | Regular | yes | While dashing: 8 / 14 / 20 / 28 damage and 12 / 16 / 20 / 25 knockback to enemies within 2 u (once per enemy per dash). |
| Dash | **Chain Dash** | Electric | no | +1 / 2 / 3 / 4 extra dash charges, at +0.25 / 0.5 / 0.75 / 1 s added to the cooldown. |
| Dash | *Dash Deflect* **[PARTIAL]** | Electric | no | Enemy bullets you dash into are reflected back. The effect is implemented, but no asset exists for it, so it is not in the shop. |

The catalog supports these brand tiers for later: *Hard, Mint, Slims, Clove.* **[PLANNED]**

Chain charges refill all at once after the (lengthened) cooldown. Effects stack additively across held cigs; a cig contributes through a single pull-based `PackStats` recalculation, so the numbers in the HUD tooltips are always the same numbers the game uses.

### 9.4 The Shop

Opens automatically after dungeons 1 and 2 (not before the boss). The screen shows:

- **3 offer cards** (name and cost only; hovering or focusing a card enlarges it and shrinks the others).
- A **tooltip** that follows the cursor (or anchors to the focused card on gamepad), showing the description and a **before / after stat preview** computed by the real stat system.
- Your **pack** (5 slots) as cigarettes. Select one to enable **Burn**.
- **Buy**, **Burn**, **Reshuffle** and **Continue** buttons.
- The **mascot cat** reacts to selecting, buying, failing to buy, a full pack, and burning.
- If a cig would replace a same-slot Mild/Regular, a confirmation panel appears first.
- A HUD element shows which pack slots are burning during the next level.

### 9.5 Items and pickups

| Item | Function |
|---|---|
| Coin | +1 coin (also health). |
| Relic (8 designs) | Counts toward an achievement; duplicate = +50 lifetime coins. |
| Key | Present and disabled. |
| Teleporter | Ends the dungeon (cyan pulsing pad, 2 u trigger, 1 s activation). |

### 9.6 Achievements

| Achievement | Requirement | Advertised reward |
|---|---|---|
| *Legendary Executioner* | Kill 1000 enemies (lifetime) | Bonus starting coins (100 or 300, advertised) |
| *Golden Hoarder* | Collect 1000 coins (lifetime) | Bonus starting coins (100 or 300, advertised) |
| *Relic Hunter* | Collect all 8 relics in a single run | Unlock the Shotgun at the start |

The Achievements screen advertises rewards ("Start with 100", "Start with 300", "Unlock Shotgun at the start"). **The rewards are not yet granted in code.** **[PLANNED]**

### 9.7 Persistence

Only lifetime stats persist: total coins ever collected, total enemies killed, unlocked achievements, total runs, best dungeons cleared, and a demo-completed flag (JSON in the platform's persistent data path). Everything else, coins, pack, weapons, dungeon level, resets on every new run.

## 10. Pitch / Press Blurb

> **1BitVed**
>
> Step into a black-and-white hellscape where every coin you own is a heartbeat.
>
> **1BitVed** is a fast top-down roguelike shooter about spending your life to get stronger. Hold the line with an assault rifle and a shotgun, dash through bullets, and slam the ground to clear the room. Slaughter your way to the teleporter before the clock kills you. Then visit the cat.
>
> The cat sells **cigarettes**. Each one bends your weapons, your dash, or your stomp, and you only have room for five. Burn one and it will hit at full power for a single level, then it is gone forever. What you spend in the shop is what you will not have when the next lunge lands.
>
> - Coin-as-health: every purchase is a survival decision
> - Hitscan cone shooting with crits, bounces and shotgun spreads
> - A pack of five upgrades and a burn mechanic that trades permanence for power
> - Procedurally built dungeons, persistent gore, a shielded pinball boss
> - Full gamepad and rebindable keyboard/mouse support

## 11. Random Notes

Important, unorganized notes concerning the overall design. Anything of real interest to someone other than me goes in the appendices.

### 11.1 Pillars

1. **Every coin is a decision.** Currency and life are the same resource.
2. **Small, readable enemies.** Simple shapes with one clear behaviour: lunge, shoot, curse.
3. **Power from a pack, not a tree.** Five slots, no stacking, burn to spike.
4. **Feel first.** Recoil, shake, hit-markers and gore are treated as gameplay.
5. **Short runs.** A full demo run is three dungeons and a boss.

### 11.2 Design decisions worth remembering

- **One boss scene, data-driven.** A `BossDefinition` asset picks the boss. A second boss is a new asset, not a new scene.
- **Boss defeat ends the run.** No loop back; there is one terminal success state.
- **Health is coins.** Chosen to fuse the economy and survival loops. Pending: whether enemies should hurt by their own damage value.
- **No unlocks, no stacking, no persistent power.** The only meta-progression is achievements, and their rewards are still to come.
- **Burning is universal.** Rather than a special "temporary boost" cig type, every upgrade can be burned.
- **Difficulty from dungeon level only**, never from upgrades. No hidden heat counter.
- **Three lifetime scopes** (persistent, run, level): persistent objects own no run state, scene objects own no data that must outlive their scene. This came out of an architecture refactor that fixed a whole class of "state leaks between runs" bugs.

### 11.3 Ideas parking lot

- Colour returning to the world with relics ("restore colour").
- More cig brands (Hard, Mint, Slims, Clove) with their own rules.
- Relics with real effects.
- Boss variants; more enemy types (Enemy 2 and 3 are already authored).
- A narrative for the player, the cat, and the boss.

## 12. Development Calendar

| Period | Focus |
|---|---|
| Foundations | Player, cone shooting, dash, stomp, procedural dungeons, enemies, coins-as-health. |
| Architecture refactor (Phases 1-6) | Persistent boot scene, lifetime-scoped state (`GameSession`), save system, run lifecycle funnel, camera ownership, de-persisted managers, per-scene player spawning. |
| Upgrade System | Pack, cigs, burning, brand exclusivity, the shop with the cat, tooltips and stat previews. |
| Settings, audio and input | Settings panel, sound manager, unified Input System, gamepad support and menus, rebinding, swap sticks. |
| **Target: playable demo** | A playable Steam demo aimed at IGDC in late October 2026. |
| Post-demo | Event bus and unified enemy death (Phase 7), real enemy scaling, boss variants, art/audio/animation polish, relic effects, narrative. |

---

# APPENDICES

## A. Glossary

| Term | Meaning |
|---|---|
| **Cig** | An upgrade. One asset = one *lineage*. |
| **Pack** | The 5-slot container of held cigs. |
| **Brand** | Mild, Regular, Hard, Mint, Slims, Clove, Electric. Mild and Regular are mutually exclusive per slot. |
| **Burn** | Play a held cig at Tier 4 for the next level, then remove it. |
| **Lineage** | A unique upgrade (`CigData.id`). Only one of a lineage is held at a time. |
| **Slot (target slot)** | Primary weapon, Secondary weapon, Stomp, or Dash. |
| **Tier** | 1-4 strength of a cig roll. |
| **Rarity** | Common / Uncommon / Rare / Epic: a bonus to damage-type values. |
| **Cone** | A weapon's trapezoid hit area. |
| **Spawner ("pinata")** | A destructible structure that spawns enemies for a budget. |
| **Artery** | The main chain of rooms from start to end. |
| **Run** | Main Menu until death or demo completion. |
| **Level** | One dungeon generation, or the boss encounter. |

## B. Project Layout and File Types

```
Indie_Sim/
  1BitVed.md                     this document
  doombible.pdf                  reference for this document's structure
  Indie Sim/                     the Unity project
    GameData.xlsx                tuning data (companion workbook)
    UpgradeSystemSpec.md         upgrade system design spec
    architecture-refactor-plan-v3.md  lifetime-scope architecture plan
    Assets/
      Scenes/     Boot, Main menu, RoguelikeMode, BossArena (+ test scenes)
      Scripts/    Managers, PlayerController, Enemy, Weapons, Upgrades, UI,
                  Dungeon Generator, CameraTouch, Input, Settings, Audio
      Prefabs/    Player, Enemies, Upgrades (cig assets), Relics, UI, Effects
      Audio/  2d Assets/  Animation/  Materials/  Resources/
```

| Extension | Use |
|---|---|
| `.unity` | Scenes. |
| `.prefab` | Player, enemies, spawner, relics, UI canvases, effects. |
| `.asset` | ScriptableObjects: weapons (`WeaponData`), cigs (`CigData` subclasses), rarity config, map parameters, boss definition, shop dialogue. |
| `.inputactions` | The single shared `PlayerControls` (Player and UI maps, Keyboard&Mouse and Gamepad schemes). |
| `settings.json` | Player settings, in the persistent data path. |
| save JSON | Lifetime stats, in the persistent data path. |

## C. Tools and Technology

- **Unity 6 (6000.2.6f2)**, URP 2D lighting and shadow casters, Tilemaps.
- **Cinemachine 3.1** for the follow camera, with custom lead, recoil, shake and speed-zoom behaviours.
- **Input System 1.14** for all gameplay and UI input, with rebinding and gamepad menus.
- **TextMeshPro / uGUI** for UI.
- Custom procedural dungeon generator (room graph, corridors, autotiled walls).
- Custom jump-flood pathfinding for the heavier melee enemies.
- Object pooling for bullets.
- JSON saves and settings.

**Third-party assets:** a 1-bit monochrome tile set, 2D Pixel Dungeon Asset Pack v2.0, Joystick Pack (touch input), Free Game Control Icons (controller button glyphs), Freesound-sourced sound effects, and placeholder music. A full attribution list will accompany the release. **[PLANNED]**

**Credits to include:**
- **Free Game Control Icons** by CCStudios (https://codecrash.itch.io/free-game-icons). Xbox, PlayStation and PC button icons, used for the controller hint rows and the shop's RB/R1 glyph (`Controller Glyph Set.asset`). Free for commercial use. The creator says credit isn't required, but we credit it anyway.

## D. Random Extremely Important Info Too Small to Rate Its Own Section

- **The tuning numbers live in `GameData.xlsx`**, including derived time-to-kill and upgrade matrices. If this document and the workbook ever disagree, the workbook was extracted from the game data more recently.
- The whole game is hitscan. The "bullets" you see from your own gun are a visual only.
- Music dynamically layers an ambient track and a "power" track according to how many enemies are within 10 u of the player, with separate teleporter ambience. The music and most art in this build are temporary.
- Enemy contact damage, the boss's contact damage, and every bullet share one damage rule: 30 coins and a second of invulnerability.
- The game is playable from any scene in the editor; missing persistent services are bootstrapped on demand.
- Touch and virtual-joystick input code from an earlier mobile experiment is still in the project but is not part of the current build targets.

## E. Known and Unfixed Bugs

We know these exist and have not tracked or decided on them yet.

- **Damage values ignored.** `attackDamage` on enemies, `bulletDamage` on ranged enemies and the boss, and the pentagram `damage` are stored but not used; every hit costs a flat 30 coins.
- **Spawner interval and scaling are partly overwritten.** The prefab's `spawnInterval` (0.7 s) is replaced by a random 1-3 s at generation, and the generator applies a fixed level-7 difficulty boost regardless of the actual run level, so dungeons 1-3 feel nearly identical in pressure.
- **Spawner caps are lifetime, not live.** A spawner's "max allowed" counters never decrease, so a spawner can only ever produce 15 enemies. Its 750+ budget is never spent.
- **Relic Hunter is currently unreachable.** Only Relic 1 is assigned to the dungeon generator in RoguelikeMode, so eight unique relics cannot appear in a run.
- **Relics have no effect**, and a duplicate relic credits only the lifetime stat, not the coins you can spend.
- **Achievement rewards are not implemented** (bonus starting coins, shotgun unlock). The Shotgun is already in the loadout.
- **Cthulhu Eye and Squid drop no coins**, and the Eye is not counted as a kill.
- **Switching weapons refills every magazine.** Possibly intended, possibly an exploit; undecided.
- **Boss data leftovers.** `bulletsPerWave` and `directionChangeInterval` on the boss are unused; boss bullet count is derived from the angle between bullets.
- **Scene values override script defaults**, which can mislead: rooms-till-boss is 3 in the scene (the script's default and comment say 5), the 120-second dungeon timer is enabled in the scene, and coins per hit is 30 in the prefab (the script default is 100).
- **Debug leftovers in the build:** the invincibility key and the scene-placed upgrade injector (see Section 1).
- **The pistol is orphaned:** the asset exists but is not carried.
- **Ammo UI in the Boss Arena** was reported as not displaying in an earlier test pass; its current status needs re-verifying.
- **Startup lag** of roughly half a second to one second when entering Play from the Main Menu or a gameplay scene; the source is not confirmed.
- **Shared player canvas** has accumulated unused objects and needs to be split into per-system canvases (HUD, shop, death).
- **Static debug/legacy input** still exists on a few debug keys and the touch script.

---

*A Production of 1BitVed. Revision 1.0. Numbers current as of 2026-09-28.*
