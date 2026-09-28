using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Save IDs must be unique and non-empty or saved content restores as the
/// wrong thing (or not at all). Checks every CigData and WeaponData asset,
/// and that each is in Resources/ContentCatalog. Runs from the menu and
/// before every build — empty/duplicate IDs fail the build.
/// </summary>
public class ContentIdValidator : IPreprocessBuildWithReport
{
    private const string CatalogAssetPath = "Assets/Resources/ContentCatalog.asset";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Validate(out List<string> errors, out List<string> warnings);
        foreach (string warning in warnings) Debug.LogWarning($"[ContentIdValidator] {warning}");
        if (errors.Count > 0)
            throw new BuildFailedException("Content ID validation failed:\n" + string.Join("\n", errors));
    }

    [MenuItem("Tools/Save/Validate Content IDs", priority = 80)]
    private static void ValidateMenu()
    {
        Validate(out List<string> errors, out List<string> warnings);
        foreach (string error in errors) Debug.LogError($"[ContentIdValidator] {error}");
        foreach (string warning in warnings) Debug.LogWarning($"[ContentIdValidator] {warning}");
        if (errors.Count == 0 && warnings.Count == 0)
            Debug.Log("[ContentIdValidator] All content IDs are valid and catalogued.");
    }

    [MenuItem("Tools/Save/Rebuild Content Catalog", priority = 81)]
    private static void RebuildCatalog()
    {
        ContentCatalog catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ContentCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        CigData[] cigs = FindAll<CigData>().OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
        WeaponData[] weapons = FindAll<WeaponData>().OrderBy(w => w.id, StringComparer.Ordinal).ToArray();
        catalog.EditorSet(cigs, weapons);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ContentIdValidator] Catalog rebuilt: {cigs.Length} upgrades, {weapons.Length} weapons.");
        ValidateMenu();
    }

    public static void Validate(out List<string> errors, out List<string> warnings)
    {
        errors = new List<string>();
        warnings = new List<string>();

        List<CigData> cigs = FindAll<CigData>();
        List<WeaponData> weapons = FindAll<WeaponData>();
        CheckIds(cigs, c => c.id, "Upgrade", errors);
        CheckIds(weapons, w => w.id, "Weapon", errors);

        ContentCatalog catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            errors.Add($"{CatalogAssetPath} is missing — run Tools/Save/Rebuild Content Catalog.");
            return;
        }

        CheckCatalogued(cigs, catalog.Cigs, "Upgrade", warnings);
        CheckCatalogued(weapons, catalog.Weapons, "Weapon", warnings);
    }

    private static void CheckIds<T>(List<T> assets, Func<T, string> idOf, string kind, List<string> errors) where T : UnityEngine.Object
    {
        Dictionary<string, T> seen = new Dictionary<string, T>();
        foreach (T asset in assets)
        {
            string id = idOf(asset);
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"{kind} '{AssetDatabase.GetAssetPath(asset)}' has an empty id.");
                continue;
            }
            if (seen.TryGetValue(id, out T first))
                errors.Add($"{kind} id '{id}' is used by both '{AssetDatabase.GetAssetPath(first)}' and '{AssetDatabase.GetAssetPath(asset)}'.");
            else
                seen.Add(id, asset);
        }
    }

    private static void CheckCatalogued<T>(List<T> assets, IReadOnlyList<T> catalogued, string kind, List<string> warnings) where T : UnityEngine.Object
    {
        HashSet<T> inCatalog = new HashSet<T>(catalogued.Where(a => a != null));
        if (inCatalog.Count != catalogued.Count)
            warnings.Add($"ContentCatalog has empty {kind} entries.");

        foreach (T asset in assets)
            if (!inCatalog.Contains(asset))
                warnings.Add($"{kind} '{AssetDatabase.GetAssetPath(asset)}' isn't in ContentCatalog, so it can't be restored from a save. Run Tools/Save/Rebuild Content Catalog if it ships.");
    }

    private static List<T> FindAll<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null)
            .ToList();
}
