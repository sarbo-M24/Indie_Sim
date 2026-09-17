using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the burn action: flips a held instance to burning (maxed) and
/// removes it the instant either its own burn timer expires or the level
/// ends — whichever comes first, with no carryover either way. Static event
/// + scene-local subscriber is the exact leak pattern
/// architecture-refactor-plan-v3.md warns about — must unsubscribe in
/// OnDisable.
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
    /// Burns a held cig: its effect resolves at max tier until either its
    /// burn timer runs out or the level ends, whichever comes first — no
    /// carryover either way. The timer is set here but only ticks once
    /// gameplay actually resumes (see Update()).
    /// </summary>
    public void Burn(CigInstance instance)
    {
        if (instance == null || instance.IsBurning) return;

        instance.IsBurning = true;
        instance.BurnTimeRemaining = instance.Data.burnDurationSeconds;
        CigEffectRegistry.Get(instance.Data.effectId).ApplyMaxed();

        Pack.Instance?.NotifyMutated();
    }

    private void Update()
    {
        if (Pack.Instance == null) return;

        // Timer only runs while the player actually has control — not while
        // the shop is open (Time.deltaTime keeps flowing there since the
        // shop pauses via input-disable, not timeScale).
        if (RoguelikeManager.Instance != null && !RoguelikeManager.Instance.GameplayInputEnabled)
            return;

        List<CigInstance> expired = null;
        foreach (CigInstance instance in Pack.Instance.HeldCigs)
        {
            if (!instance.IsBurning) continue;

            instance.BurnTimeRemaining -= Time.deltaTime;
            if (instance.BurnTimeRemaining <= 0f)
            {
                expired ??= new List<CigInstance>();
                expired.Add(instance);
            }
        }

        if (expired == null) return;

        foreach (CigInstance instance in expired)
            Pack.Instance.Remove(instance); // Remove() already recomputes/syncs

        Debug.Log($"[BurnResolver] Burn timer expired — removed {expired.Count} cig(s).");
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
