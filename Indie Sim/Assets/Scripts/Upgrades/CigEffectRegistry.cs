using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// No-op placeholder effect. Real effects are registered here one at a time
/// as the Step 4 catalog is built.
/// </summary>
public class NoOpEffect : IUpgradeEffect
{
    public void Apply() { }
    public void ApplyMaxed() { }
    public void Remove() { }
    public void Contribute(CigInstance instance, ref PackStats stats) { }
}

public static class CigEffectRegistry
{
    private static readonly Dictionary<CigEffectId, IUpgradeEffect> Effects = new Dictionary<CigEffectId, IUpgradeEffect>
    {
        { CigEffectId.None, new NoOpEffect() },
    };

    public static IUpgradeEffect Get(CigEffectId id)
    {
        if (Effects.TryGetValue(id, out IUpgradeEffect effect))
            return effect;

        Debug.LogWarning($"[CigEffectRegistry] No effect registered for {id} yet — falling back to no-op.");
        return Effects[CigEffectId.None];
    }
}
