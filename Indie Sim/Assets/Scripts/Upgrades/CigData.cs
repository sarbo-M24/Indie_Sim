using UnityEngine;

/// <summary>
/// One catalog lineage (e.g. "Primary weapon crit"), per UpgradeSystemSpec.md's
/// data model. Abstract base holding only shared identity/classification
/// fields — no magnitude numbers live here. Each concrete subclass (see
/// CritChanceCigData.cs, StompPowerCigData.cs, etc.) implements IUpgradeEffect
/// directly and adds only the magnitude field(s) it actually needs, so the
/// asset you create in the Project window *is* the effect: Sarbo fills in
/// identity and tier-value fields together in one Inspector, nothing to
/// cross-reference. `id` alone is the lineage identity (one asset = one
/// lineage) — Pack keys replace-in-place off `id`.
///
/// Apply/ApplyMaxed/Remove/Contribute are pure stat contributors on every
/// subclass (see IUpgradeEffect.cs) and must never write to `this` — this is
/// a shared ScriptableObject ASSET referenced by every CigInstance of this
/// lineage, and a runtime write to an asset persists across Editor play
/// sessions (same invariant this codebase already enforces for WeaponData).
/// All per-instance runtime state (tier, rarity, burning) lives on
/// CigInstance instead.
/// </summary>
public abstract class CigData : ScriptableObject, IUpgradeEffect
{
    public string id;
    public string displayName;

    [TextArea]
    public string description;

    [Tooltip("The cig's own art, shown on its shop card.")]
    public Sprite icon;

    public Brand brand;
    public TargetSlot targetSlot;

    // Every upgrade has 4 tiers (per the upgrade sheet), so there's no hasTier
    // switch — CigPool always rolls 1-4. Rarity is the only optional axis.
    [Tooltip("False if this upgrade never rolls/uses a rarity bonus.")]
    public bool hasRarity;

    [Tooltip("Shared rarity -> bonus % lookup. Only read when hasRarity is true.")]
    [SerializeField] protected RarityConfig rarityConfig;

    [Tooltip("Coin cost to buy this cig from the shop.")]
    public int cost;

    /// <summary>The rolled rarity's bonus %, or 0 for a rarity-less upgrade or a missing config.</summary>
    protected float GetRarityBonus(CigInstance instance)
    {
        if (!hasRarity || rarityConfig == null) return 0f;
        return rarityConfig.GetBonus(instance.RolledRarity);
    }

    public abstract void Apply(int tier, Rarity rarity);
    public abstract void ApplyMaxed(Rarity rarity);
    public abstract void Remove();
    public abstract void Contribute(CigInstance instance, ref PackStats stats);
}
