using UnityEngine;

/// <summary>
/// Mild / Stomp: stomp hits harder over a bigger radius. Tiers scale damage
/// and radius; rarity scales damage only (radius is tier-only). Formerly
/// StompSeekCigData — renamed with its .meta, so existing assets still bind.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Stomp Power Cig")]
public class StompPowerCigData : CigData
{
    [Tooltip("Flat bonus stomp damage per tier, before the rarity bonus.")]
    [SerializeField] private TierValuesInt damagePerTier = new TierValuesInt(2, 5, 9, 14);
    [Tooltip("Bonus stomp radius per tier. Not affected by rarity.")]
    [SerializeField] private TierValues radiusPerTier = new TierValues(1f, 2f, 3f, 4f);

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = instance.EffectiveTier;
        float rarityBonus = GetRarityBonus(instance);
        int damage = damagePerTier.Get(tier);

        stats.StompBonusDamage += Mathf.RoundToInt(damage * (1f + rarityBonus));
        stats.StompBonusRadius += radiusPerTier.Get(tier);
    }
}
