using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves saved content IDs back to assets (save-system-spec.md §2, Content
/// catalogs). Lives at Resources/ContentCatalog so it's reachable from Boot,
/// where no scene-local catalog (e.g. CigPool's) exists.
///
/// Keep it in sync with Tools/Save/Rebuild Content Catalog; Tools/Save/Validate
/// Content IDs (also run before every build) flags empty/duplicate IDs and
/// assets missing from here.
/// </summary>
[CreateAssetMenu(fileName = "ContentCatalog", menuName = "Save/Content Catalog")]
public class ContentCatalog : ScriptableObject
{
    public const string ResourcePath = "ContentCatalog";

    [SerializeField] private CigData[] cigs = new CigData[0];
    [SerializeField] private WeaponData[] weapons = new WeaponData[0];

    public IReadOnlyList<CigData> Cigs => cigs;
    public IReadOnlyList<WeaponData> Weapons => weapons;

    private Dictionary<string, CigData> _cigsById;
    private Dictionary<string, WeaponData> _weaponsById;

    public static ContentCatalog Load()
    {
        ContentCatalog catalog = Resources.Load<ContentCatalog>(ResourcePath);
        if (catalog != null) return catalog;

        Debug.LogError($"[ContentCatalog] Resources/{ResourcePath}.asset is missing — saved upgrades and weapons can't be restored. Run Tools/Save/Rebuild Content Catalog.");
        return CreateInstance<ContentCatalog>();
    }

    public bool TryGetCig(string id, out CigData cig)
    {
        _cigsById ??= Index(cigs, c => c.id);
        return _cigsById.TryGetValue(id ?? "", out cig);
    }

    public bool TryGetWeapon(string id, out WeaponData weapon)
    {
        _weaponsById ??= Index(weapons, w => w.id);
        return _weaponsById.TryGetValue(id ?? "", out weapon);
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: replaces the contents (Rebuild Content Catalog).</summary>
    public void EditorSet(CigData[] newCigs, WeaponData[] newWeapons)
    {
        cigs = newCigs;
        weapons = newWeapons;
        _cigsById = null;
        _weaponsById = null;
    }
#endif

    private static Dictionary<string, T> Index<T>(T[] assets, System.Func<T, string> idOf) where T : Object
    {
        Dictionary<string, T> byId = new Dictionary<string, T>();
        foreach (T asset in assets)
        {
            if (asset == null || string.IsNullOrEmpty(idOf(asset))) continue;
            byId[idOf(asset)] = asset; // duplicates are the validator's job to flag
        }
        return byId;
    }
}

/// <summary>
/// Relics are prefab MonoBehaviours with an int index, not assets, so their
/// save IDs are derived: index 3 ⇄ "relic_3".
/// </summary>
public static class RelicIds
{
    public const int Count = 8;
    private const string Prefix = "relic_";

    public static string ToId(int index) => Prefix + index;

    public static bool TryParse(string id, out int index)
    {
        index = -1;
        return id != null
            && id.StartsWith(Prefix)
            && int.TryParse(id.Substring(Prefix.Length), out index)
            && index >= 0 && index < Count;
    }
}
