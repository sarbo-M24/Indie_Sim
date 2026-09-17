using UnityEngine;

/// <summary>
/// Shared per-tier/per-rarity damage values and the one damage formula every
/// damage-based effect resolves through. Placeholder numbers — balancing is
/// not part of this pass.
/// </summary>
public static class TierRarityTable
{
    // Index = tier (1-4). Index 0 unused so tier can index directly.
    private static readonly int[] UpgradedDamageByTier = { 0, 2, 5, 9, 14 };

    // Index = (int)Rarity.
    private static readonly float[] RarityBonusByRarity = { 0f, 0.10f, 0.25f, 0.50f };

    public static int GetUpgradedDamage(int tier)
    {
        tier = Mathf.Clamp(tier, 0, UpgradedDamageByTier.Length - 1);
        return UpgradedDamageByTier[tier];
    }

    public static float GetRarityBonus(Rarity rarity)
    {
        return RarityBonusByRarity[(int)rarity];
    }

    /// <summary>FinalDamage = BaseDamage + UpgradedDamage + (RarityBonus x UpgradedDamage)</summary>
    public static int ResolveDamage(int baseDamage, int tier, Rarity rarity)
    {
        int upgradedDamage = GetUpgradedDamage(tier);
        float rarityBonus = GetRarityBonus(rarity);
        return Mathf.RoundToInt(baseDamage + upgradedDamage + (rarityBonus * upgradedDamage));
    }
}
