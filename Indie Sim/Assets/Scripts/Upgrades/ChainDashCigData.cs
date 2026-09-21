using UnityEngine;

/// <summary>
/// Chain dash — more dashes available per activation — flat, no
/// tiers/rarities.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Chain Dash Cig")]
public class ChainDashCigData : CigData
{
    [Tooltip("Flat extra dash charges granted — this upgrade has no tiers/rarities.")]
    [SerializeField] private int extraCharges = 1;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        stats.DashExtraCharges += extraCharges;
    }
}
