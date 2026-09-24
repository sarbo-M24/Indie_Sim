using UnityEngine;

/// <summary>
/// Regular / Dash: while dashing, a radius around the player pushes enemies
/// away and damages each one once per dash (PlayerController tracks hits per
/// dash). Tiers scale damage and knockback force; rarity scales both. The
/// radius is a fixed, untiered value.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Dash AoE Cig")]
public class DashAoECigData : CigData
{
    [Tooltip("Damage dealt once per enemy per dash, per tier, before the rarity bonus.")]
    [SerializeField] private TierValuesInt damagePerTier = new TierValuesInt(8, 14, 20, 28);
    [Tooltip("Outward impulse applied once per enemy per dash, per tier, before the rarity bonus. For scale: Enemy.knockbackStrength (a regular hit) is 10.")]
    [SerializeField] private TierValues knockbackPerTier = new TierValues(12f, 16f, 20f, 25f);
    [Tooltip("Radius around the player while dashing — fixed, not tiered.")]
    [SerializeField] private float radius = 2f;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = instance.EffectiveTier;
        float rarityScale = 1f + GetRarityBonus(instance);

        stats.DashAoeDamage += Mathf.RoundToInt(damagePerTier.Get(tier) * rarityScale);
        stats.DashAoeKnockback += knockbackPerTier.Get(tier) * rarityScale;
        stats.DashAoeRadius = Mathf.Max(stats.DashAoeRadius, radius);
    }
}
