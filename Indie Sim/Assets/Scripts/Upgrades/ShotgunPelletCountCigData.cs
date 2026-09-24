using UnityEngine;

/// <summary>
/// Electric / Secondary (shotgun only): extra pellets per shot. Tiers only,
/// no rarity.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Shotgun Pellet Count Cig")]
public class ShotgunPelletCountCigData : CigData
{
    [Tooltip("Extra pellets per shot, per tier.")]
    [SerializeField] private TierValuesInt extraPelletsPerTier = new TierValuesInt(1, 2, 3, 4);

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        stats.ShotgunBonusPellets += extraPelletsPerTier.Get(instance.EffectiveTier);
    }
}
