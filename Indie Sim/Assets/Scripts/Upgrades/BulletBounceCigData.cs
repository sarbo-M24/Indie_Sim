using UnityEngine;

/// <summary>
/// Bullets bounce off enemies for a weapon slot — flat, no tiers/rarities.
/// Created as two separate asset instances (one for PrimaryWeapon, one for
/// SecondaryWeapon per `targetSlot`).
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Bullet Bounce Cig")]
public class BulletBounceCigData : CigData
{
    [Tooltip("Flat bounce count granted — this upgrade has no tiers/rarities.")]
    [SerializeField] private int bounceCount = 1;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        if (targetSlot == TargetSlot.SecondaryWeapon)
            stats.SecondaryBounceCount += bounceCount;
        else
            stats.PrimaryBounceCount += bounceCount;
    }
}
