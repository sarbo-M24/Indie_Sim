using UnityEngine;

/// <summary>
/// Weapon upgrade for a single slot: % fire-rate increase, tiers + rarity.
/// Two separate asset instances (Primary/Secondary) per `targetSlot`, same as CritChanceCigData.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Fire Rate Cig")]
public class FireRateCigData : CigData
{
    [Tooltip("% fire-rate increase granted at each tier. Index 0 unused (tiers are 1-4).")]
    [SerializeField] private float[] fireRateBonusPerTier = { 0f, 0.10f, 0.20f, 0.30f, 0.45f };
    [SerializeField] private RarityConfig rarityConfig;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = Mathf.Clamp(instance.EffectiveTier, 0, fireRateBonusPerTier.Length - 1);
        float rarityBonus = rarityConfig != null ? rarityConfig.GetBonus(instance.RolledRarity) : 0f;

        float tierBonus = fireRateBonusPerTier[tier];
        float finalBonus = tierBonus + rarityBonus * tierBonus;

        if (targetSlot == TargetSlot.SecondaryWeapon)
            stats.SecondaryFireRateBonus += finalBonus;
        else
            stats.PrimaryFireRateBonus += finalBonus;
    }
}
