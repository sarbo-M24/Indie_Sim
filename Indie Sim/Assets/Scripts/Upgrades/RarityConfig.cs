using UnityEngine;

/// <summary>
/// The shared rarity -> bonus % lookup every CigData subclass's Contribute()
/// reads, per UpgradeSystemSpec.md — a single asset made once (not a static
/// C# table), so every upgrade's Inspector points at the same source of
/// truth instead of duplicating the four values per cig. Wire the same
/// RarityConfig asset into every CigData subclass instance's `rarityConfig`
/// field. Placeholder numbers — balancing is not part of this pass.
/// </summary>
[CreateAssetMenu(menuName = "Upgrades/Rarity Config")]
public class RarityConfig : ScriptableObject
{
    [SerializeField] private float common = 0f;
    [SerializeField] private float uncommon = 0.10f;
    [SerializeField] private float rare = 0.25f;
    [SerializeField] private float epic = 0.50f;

    public float GetBonus(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return common;
            case Rarity.Uncommon: return uncommon;
            case Rarity.Rare: return rare;
            case Rarity.Epic: return epic;
            default: return 0f;
        }
    }
}
