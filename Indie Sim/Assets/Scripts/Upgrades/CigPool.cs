using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the catalog and the Buy tab's offer generation. A bought lineage is
/// removed from the pool permanently for the run (per spec — it never
/// reappears, even after being burned). Rolls a fresh tier/rarity onto a new
/// CigInstance for every still-available lineage each time offers are asked
/// for, so the whole not-yet-purchased catalog is always the offer set.
/// </summary>
public class CigPool : MonoBehaviour
{
    public static CigPool Instance { get; private set; }

    [Tooltip("Every shipped CigData asset — the full catalog.")]
    [SerializeField] private CigData[] catalog;

    private readonly List<string> _purchasedIds = new List<string>();

    /// <summary>Read-only view of the full catalog, for debug tooling.</summary>
    public IReadOnlyList<CigData> Catalog => catalog;

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
        _purchasedIds.Clear();
        if (GameSession.Instance != null)
            _purchasedIds.AddRange(GameSession.Instance.CurrentRun.PurchasedCigIds);
    }

    private void SyncToRunStats()
    {
        if (GameSession.Instance == null) return;
        GameSession.Instance.CurrentRun.PurchasedCigIds = new List<string>(_purchasedIds);
    }

    public bool IsPurchased(string cigId) => _purchasedIds.Contains(cigId);

    /// <summary>Builds the current Buy-tab offer set: every not-yet-purchased lineage, freshly rolled.</summary>
    public List<CigInstance> RollOffers()
    {
        List<CigInstance> offers = new List<CigInstance>();
        foreach (CigData data in catalog)
        {
            if (data == null || _purchasedIds.Contains(data.id)) continue;
            offers.Add(RollInstance(data));
        }
        return Shuffle(offers);
    }

    private CigInstance RollInstance(CigData data)
    {
        CigInstance instance = new CigInstance { Data = data };
        if (data.hasTierRarity)
        {
            instance.RolledTier = Random.Range(1, 5); // 1-4 inclusive
            instance.RolledRarity = (Rarity)Random.Range(0, System.Enum.GetValues(typeof(Rarity)).Length);
        }
        else
        {
            instance.RolledTier = 1;
            instance.RolledRarity = Rarity.Common;
        }
        return instance;
    }

    /// <summary>Marks a lineage bought — permanently removed from the pool for this run.</summary>
    public void MarkPurchased(string cigId)
    {
        if (_purchasedIds.Contains(cigId)) return;
        _purchasedIds.Add(cigId);
        SyncToRunStats();
    }

    // Fisher-Yates. Lifted from the old StoreManager (deleted in Step 0) —
    // the one piece of that class worth keeping.
    private static List<T> Shuffle<T>(List<T> input)
    {
        List<T> list = new List<T>(input);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    [ContextMenu("DEBUG - Print Offers")]
    private void DEBUG_PrintOffers()
    {
        List<CigInstance> offers = RollOffers();
        Debug.Log($"[CigPool] {offers.Count} offer(s):");
        foreach (CigInstance offer in offers)
            Debug.Log($"  - {offer.Data.displayName} | Tier {offer.RolledTier} ({offer.RolledRarity}) | Cost {offer.Data.cost}");
    }
}
