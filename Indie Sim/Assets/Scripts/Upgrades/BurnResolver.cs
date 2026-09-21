using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the burn action, per UpgradeSystemSpec.md: flips a held instance
/// to burning (maxed Tier, Rarity unchanged) for the next level only, then
/// fully removes it the moment that level ends — not a revert to its
/// pre-burn state, just gone. Burning is not time-based; there's no
/// countdown, only "resolves once, then removed at the next level
/// boundary." Static event + scene-local subscriber is the exact leak
/// pattern architecture-refactor-plan-v3.md warns about — must unsubscribe
/// in OnDisable.
/// </summary>
public class BurnResolver : MonoBehaviour
{
    public static BurnResolver Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        LevelBoundary.OnLevelEnded += HandleLevelEnded;
    }

    private void OnDisable()
    {
        LevelBoundary.OnLevelEnded -= HandleLevelEnded;
    }

    /// <summary>
    /// Burns a held cig: its effect resolves at max tier (Rarity unchanged)
    /// for the next level only, resolved by HandleLevelEnded() below — no
    /// timer, no carryover past that level.
    /// </summary>
    public void Burn(CigInstance instance)
    {
        if (instance == null || instance.IsBurning) return;

        instance.IsBurning = true;
        instance.Data.ApplyMaxed(instance.RolledRarity);

        Pack.Instance?.NotifyMutated();
    }

    private void HandleLevelEnded()
    {
        if (Pack.Instance == null) return;

        // Copy first — Pack.Remove() mutates the live list we'd be iterating.
        List<CigInstance> burning = new List<CigInstance>();
        foreach (CigInstance instance in Pack.Instance.HeldCigs)
            if (instance.IsBurning)
                burning.Add(instance);

        foreach (CigInstance instance in burning)
            Pack.Instance.Remove(instance);

        if (burning.Count > 0)
            Debug.Log($"[BurnResolver] Level ended — removed {burning.Count} burned cig(s) (no carryover).");
    }
}
