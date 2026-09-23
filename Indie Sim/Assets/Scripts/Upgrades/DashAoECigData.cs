using UnityEngine;

/// <summary>
/// While the player dashes, a radius around them continuously pushes enemies
/// away and damages each one once per dash (PlayerController tracks hits per
/// dash so repeated ticks don't re-damage an enemy still inside the radius).
/// Both damage and radius scale per tier, with a rarity bonus boosting both.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Dash AoE Cig")]
public class DashAoECigData : CigData
{
    [Tooltip("Damage dealt once per enemy per dash, at each tier. Index 0 unused (tiers are 1-4).")]
    [SerializeField] private int[] damagePerTier = { 0, 8, 14, 20, 28 };
    [Tooltip("Radius around the player, continuously active while dashing, at each tier. Index 0 unused (tiers are 1-4).")]
    [SerializeField] private float[] radiusPerTier = { 0f, 1.5f, 2f, 2.5f, 3f };
    [SerializeField] private RarityConfig rarityConfig;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = Mathf.Clamp(instance.EffectiveTier, 0, damagePerTier.Length - 1);
        float rarityBonus = rarityConfig != null ? rarityConfig.GetBonus(instance.RolledRarity) : 0f;

        int tierDamage = damagePerTier[tier];
        float tierRadius = radiusPerTier[Mathf.Clamp(tier, 0, radiusPerTier.Length - 1)];

        stats.DashAoeDamage += Mathf.RoundToInt(tierDamage + rarityBonus * tierDamage);
        stats.DashAoeRadius += tierRadius + rarityBonus * tierRadius;
    }
}
