using UnityEngine;

/// <summary>
/// Stomp seeks enemies in a range instead of only hitting directly under the
/// player — tiers + rarities (range, damage).
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Stomp Seek Cig")]
public class StompSeekCigData : CigData
{
    [Tooltip("Bonus stomp radius granted at each tier. Index 0 unused (tiers are 1-4).")]
    [SerializeField] private float[] radiusPerTier = { 0f, 1f, 2f, 3f, 4f };
    [Tooltip("Bonus stomp damage granted at each tier, before the rarity bonus. Index 0 unused.")]
    [SerializeField] private int[] damagePerTier = { 0, 2, 5, 9, 14 };
    [SerializeField] private RarityConfig rarityConfig;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = Mathf.Clamp(instance.EffectiveTier, 0, radiusPerTier.Length - 1);
        float rarityBonus = rarityConfig != null ? rarityConfig.GetBonus(instance.RolledRarity) : 0f;
        int damage = damagePerTier[Mathf.Clamp(tier, 0, damagePerTier.Length - 1)];

        stats.StompBonusRadius += radiusPerTier[tier];
        stats.StompBonusDamage += Mathf.RoundToInt(damage + rarityBonus * damage);
    }
}
