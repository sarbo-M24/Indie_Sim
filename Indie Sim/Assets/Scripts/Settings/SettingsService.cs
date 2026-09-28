using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Owns the player's GameSettings: loads settings.json (separate from the save
/// file) on first access, applies changes live via OnChanged, and writes to
/// disk only when asked (Save — call it when a settings panel closes, not per
/// slider tick) or when the app quits.
///
/// Static, like InputManager, so it needs no object in Boot.unity and is
/// usable from any Awake. Listeners: subscribe in OnEnable, unsubscribe in
/// OnDisable, and apply Current once when subscribing.
///
/// Writes are crash-safe: settings.tmp is written first, then swapped over
/// the real file. A missing or corrupt file falls back to defaults.
/// </summary>
public static class SettingsService
{
    private const string FileName = "settings.json";
    private const string LegacySoundKey = "SoundEnabled";

    private static GameSettings current;
    private static bool dirty;

    /// <summary>Raised after any change made through Change / Reset*.</summary>
    public static event Action<GameSettings> OnChanged;

    public static GameSettings Current
    {
        get
        {
            if (current == null) Load();
            return current;
        }
    }

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // Statics survive play-mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        current = null;
        dirty = false;
        OnChanged = null;
        Application.quitting -= Save;
        Application.quitting += Save;
    }

    /// <summary>
    /// Edits the settings and notifies listeners, e.g.
    /// <c>SettingsService.Change(s => s.musicVolume = value);</c>
    /// </summary>
    public static void Change(Action<GameSettings> edit)
    {
        edit(Current);
        dirty = true;
        OnChanged?.Invoke(current);
    }

    /// <summary>Writes to disk if anything changed since the last save.</summary>
    public static void Save()
    {
        if (!dirty || current == null) return;

        string tmpPath = FilePath + ".tmp";
        try
        {
            File.WriteAllText(tmpPath, JsonUtility.ToJson(current, true));
            if (File.Exists(FilePath)) File.Replace(tmpPath, FilePath, null);
            else File.Move(tmpPath, FilePath);
            dirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SettingsService] Couldn't save settings: {e.Message}");
        }
    }

    private static void Load()
    {
        current = null;

        if (File.Exists(FilePath))
        {
            try
            {
                current = JsonUtility.FromJson<GameSettings>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SettingsService] settings.json is unreadable, using defaults: {e.Message}");
            }
        }

        if (current == null)
        {
            current = new GameSettings();
            dirty = true;
        }

        Migrate();
        Save();
    }

    private static void Migrate()
    {
        // OptionsMenu's old Sound ON/OFF toggle (AudioListener.volume hack).
        if (PlayerPrefs.HasKey(LegacySoundKey))
        {
            if (PlayerPrefs.GetInt(LegacySoundKey, 1) == 0) current.masterVolume = 0f;
            PlayerPrefs.DeleteKey(LegacySoundKey);
            PlayerPrefs.Save();
            dirty = true;
        }

        if (current.version < GameSettings.CurrentVersion)
        {
            // Future per-version fix-ups go here.
            current.version = GameSettings.CurrentVersion;
            dirty = true;
        }
    }
}
