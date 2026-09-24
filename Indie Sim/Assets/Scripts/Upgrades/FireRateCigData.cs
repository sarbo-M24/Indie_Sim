using UnityEngine;

/// <summary>
/// Electric / Primary: % fire-rate increase. Tiers only, no rarity. Still
/// honours `targetSlot`, but the demo catalog only ships the Primary asset.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Fire Rate Cig")]
public class FireRateCigData : CigData
{
    [Tooltip("Fire-rate increase per tier (0.1 = +10%).")]
    [SerializeField] private TierValues fireRateBonusPerTier = new TierValues(0.10f, 0.20f, 0.30f, 0.45f);

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        float bonus = fireRateBonusPerTier.Get(instance.EffectiveTier);

        if (targetSlot == TargetSlot.SecondaryWeapon)
            stats.SecondaryFireRateBonus += bonus;
        else
            stats.PrimaryFireRateBonus += bonus;
    }
}
