# Upgrade Catalog Snapshot — as of 2026-09-23

Raw data pulled directly from every `CigData` asset in `Assets/Prefabs/Upgrades/`, for reference while designing the brand/exclusivity rework. `targetSlot` and `brand` are shown as both their raw serialized int and their enum name (`Assets/Scripts/Upgrades/CigEnums.cs`):

```csharp
public enum Brand { Mild, Regular, Hard, Mint, Slims, Clove, Electric }
public enum TargetSlot { PrimaryWeapon, SecondaryWeapon, Stomp, Dash }
```

Every asset currently has `brand: 0` (Mild) — the field exists in `CigData.cs` but has never actually been assigned/used yet.

| Asset file | Class | id | displayName | targetSlot | brand | hasTierRarity | cost | Effect-specific fields |
|---|---|---|---|---|---|---|---|---|
| `Primary Dmg & Crit.asset` | `CritChanceCigData` | 1 | Primary Up | 0 = PrimaryWeapon | 0 = Mild | true | 10 | `critChancePerTier` [0, .1, .2, .3, .45], `critDamageMultiplier` 1.5, `damagePerTier` [5, 10, 15, 20, 25] |
| `Secondary Dmg & Crit.asset` | `CritChanceCigData` | 2 | Secondary Up | 1 = SecondaryWeapon | 0 = Mild | true | 10 | `critChancePerTier` [0, .1, .2, .3, .45], `critDamageMultiplier` 1.5, `damagePerTier` [5, 10, 15, 20, 25] |
| `PrimaryBullet Bounce.asset` | `BulletBounceCigData` | 3 | Primary Bullet Bounce | 0 = PrimaryWeapon | 0 = Mild | false | 10 | `bounceCount` 2 |
| `Secondary Bullet Bounce.asset` | `BulletBounceCigData` | 4 | Secondary Bullet Bounce | 1 = SecondaryWeapon | 0 = Mild | false | 10 | `bounceCount` 2 |
| `Stomp Seek.asset` | `StompSeekCigData` | 5 | Stomp Seek | 2 = Stomp | 0 = Mild | true | 10 | `radiusPerTier` [0, 1, 2, 3, 4], `damagePerTier` [0, 2, 5, 9, 14] |
| `Stomp Circle.asset` | `StompCircleCigData` | 6 | Stomp Circle | 2 = Stomp | 0 = Mild | true | 10 | `bulletCountPerTier` [2, 4, 6, 8, 10], `damagePerTier` [1, 2, 5, 9, 14] |
| `Dash Post Dmg.asset` | `DashPostDamageCigData` | 7 | Dash PostDmg | 3 = Dash | 0 = Mild | true | 10 | `damageMultiplierPerTier` [1, 1.25, 1.5, 1.75, 2], `durationSeconds` 2 |
| `Dash AOEDmg.asset` | `DashAoECigData` | 8 | Dash AOE Dmg | 3 = Dash | 0 = Mild | true | 10 | `damagePerTier` [4, 8, 14, 20, 28], `radiusPerTier` [1, 1.5, 2, 2.5, 3] |
| `Dash Deflect.asset` | `DashDeflectCigData` | 9 | Dash Deflect | 3 = Dash | 0 = Mild | false | 10 | (pure flag, no magnitude fields) |
| `Chain Dash.asset` | `ChainDashCigData` | 10 | Chain Dash | 3 = Dash | 0 = Mild | false | 10 | `extraCharges` 2 |
| `Chain Stomp.asset` | `ChainStompCigData` | 11 | Chain Stomp | 2 = Stomp | 0 = Mild | false | 10 | `extraCharges` 2 |
| `Primary FireRate.asset` | `FireRateCigData` | 12 | Primary Firerate | 0 = PrimaryWeapon | 0 = Mild | true | 10 | `fireRateBonusPerTier` [.5, .1, .2, .3, .45] *(index 0 looks like a leftover placeholder — should probably be 0, see note below)* |
| `Secondary Firerate.asset` | `FireRateCigData` | 13 | Secondary Firerate | 1 = SecondaryWeapon | 0 = Mild | true | 10 | `fireRateBonusPerTier` [.5, .1, .2, .3, .45] *(same note)* |

All tiered assets share the same `Rarity Config.asset` (`common: 0, uncommon: 0.1, rare: 0.25, epic: 0.5`). All 13 share the same placeholder `icon`.

## Per-slot breakdown

- **PrimaryWeapon (3):** Crit (1), Bounce (3), FireRate (12)
- **SecondaryWeapon (3):** Crit (2), Bounce (4), FireRate (13)
- **Stomp (3):** Seek (5), Circle (6), Chain Stomp (11)
- **Dash (4):** Post Dmg (7), AoE (8), Deflect (9), Chain Dash (10)

## Note spotted while pulling this data (not fixed, flagging only)

`Primary FireRate.asset` and `Secondary Firerate.asset` both have `fireRateBonusPerTier[0] = 0.5` instead of `0`. Every other tiered asset's index 0 (the "tier 0 / unused" slot per `FireRateCigData`'s own doc comment — tiers are 1-4) is `0`. Since `CigInstance.RolledTier` is always 1-4 in practice, this is currently harmless, but worth zeroing out for consistency if it was a typo during creation.
