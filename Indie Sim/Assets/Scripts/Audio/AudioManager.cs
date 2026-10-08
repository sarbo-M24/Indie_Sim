using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the volume settings to the audio mixer and hands out its groups.
///
/// The mixer is Assets/Resources/MainMixer.mixer (made by hand, see
/// SettingsAudioInputPlan.md): groups Master → Music, SFX, UI, with the
/// volumes exposed as MasterVol, MusicVol, SfxVol, UiVol. Loaded from
/// Resources so nothing needs wiring in Boot.unity — static, like
/// InputManager. Without the mixer, the Master slider still works through
/// AudioListener.volume; the per-group sliders do nothing until it exists.
///
/// Routing: MusicDirector sends its sources to MusicGroup. Any scene
/// AudioSource with no output group is sent to SfxGroup on scene load.
/// Sources on prefabs spawned later (enemies, bullets) need their Output set
/// to SFX on the prefab itself.
/// </summary>
public static class AudioManager
{
    private const string MixerResourcePath = "MainMixer";
    private const float MinDb = -80f;

    private static AudioMixer mixer;
    private static bool mixerLookedUp;

    public static AudioMixerGroup MusicGroup { get; private set; }
    public static AudioMixerGroup SfxGroup { get; private set; }
    public static AudioMixerGroup UiGroup { get; private set; }

    // Statics survive play-mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        mixer = null;
        mixerLookedUp = false;
        MusicGroup = SfxGroup = UiGroup = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        LookUpMixer();

        SettingsService.OnChanged += OnSettingsChanged;
        Application.focusChanged += OnFocusChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting += Shutdown;
    }

    private static void Shutdown()
    {
        SettingsService.OnChanged -= OnSettingsChanged;
        Application.focusChanged -= OnFocusChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.quitting -= Shutdown;
    }

    private static void OnSettingsChanged(GameSettings settings) => ApplyVolumes();
    private static void OnFocusChanged(bool focused) => ApplyVolumes();

    private static void LookUpMixer()
    {
        if (mixerLookedUp) return;
        mixerLookedUp = true;

        mixer = Resources.Load<AudioMixer>(MixerResourcePath);
        if (mixer == null)
        {
            Debug.LogWarning("[AudioManager] No Resources/MainMixer.mixer yet — only the Master volume applies (via AudioListener).");
            return;
        }

        MusicGroup = FindGroup("Music");
        SfxGroup = FindGroup("SFX");
        UiGroup = FindGroup("UI");
    }

    private static AudioMixerGroup FindGroup(string name)
    {
        foreach (AudioMixerGroup group in mixer.FindMatchingGroups(name))
            if (group.name == name) return group;

        Debug.LogWarning($"[AudioManager] MainMixer has no '{name}' group.");
        return null;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RouteUnassignedSources(scene);

        // Mixer SetFloat is unreliable before the first scene has loaded, so
        // re-apply on every load as well as on each change.
        ApplyVolumes();
    }

    private static void RouteUnassignedSources(Scene scene)
    {
        if (SfxGroup == null) return;

        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                if (source.outputAudioMixerGroup == null)
                    source.outputAudioMixerGroup = SfxGroup;
    }

    private static void ApplyVolumes()
    {
        GameSettings s = SettingsService.Current;
        bool muted = s.muteWhenUnfocused && !Application.isFocused;
        float master = muted ? 0f : s.masterVolume;

        if (mixer == null)
        {
            AudioListener.volume = master;
            return;
        }

        AudioListener.volume = 1f;
        mixer.SetFloat("MasterVol", ToDb(master));
        mixer.SetFloat("MusicVol", ToDb(s.musicVolume));
        mixer.SetFloat("SfxVol", ToDb(s.sfxVolume));
        mixer.SetFloat("UiVol", ToDb(s.uiVolume));
    }

    /// <summary>Linear 0–1 slider value to decibels; 0 is silence (-80 dB).</summary>
    public static float ToDb(float linear) =>
        linear <= 0.0001f ? MinDb : Mathf.Max(MinDb, 20f * Mathf.Log10(linear));
}
