using System;

/// <summary>
/// Every player-facing option, with its default. Serialized as-is to
/// settings.json by SettingsService (JsonUtility, so public fields only).
/// Adding an option = add a field here with its default, then read it where
/// it applies (listen to SettingsService.OnChanged if it must update live).
/// Fields missing from an older file keep the defaults below.
/// </summary>
[Serializable]
public class GameSettings
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;

    // ── Audio (0–1, linear slider values; AudioManager converts to dB) ──
    public float masterVolume = 1f;
    public float musicVolume = 0.8f;
    public float sfxVolume = 1f;
    public float uiVolume = 1f;
    public bool muteWhenUnfocused = false;

    // ── Gameplay ──
    public bool damageNumbers = true;
    public bool psychedelicMode = false;
    public float screenShake = 1f;      // 0–1, scales every camera shake / recoil kick
    public float cameraLead = 1f;       // 0–1, scales how far the camera leads toward the aim
    public float flashIntensity = 1f;   // 0–1, damage vignette flash (photosensitivity)

    // ── Controls ──
    public string bindingOverridesJson = "";

    public GameSettings Clone() => (GameSettings)MemberwiseClone();

    public void ResetAudio()
    {
        GameSettings d = new GameSettings();
        masterVolume = d.masterVolume;
        musicVolume = d.musicVolume;
        sfxVolume = d.sfxVolume;
        uiVolume = d.uiVolume;
        muteWhenUnfocused = d.muteWhenUnfocused;
    }

    public void ResetGameplay()
    {
        GameSettings d = new GameSettings();
        damageNumbers = d.damageNumbers;
        psychedelicMode = d.psychedelicMode;
        screenShake = d.screenShake;
        cameraLead = d.cameraLead;
        flashIntensity = d.flashIntensity;
    }
}
