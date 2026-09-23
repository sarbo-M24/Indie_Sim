using UnityEngine;

/// <summary>
/// Secondary weapon (shotgun) only: increases pellets fired per shot.
/// Has tiers, no rarity — the asset should have hasTier = true, hasRarity =
/// false, so the shop rolls/shows a Tier but never a Rarity for this cig.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Shotgun Pellet Count Cig")]
public class ShotgunPelletCountCigData : CigData
{
    [Tooltip("Extra pellets granted at each tier. Index 0 unused (tiers are 1-4).")]
    [SerializeField] private int[] extraPelletsPerTier = { 0, 1, 2, 3, 4 };

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = Mathf.Clamp(instance.EffectiveTier, 0, extraPelletsPerTier.Length - 1);
        stats.ShotgunBonusPellets += extraPelletsPerTier[tier];
    }
}
