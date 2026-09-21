using UnityEngine;

/// <summary>
/// Stomp gains increased damage and releases a circle of bullets around the
/// player — tiers + rarities (damage, bullet count).
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Stomp Circle Cig")]
public class StompCircleCigData : CigData
{
    [Tooltip("Bullets fired in the ring at each tier. Index 0 unused (tiers are 1-4).")]
    [SerializeField] private int[] bulletCountPerTier = { 0, 4, 6, 8, 10 };
    [Tooltip("Bonus stomp damage granted at each tier, before the rarity bonus. Index 0 unused.")]
    [SerializeField] private int[] damagePerTier = { 0, 2, 5, 9, 14 };
    [SerializeField] private RarityConfig rarityConfig;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = Mathf.Clamp(instance.EffectiveTier, 0, bulletCountPerTier.Length - 1);
        float rarityBonus = rarityConfig != null ? rarityConfig.GetBonus(instance.RolledRarity) : 0f;
        int damage = damagePerTier[Mathf.Clamp(tier, 0, damagePerTier.Length - 1)];

        stats.StompBulletCount += bulletCountPerTier[tier];
        stats.StompBonusDamage += Mathf.RoundToInt(damage + rarityBonus * damage);
    }
}
