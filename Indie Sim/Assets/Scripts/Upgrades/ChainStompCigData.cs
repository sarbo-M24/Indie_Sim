using UnityEngine;

/// <summary>Chain stomp — more stomp charges available — flat, no tiers/rarities.</summary>
[CreateAssetMenu(menuName = "Upgrades/Chain Stomp Cig")]
public class ChainStompCigData : CigData
{
    [Tooltip("Flat extra stomp charges granted — this upgrade has no tiers/rarities.")]
    [SerializeField] private int extraCharges = 1;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        stats.StompExtraCharges += extraCharges;
    }
}
