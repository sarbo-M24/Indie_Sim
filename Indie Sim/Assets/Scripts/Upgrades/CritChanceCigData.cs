using UnityEngine;

/// <summary>
/// Weapon upgrade for a single slot: crit chance AND flat weapon damage,
/// tiers + rarities. Created as two separate asset instances (one for
/// PrimaryWeapon, one for SecondaryWeapon per `targetSlot`), each
/// independently tunable; which PackStats bucket a given instance writes
/// into is decided at Contribute() time from `targetSlot`.
///
/// Damage formula (per the user's spec): FinalDmg = WeaponBaseDmg (owned by
/// WeaponData, read in PlayerConeShooter) + UpgradeDmg (damagePerTier below)
/// + (RarityBonus x UpgradeDmg). This class only contributes the
/// UpgradeDmg + RarityBonus*UpgradeDmg portion as *WeaponBonusDamage;
/// PlayerConeShooter.GetDynamicWeaponDamage() adds it to the weapon's own
/// base damage.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Crit Chance Cig")]
public class CritChanceCigData : CigData
{
    [Tooltip("Crit chance granted at each tier. Index 0 unused (tiers are 1-4).")]
    [SerializeField] private float[] critChancePerTier = { 0f, 0.10f, 0.20f, 0.30f, 0.45f };
    [Tooltip("Damage multiplier applied on a crit — flat, not tiered.")]
    [SerializeField] private float critDamageMultiplier = 1.5f;
    [Tooltip("Flat weapon damage bonus (UpgradeDmg) granted at each tier, before the rarity bonus. Index 0 unused (tiers are 1-4).")]
    [SerializeField] private int[] damagePerTier = { 0, 2, 5, 9, 14 };
    [SerializeField] private RarityConfig rarityConfig;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = Mathf.Clamp(instance.EffectiveTier, 0, critChancePerTier.Length - 1);
        float rarityBonus = rarityConfig != null ? rarityConfig.GetBonus(instance.RolledRarity) : 0f;

        float chance = critChancePerTier[tier] + rarityBonus;

        int upgradeDmg = damagePerTier[Mathf.Clamp(tier, 0, damagePerTier.Length - 1)];
        int bonusDamage = Mathf.RoundToInt(upgradeDmg + rarityBonus * upgradeDmg);

        if (targetSlot == TargetSlot.SecondaryWeapon)
        {
            stats.SecondaryCritChance += chance;
            stats.SecondaryCritMultiplier += critDamageMultiplier - 1f; // baseline is already 1f
            stats.SecondaryWeaponBonusDamage += bonusDamage;
        }
        else
        {
            stats.PrimaryCritChance += chance;
            stats.PrimaryCritMultiplier += critDamageMultiplier - 1f;
            stats.PrimaryWeaponBonusDamage += bonusDamage;
        }
    }
}
