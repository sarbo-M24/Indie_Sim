using UnityEngine;

/// <summary>
/// Regular / Primary or Secondary: flat weapon damage + bullets bounce off
/// enemies. Created twice (one asset per weapon `targetSlot`). Tiers scale
/// both flat damage and bounce count; rarity scales damage only.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Bullet Bounce Cig")]
public class BulletBounceCigData : CigData
{
    [Tooltip("Flat weapon damage bonus per tier, before the rarity bonus.")]
    [SerializeField] private TierValuesInt damagePerTier = new TierValuesInt(5, 10, 15, 20);
    [Tooltip("Enemies a bullet bounces to after its first hit, per tier. Not affected by rarity.")]
    [SerializeField] private TierValuesInt bouncesPerTier = new TierValuesInt(1, 2, 3, 4);

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        int tier = instance.EffectiveTier;
        int bonusDamage = Mathf.RoundToInt(damagePerTier.Get(tier) * (1f + GetRarityBonus(instance)));
        int bounces = bouncesPerTier.Get(tier);

        if (targetSlot == TargetSlot.SecondaryWeapon)
        {
            stats.SecondaryBounceCount += bounces;
            stats.SecondaryWeaponBonusDamage += bonusDamage;
        }
        else
        {
            stats.PrimaryBounceCount += bounces;
            stats.PrimaryWeaponBonusDamage += bonusDamage;
        }
    }
}
