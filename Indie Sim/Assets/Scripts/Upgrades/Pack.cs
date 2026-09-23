using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-local singleton owning the up-to-5 active CigInstances and the
/// PackStats every consumer reads. Rehydrates from GameSession.CurrentRun on
/// Start() (Phase 6 pattern, mirrors CoinManager) so state survives
/// RoguelikeMode -> BossArena; every mutation writes back immediately.
/// </summary>
public class Pack : MonoBehaviour
{
    public const int MaxSlots = 5;

    public static Pack Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private CigData debugTestCig;

    private readonly List<CigInstance> _held = new List<CigInstance>();

    public IReadOnlyList<CigInstance> HeldCigs => _held;
    public PackStats Stats { get; private set; } = PackStats.Baseline;
    public bool IsFull => _held.Count >= MaxSlots;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _held.Clear();
        if (GameSession.Instance != null)
            _held.AddRange(GameSession.Instance.CurrentRun.HeldCigs);

        Recompute();
    }

    private void SyncToRunStats()
    {
        if (GameSession.Instance == null) return;
        GameSession.Instance.CurrentRun.HeldCigs = new List<CigInstance>(_held);
    }

    /// <summary>
    /// Finds the held instance of a given lineage. Per UpgradeSystemSpec.md,
    /// `CigData.id` alone is the lineage identity now (one asset = one
    /// lineage) — there is no separate lineageId field.
    /// </summary>
    public CigInstance FindByLineage(string cigId)
    {
        foreach (CigInstance instance in _held)
            if (instance.Data != null && instance.Data.id == cigId)
                return instance;
        return null;
    }

    public bool CanAdd(string cigId)
    {
        return FindByLineage(cigId) != null || !IsFull;
    }

    /// <summary>Mild and Regular are mutually exclusive per slot (for now); every other brand (Electric, etc.) is unrestricted.</summary>
    private static bool IsExclusiveBrand(Brand brand) => brand == Brand.Mild || brand == Brand.Regular;

    /// <summary>
    /// Returns the held instance that buying `newData` would conflict with —
    /// same targetSlot, a different lineage, and both an exclusive brand
    /// (Mild/Regular) — or null if there's no conflict. Buying a non-exclusive
    /// brand (Electric, etc.) never conflicts, regardless of what's held.
    /// </summary>
    public CigInstance GetBrandConflict(CigData newData)
    {
        if (newData == null || !IsExclusiveBrand(newData.brand)) return null;

        foreach (CigInstance held in _held)
        {
            if (held?.Data == null) continue;
            if (held.Data.id == newData.id) continue; // same lineage — replace-in-place, not a brand conflict
            if (held.Data.targetSlot != newData.targetSlot) continue;
            if (!IsExclusiveBrand(held.Data.brand)) continue;

            return held;
        }
        return null;
    }

    /// <summary>
    /// Spends coins, adds the offer to the pack (replacing a held instance of
    /// the same lineage in place, per the core loop), and marks it as
    /// permanently purchased in the pool. No side effects on failure. Fails
    /// if the offer has a same-slot brand conflict — use BuyWithReplace for that.
    /// </summary>
    public bool Buy(CigInstance offer)
    {
        if (offer == null || offer.Data == null) return false;
        if (!CanAdd(offer.Data.id)) return false;
        if (GetBrandConflict(offer.Data) != null) return false;
        if (CoinManager.Instance != null && !CoinManager.Instance.SpendCoins(offer.Data.cost)) return false;

        TryAdd(offer);
        CigPool.Instance?.MarkPurchased(offer.Data.id);
        return true;
    }

    /// <summary>
    /// Buys `offer` after removing `toReplace` to free its slot for the brand
    /// exclusivity rule (see GetBrandConflict). Checks affordability before
    /// removing anything, so a failed purchase never destructively drops the
    /// held cig. `toReplace` may be null, in which case this behaves like Buy().
    /// </summary>
    public bool BuyWithReplace(CigInstance offer, CigInstance toReplace)
    {
        if (offer == null || offer.Data == null) return false;
        if (CoinManager.Instance != null && !CoinManager.Instance.HasEnoughCoins(offer.Data.cost)) return false;

        if (toReplace != null) Remove(toReplace);
        if (!CanAdd(offer.Data.id)) return false;

        if (CoinManager.Instance != null) CoinManager.Instance.SpendCoins(offer.Data.cost);

        TryAdd(offer);
        CigPool.Instance?.MarkPurchased(offer.Data.id);
        return true;
    }

    /// <summary>
    /// Adds an instance directly, replacing any held instance of the same
    /// lineage in place. Returns false (no-op) if the pack is full and no
    /// same-lineage slot exists to replace. Does not touch coins or the pool
    /// — use Buy() for a real shop purchase.
    /// </summary>
    public bool TryAdd(CigInstance instance)
    {
        if (instance == null || instance.Data == null) return false;

        int existingIndex = _held.FindIndex(c => c.Data != null && c.Data.id == instance.Data.id);
        if (existingIndex != -1)
            _held[existingIndex] = instance;
        else
        {
            if (IsFull) return false;
            _held.Add(instance);
        }

        instance.Data.Apply(instance.RolledTier, instance.RolledRarity);
        NotifyMutated();
        return true;
    }

    /// <summary>Fully removes a held instance — the only discard path (burn resolution).</summary>
    public void Remove(CigInstance instance)
    {
        if (instance == null) return;
        if (_held.Remove(instance))
        {
            instance.Data.Remove();
            NotifyMutated();
        }
    }

    /// <summary>Call after mutating a held instance in place (e.g. burning it).</summary>
    public void NotifyMutated()
    {
        SyncToRunStats();
        Recompute();
    }

    /// <summary>Rebuilds Stats from scratch from every held instance's effective tier.</summary>
    public void Recompute()
    {
        PackStats stats = PackStats.Baseline;
        foreach (CigInstance instance in _held)
        {
            if (instance?.Data == null) continue;
            instance.Data.Contribute(instance, ref stats);
        }
        Stats = stats;
    }

    [ContextMenu("DEBUG - Buy Test Cig")]
    private void DEBUG_BuyTestCig()
    {
        if (debugTestCig == null)
        {
            Debug.LogWarning("[Pack] No debugTestCig assigned.");
            return;
        }

        CigInstance instance = new CigInstance
        {
            Data = debugTestCig,
            RolledTier = 1,
            RolledRarity = Rarity.Common
        };

        bool bought = Buy(instance);
        Debug.Log(bought
            ? $"[Pack] DEBUG bought {debugTestCig.displayName}. Held: {_held.Count}/{MaxSlots}"
            : "[Pack] DEBUG buy failed (full or unaffordable).");
    }

    [ContextMenu("DEBUG - Burn Test Cig")]
    private void DEBUG_BurnTestCig()
    {
        CigInstance instance = debugTestCig != null ? FindByLineage(debugTestCig.id) : null;
        if (instance == null)
        {
            Debug.LogWarning("[Pack] DEBUG burn — test cig not currently held.");
            return;
        }

        BurnResolver.Instance?.Burn(instance);
        Debug.Log($"[Pack] DEBUG burned {instance.Data.displayName} — now at tier {instance.EffectiveTier}.");
    }

    [ContextMenu("DEBUG - Print Pack")]
    private void DEBUG_PrintPack()
    {
        Debug.Log($"[Pack] {_held.Count}/{MaxSlots} held:");
        foreach (CigInstance c in _held)
        {
            Debug.Log($"  - {c.Data.displayName} | Tier {c.RolledTier} ({c.RolledRarity}) | Burning: {c.IsBurning} | EffectiveTier: {c.EffectiveTier}");
        }
    }
}
