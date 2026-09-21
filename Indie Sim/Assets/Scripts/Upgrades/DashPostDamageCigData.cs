using UnityEngine;

/// <summary>
/// Deal more damage for a fixed window after dashing (player tints red while
/// active) — tiers + rarities (damage %, visual).
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Dash Post-Damage Cig")]
public class DashPostDamageCigData : CigData
{
    [Tooltip("Damage multiplier during the post-dash window at each tier. Index 0 unused (tiers are 1-4).")]
    [SerializeField] private float[] damageMultiplierPerTier = { 1f, 1.25f, 1.5f, 1.75f, 2f };
    [Tooltip("Fixed window duration in seconds — not tiered, per the catalog.")]
    [SerializeField] private float durationSeconds = 2f;
    [SerializeField] private RarityConfig rarityConfig;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = Mathf.Clamp(instance.EffectiveTier, 0, damageMultiplierPerTier.Length - 1);
        float baseMultiplier = damageMultiplierPerTier[tier];
        float rarityBonus = rarityConfig != null ? rarityConfig.GetBonus(instance.RolledRarity) : 0f;
        // Rarity scales the bonus portion of the multiplier, not the whole 1x baseline.
        float finalMultiplier = baseMultiplier + rarityBonus * (baseMultiplier - 1f);

        stats.DashDamageWindowMultiplier += finalMultiplier - 1f; // baseline is already 1f
        stats.DashDamageWindowDuration = durationSeconds;
    }
}
