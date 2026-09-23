using UnityEngine;

/// <summary>
/// Weapon upgrade for a single slot: flat bullet bounce + flat weapon damage
/// (damage has tiers + rarity, bounce count does not). Created as two
/// separate asset instances (one for PrimaryWeapon, one for SecondaryWeapon
/// per `targetSlot`). The damage portion moved here from CritChanceCigData,
/// which is now crit-chance-only.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Bullet Bounce Cig")]
public class BulletBounceCigData : CigData
{
    [Tooltip("Flat bounce count granted — does not scale with tier.")]
    [SerializeField] private int bounceCount = 1;
    [Tooltip("Flat weapon damage bonus granted at each tier, before the rarity bonus. Index 0 unused (tiers are 1-4).")]
    [SerializeField] private int[] damagePerTier = { 0, 5, 10, 15, 20 };
    [SerializeField] private RarityConfig rarityConfig;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = Mathf.Clamp(instance.EffectiveTier, 0, damagePerTier.Length - 1);
        float rarityBonus = rarityConfig != null ? rarityConfig.GetBonus(instance.RolledRarity) : 0f;

        int tierDamage = damagePerTier[tier];
        int bonusDamage = Mathf.RoundToInt(tierDamage + rarityBonus * tierDamage);

        if (targetSlot == TargetSlot.SecondaryWeapon)
        {
            stats.SecondaryBounceCount += bounceCount;
            stats.SecondaryWeaponBonusDamage += bonusDamage;
        }
        else
        {
            stats.PrimaryBounceCount += bounceCount;
            stats.PrimaryWeaponBonusDamage += bonusDamage;
        }
    }
}
