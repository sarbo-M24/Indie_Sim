using UnityEngine;

/// <summary>
/// Regular / Stomp: more stomp damage + stomp releases a circle of bullets.
/// Tiers scale flat damage and bullet count; rarity scales damage only.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Stomp Circle Cig")]
public class StompCircleCigData : CigData
{
    [Tooltip("Flat bonus stomp damage per tier, before the rarity bonus.")]
    [SerializeField] private TierValuesInt damagePerTier = new TierValuesInt(2, 5, 9, 14);
    [Tooltip("Bullets fired in the ring per tier. Not affected by rarity.")]
    [SerializeField] private TierValuesInt bulletCountPerTier = new TierValuesInt(4, 6, 8, 10);

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = instance.EffectiveTier;
        int damage = damagePerTier.Get(tier);

        stats.StompBonusDamage += Mathf.RoundToInt(damage * (1f + GetRarityBonus(instance)));
        stats.StompBulletCount += bulletCountPerTier.Get(tier);
    }
}
