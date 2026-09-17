using UnityEngine;

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

    /// <summary>
    /// Seconds left on the burn countdown, only ticking while gameplay is
    /// active (not while the shop is open). Set to Data.burnDurationSeconds
    /// the moment Burn() is called; reaching 0 removes the instance.
    /// </summary>
    public float BurnTimeRemaining;

    /// <summary>Tier used for effect resolution — maxed (4) while burning.</summary>
    public int EffectiveTier => IsBurning ? 4 : RolledTier;

    /// <summary>0-1 fraction remaining, for a HUD fill bar. 0 when not burning.</summary>
    public float BurnFraction => (IsBurning && Data != null && Data.burnDurationSeconds > 0f)
        ? Mathf.Clamp01(BurnTimeRemaining / Data.burnDurationSeconds)
        : 0f;
}
