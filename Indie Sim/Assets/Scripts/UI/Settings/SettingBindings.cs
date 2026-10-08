/// <summary>Slider-backed settings (0–1 values). Picked per SliderRow in the Inspector.</summary>
public enum FloatSetting
{
    MasterVolume,
    MusicVolume,
    SfxVolume,
    UiVolume,
    ScreenShake,
    CameraLead,
    DamageFlashIntensity, // GameSettings.flashIntensity (field name kept so saved settings carry over)
    PsychedelicIntensity,
}

/// <summary>On/off settings. Picked per ToggleRow in the Inspector.</summary>
public enum BoolSetting
{
    MuteWhenUnfocused,
    DamageNumbers,
    PsychedelicMode,
    SwapSticks,
}

/// <summary>
/// Maps the Inspector enums onto GameSettings fields, so a row is wired by
/// picking a value from a dropdown. Adding an option: add the field to
/// GameSettings, a value to one enum above, and a case to each switch below.
/// </summary>
public static class SettingBindings
{
    private static readonly GameSettings Defaults = new GameSettings();

    public static float Get(FloatSetting setting) => Get(setting, SettingsService.Current);
    public static float GetDefault(FloatSetting setting) => Get(setting, Defaults);
    public static bool Get(BoolSetting setting) => Get(setting, SettingsService.Current);
    public static bool GetDefault(BoolSetting setting) => Get(setting, Defaults);

    private static float Get(FloatSetting setting, GameSettings s)
    {
        switch (setting)
        {
            case FloatSetting.MasterVolume: return s.masterVolume;
            case FloatSetting.MusicVolume: return s.musicVolume;
            case FloatSetting.SfxVolume: return s.sfxVolume;
            case FloatSetting.UiVolume: return s.uiVolume;
            case FloatSetting.ScreenShake: return s.screenShake;
            case FloatSetting.CameraLead: return s.cameraLead;
            case FloatSetting.DamageFlashIntensity: return s.flashIntensity;
            case FloatSetting.PsychedelicIntensity: return s.psychedelicIntensity;
            default: return 0f;
        }
    }

    public static void Set(FloatSetting setting, float value)
    {
        SettingsService.Change(s =>
        {
            switch (setting)
            {
                case FloatSetting.MasterVolume: s.masterVolume = value; break;
                case FloatSetting.MusicVolume: s.musicVolume = value; break;
                case FloatSetting.SfxVolume: s.sfxVolume = value; break;
                case FloatSetting.UiVolume: s.uiVolume = value; break;
                case FloatSetting.ScreenShake: s.screenShake = value; break;
                case FloatSetting.CameraLead: s.cameraLead = value; break;
                case FloatSetting.DamageFlashIntensity: s.flashIntensity = value; break;
                case FloatSetting.PsychedelicIntensity: s.psychedelicIntensity = value; break;
            }
        });
    }

    private static bool Get(BoolSetting setting, GameSettings s)
    {
        switch (setting)
        {
            case BoolSetting.MuteWhenUnfocused: return s.muteWhenUnfocused;
            case BoolSetting.DamageNumbers: return s.damageNumbers;
            case BoolSetting.PsychedelicMode: return s.psychedelicMode;
            case BoolSetting.SwapSticks: return s.swapSticks;
            default: return false;
        }
    }

    public static void Set(BoolSetting setting, bool value)
    {
        SettingsService.Change(s =>
        {
            switch (setting)
            {
                case BoolSetting.MuteWhenUnfocused: s.muteWhenUnfocused = value; break;
                case BoolSetting.DamageNumbers: s.damageNumbers = value; break;
                case BoolSetting.PsychedelicMode: s.psychedelicMode = value; break;
                case BoolSetting.SwapSticks: s.swapSticks = value; break;
            }
        });
    }
}
