using UnityEngine;

/// <summary>
/// Weapon upgrade for a single slot: crit chance only. Created twice as
/// separate assets (one for PrimaryWeapon, one for SecondaryWeapon per
/// `targetSlot`), each independently tunable. Tiers scale crit chance;
/// rarity does not touch chance at all — it scales crit damage instead
/// (baseCritDamageMultiplier + rarityBonus). Flat weapon damage used to live
/// here too but moved to BulletBounceCigData, which now covers "flat damage
/// + bounce" for the same slot.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Crit Chance Cig")]
public class CritChanceCigData : CigData
{
    [Tooltip("Crit chance granted at each tier. Index 0 unused (tiers are 1-4).")]
    [SerializeField] private float[] critChancePerTier = { 0f, 0.10f, 0.20f, 0.30f, 0.45f };
    [Tooltip("Crit damage multiplier before the rarity bonus (e.g. 1.5 = +50% damage on a crit).")]
    [SerializeField] private float baseCritDamageMultiplier = 1.5f;
    [SerializeField] private RarityConfig rarityConfig;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = Mathf.Clamp(instance.EffectiveTier, 0, critChancePerTier.Length - 1);
        float chance = critChancePerTier[tier];

        float rarityBonus = rarityConfig != null ? rarityConfig.GetBonus(instance.RolledRarity) : 0f;
        float extraMultiplier = baseCritDamageMultiplier - 1f;
        float finalExtraMultiplier = extraMultiplier + rarityBonus * extraMultiplier;

        if (targetSlot == TargetSlot.SecondaryWeapon)
        {
            stats.SecondaryCritChance += chance;
            stats.SecondaryCritMultiplier += finalExtraMultiplier; // baseline is already 1f
        }
        else
        {
            stats.PrimaryCritChance += chance;
            stats.PrimaryCritMultiplier += finalExtraMultiplier;
        }
    }
}
