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
    public bool muteWhenUnfocused = false; // unused: option removed from Settings, kept so saved settings still load

    // ── Gameplay ──
    public bool damageNumbers = true;
    public bool psychedelicMode = false;
    public float screenShake = 1f;      // 0–1, scales every camera shake / recoil kick
    public float cameraLead = 1f;       // 0–1, scales how far the camera leads toward the aim
    public float flashIntensity = 1f;   // 0–1, damage vignette flash (photosensitivity) — "Damage Flash Intensity"
    public float psychedelicIntensity = 1f; // 0–1, how far Psychedelic mode's colours replace normal blood

    // ── Controls ──
    public string bindingOverridesJson = "";
    public bool swapSticks = false;     // gamepad: move on the right stick, aim on the left
}
