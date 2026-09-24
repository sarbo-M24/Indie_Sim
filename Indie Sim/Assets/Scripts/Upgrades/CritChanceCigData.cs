using UnityEngine;

/// <summary>
/// Mild / Primary or Secondary: crit chance. Created twice (one asset per
/// weapon `targetSlot`). Tiers scale crit chance; rarity scales crit
/// *damage* — the extra part of the multiplier (1.5x -> +0.5), not the 1x
/// baseline — and never touches chance.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Crit Chance Cig")]
public class CritChanceCigData : CigData
{
    [Tooltip("Crit chance per tier (0.1 = 10%).")]
    [SerializeField] private TierValues critChancePerTier = new TierValues(0.10f, 0.20f, 0.30f, 0.45f);
    [Tooltip("Crit damage multiplier before the rarity bonus (e.g. 1.5 = +50% damage on a crit).")]
    [SerializeField] private float baseCritDamageMultiplier = 1.5f;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        float chance = critChancePerTier.Get(instance.EffectiveTier);
        float extraMultiplier = (baseCritDamageMultiplier - 1f) * (1f + GetRarityBonus(instance));

        if (targetSlot == TargetSlot.SecondaryWeapon)
        {
            stats.SecondaryCritChance += chance;
            stats.SecondaryCritMultiplier += extraMultiplier; // baseline is already 1f
        }
        else
        {
            stats.PrimaryCritChance += chance;
            stats.PrimaryCritMultiplier += extraMultiplier;
        }
    }
}
