using UnityEngine;

/// <summary>
/// One catalog lineage (e.g. "Primary weapon crit"), per UpgradeSystemSpec.md's
/// data model. Abstract base holding only shared identity/classification
/// fields — no magnitude numbers live here. Each concrete subclass (see
/// CritChanceCigData.cs, StompSeekCigData.cs, etc.) implements IUpgradeEffect
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

    [Tooltip("False for a flat, always-tier-1 upgrade with no tier roll.")]
    public bool hasTier;

    [Tooltip("False if this upgrade never rolls/uses a rarity bonus, regardless of hasTier.")]
    public bool hasRarity;

    [Tooltip("Coin cost to buy this cig from the shop.")]
    public int cost;

    public abstract void Apply(int tier, Rarity rarity);
    public abstract void ApplyMaxed(Rarity rarity);
    public abstract void Remove();
    public abstract void Contribute(CigInstance instance, ref PackStats stats);
}
