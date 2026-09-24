using UnityEngine;

/// <summary>
/// Electric / Dash: chain dash — more dashes per activation, paid for with a
/// longer cooldown. Tiers scale both; no rarity.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Chain Dash Cig")]
public class ChainDashCigData : CigData
{
    [Tooltip("Extra dash charges per tier.")]
    [SerializeField] private TierValuesInt extraChargesPerTier = new TierValuesInt(1, 2, 3, 4);
    [Tooltip("Seconds added to the dash cooldown, per tier.")]
    [SerializeField] private TierValues cooldownPenaltyPerTier = new TierValues(0.25f, 0.5f, 0.75f, 1f);

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = instance.EffectiveTier;
        stats.DashExtraCharges += extraChargesPerTier.Get(tier);
        stats.DashCooldownPenalty += cooldownPenaltyPerTier.Get(tier);
    }
}
