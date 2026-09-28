/// <summary>Slider-backed settings (0–1 values). Picked per SliderRow in the Inspector.</summary>
public enum FloatSetting
{
    MasterVolume,
    MusicVolume,
    SfxVolume,
    UiVolume,
    ScreenShake,
    CameraLead,
    FlashIntensity,
}

/// <summary>On/off settings. Picked per ToggleRow in the Inspector.</summary>
public enum BoolSetting
{
    MuteWhenUnfocused,
    DamageNumbers,
    PsychedelicMode,
}

/// <summary>
/// Maps the Inspector enums onto GameSettings fields, so a row is wired by
/// picking a value from a dropdown. Adding an option: add the field to
/// GameSettings, a value to one enum above, and a case to each switch below.
/// </summary>
public static class SettingBindings
{
    public static float Get(FloatSetting setting)
    {
        GameSettings s = SettingsService.Current;
        switch (setting)
        {
            case FloatSetting.MasterVolume: return s.masterVolume;
            case FloatSetting.MusicVolume: return s.musicVolume;
            case FloatSetting.SfxVolume: return s.sfxVolume;
            case FloatSetting.UiVolume: return s.uiVolume;
            case FloatSetting.ScreenShake: return s.screenShake;
            case FloatSetting.CameraLead: return s.cameraLead;
            case FloatSetting.FlashIntensity: return s.flashIntensity;
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
                case FloatSetting.FlashIntensity: s.flashIntensity = value; break;
            }
        });
    }

    public static bool Get(BoolSetting setting)
    {
        GameSettings s = SettingsService.Current;
        switch (setting)
        {
            case BoolSetting.MuteWhenUnfocused: return s.muteWhenUnfocused;
            case BoolSetting.DamageNumbers: return s.damageNumbers;
            case BoolSetting.PsychedelicMode: return s.psychedelicMode;
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
            }
        });
    }
}
