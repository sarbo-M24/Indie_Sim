using UnityEngine;

/// <summary>
/// One catalog lineage (e.g. "Primary weapon crit"). Tier and rarity are not
/// stored here — they're rolled per-purchase onto a CigInstance. effectId is
/// resolved through CigEffectRegistry rather than referencing IUpgradeEffect
/// directly, so this asset never needs [SerializeReference] wiring.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Cig Data")]
public class CigData : ScriptableObject
{
    public string id;
    public string displayName;
    public string lineageId;
    public Brand brand;
    public TargetSlot targetSlot;

    [Tooltip("False for flat, one-off upgrades with no tier/rarity roll.")]
    public bool hasTierRarity;

    [Tooltip("Coin cost to buy this cig from the shop.")]
    public int cost;

    [Tooltip("Seconds a burned instance's maxed effect lasts once gameplay resumes, before it's removed. Placeholder value — balancing is not part of this pass.")]
    public float burnDurationSeconds = 20f;

    public CigEffectId effectId;
}
