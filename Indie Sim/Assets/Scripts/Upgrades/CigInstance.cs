/// <summary>
/// A purchased, held cig: a CigData reference plus the tier/rarity rolled
/// for this specific purchase, and whether it's currently burning.
/// </summary>
[System.Serializable]
public class CigInstance
{
    public CigData Data;
    public int RolledTier;
    public Rarity RolledRarity;
    public bool IsBurning;

    /// <summary>Tier used for effect resolution — maxed (4) while burning.</summary>
    public int EffectiveTier => IsBurning ? 4 : RolledTier;
}
