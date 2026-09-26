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

    [Header("Card border colours")]
    [SerializeField] private Color commonColor = Color.white;
    [SerializeField] private Color uncommonColor = Color.green;
    [SerializeField] private Color rareColor = Color.blue;
    [SerializeField] private Color epicColor = new Color(0.6f, 0.2f, 0.9f);

    public Color GetColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return commonColor;
            case Rarity.Uncommon: return uncommonColor;
            case Rarity.Rare: return rareColor;
            case Rarity.Epic: return epicColor;
            default: return commonColor;
        }
    }

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
