using UnityEngine;

/// <summary>
/// Mild / Dash: deal more damage for a fixed window after dashing (player
/// tints red while active). Tiers scale the damage %; no rarity.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Dash Post-Damage Cig")]
public class DashPostDamageCigData : CigData
{
    [Tooltip("Damage bonus during the post-dash window, per tier (0.25 = +25%).")]
    [SerializeField] private TierValues damageBonusPerTier = new TierValues(0.25f, 0.5f, 0.75f, 1f);
    [Tooltip("Fixed window duration in seconds — not tiered.")]
    [SerializeField] private float durationSeconds = 2f;

    public override void Apply(int tier, Rarity rarity) { }
    public override void ApplyMaxed(Rarity rarity) { }
    public override void Remove() { }

    public override void Contribute(CigInstance instance, ref PackStats stats)
    {
        stats.DashDamageWindowMultiplier += damageBonusPerTier.Get(instance.EffectiveTier); // baseline is already 1f
        stats.DashDamageWindowDuration = durationSeconds;
    }
}
