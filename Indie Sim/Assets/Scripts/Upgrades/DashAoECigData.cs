using UnityEngine;

/// <summary>
/// Damage in an area after the dash completes — flat, no tiers/rarities.
/// Radius is never authored here: PlayerController derives it at runtime
/// from the dash's own travel distance (distance = the circle's diameter).
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Dash AoE Cig")]
public class DashAoECigData : CigData
{
    [SerializeField] private int damage = 12;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        stats.DashAoeDamage += damage;
    }
}
