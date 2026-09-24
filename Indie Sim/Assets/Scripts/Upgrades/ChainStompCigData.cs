using UnityEngine;

/// <summary>
/// Electric / Stomp: chain stomp — more stomps per activation, paid for with
/// a longer cooldown. Tiers scale both; no rarity.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Chain Stomp Cig")]
public class ChainStompCigData : CigData
{
    [Tooltip("Extra stomp charges per tier.")]
    [SerializeField] private TierValuesInt extraChargesPerTier = new TierValuesInt(1, 2, 3, 4);
    [Tooltip("Seconds added to the stomp cooldown, per tier.")]
    [SerializeField] private TierValues cooldownPenaltyPerTier = new TierValues(0.5f, 1f, 1.5f, 2f);

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = instance.EffectiveTier;
        stats.StompExtraCharges += extraChargesPerTier.Get(tier);
        stats.StompCooldownPenalty += cooldownPenaltyPerTier.Get(tier);
    }
}
