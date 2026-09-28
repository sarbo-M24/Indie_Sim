using System;
using System.IO;
using UnityEngine;

/// <summary>
/// One-time move from the pre-slot save (persistent_stats.json, written by the
/// deleted SaveSystem) into profile.json, then the old file and the legacy
/// PlayerPrefs save keys are deleted. The PlayerPrefs values themselves are
/// not imported (spec 4B: no migration) — the old SaveSystem already folded
/// them into persistent_stats.json. Settings keys are left alone.
/// Safe to run every launch; delete this class before ship once no dev
/// machine still has a persistent_stats.json.
/// </summary>
public static class LegacySaveImport
{
    private const string LegacyFileName = "persistent_stats.json";
    private static readonly string[] LegacyPlayerPrefsKeys = { "TotalCoinsEverCollected", "TotalEnemiesKilled", "UnlockedAchievements" };

    /// <summary>Loads the profile, importing the legacy file first if profile.json doesn't exist yet.</summary>
    public static void LoadProfile(SaveService saves, PersistentStats persistent)
    {
        string legacyPath = Path.Combine(Application.persistentDataPath, LegacyFileName);

        if (File.Exists(legacyPath) && !File.Exists(saves.ProfilePath) && !File.Exists(saves.ProfilePath + ".bak"))
            Import(saves, persistent, legacyPath);
        else
            saves.LoadProfile();

        // Only once profile.json is safely on disk.
        if (File.Exists(legacyPath) && File.Exists(saves.ProfilePath))
            TryDeleteLegacyFile(legacyPath);

        DeleteLegacyPlayerPrefs();
    }

    private static void Import(SaveService saves, PersistentStats persistent, string legacyPath)
    {
        try
        {
            PersistentStats legacy = JsonUtility.FromJson<PersistentStats>(File.ReadAllText(legacyPath));
            if (legacy == null) throw new FormatException("empty file");

            persistent.TotalCoinsEverCollected = legacy.TotalCoinsEverCollected;
            persistent.TotalEnemiesKilled = legacy.TotalEnemiesKilled;
            persistent.UnlockedAchievementIds = legacy.UnlockedAchievementIds ?? new System.Collections.Generic.List<string>();
            persistent.TotalRuns = legacy.TotalRuns;
            persistent.BestRunDungeonsCleared = legacy.BestRunDungeonsCleared;
            persistent.DemoCompleted = legacy.DemoCompleted;
        }
        catch (Exception e)
        {
            // Leave the legacy file in place (nothing is written) so it can be inspected.
            Debug.LogError($"[LegacySaveImport] {LegacyFileName} is unreadable, starting a fresh profile: {e.Message}");
            return;
        }

        if (saves.WriteProfile())
            Debug.Log($"[LegacySaveImport] Imported {LegacyFileName} into profile.json.");
    }

    private static void TryDeleteLegacyFile(string legacyPath)
    {
        try
        {
            File.Delete(legacyPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LegacySaveImport] Couldn't delete {LegacyFileName}: {e.Message}");
        }
    }

    private static void DeleteLegacyPlayerPrefs()
    {
        bool deleted = false;
        foreach (string key in LegacyPlayerPrefsKeys)
        {
            if (!PlayerPrefs.HasKey(key)) continue;
            PlayerPrefs.DeleteKey(key);
            deleted = true;
        }
        if (deleted) PlayerPrefs.Save();
    }
}
