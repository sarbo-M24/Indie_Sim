using System;

/// <summary>
/// Minimal level-boundary signal for BurnResolver, per UpgradeSystemSpec's
/// sequencing note: don't wait on the architecture refactor's Phase 7 event
/// bus. Raised from RoguelikeManager.CompleteDungeon(), before the boss
/// branch, so it fires on both the shop path and the boss path.
/// </summary>
public static class LevelBoundary
{
    public static event Action OnLevelEnded;

    public static void RaiseLevelEnded()
    {
        OnLevelEnded?.Invoke();
    }
}
