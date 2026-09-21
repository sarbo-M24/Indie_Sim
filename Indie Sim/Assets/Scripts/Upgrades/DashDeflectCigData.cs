using UnityEngine;

/// <summary>
/// Deflects projectiles the player dashes into — flat, no tiers/rarities,
/// no magnitude field at all, a pure behavior flag.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Dash Deflect Cig")]
public class DashDeflectCigData : CigData
{
    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        stats.DashDeflectEnabled = true;
    }
}
