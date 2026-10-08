using UnityEngine;

/// <summary>
/// Plays the game's music across every scene. Spawned on first use and kept
/// with DontDestroyOnLoad, so nothing needs wiring in Boot.unity and music
/// carries over scene loads instead of cutting out.
///
/// Scenes say what they want with a SceneMusic component (menu, tutorial,
/// dungeon, boss). A new track crossfades in over the old one on two "decks"
/// (equal-power, on unscaled time, so it keeps going while paused); asking
/// for the track that's already playing changes nothing, so the song runs on
/// through tutorial → dungeon or a retry. A scene without SceneMusic leaves
/// the music as it is.
///
/// Every source goes through AudioManager.MusicGroup, so the Music slider
/// applies.
/// </summary>
public class MusicDirector : MonoBehaviour
{
    private const float DefaultFadeSeconds = 1.5f;
    private const float IntensityPerSecond = 1f; // how fast the combat layer follows SetCombatIntensity

    private static MusicDirector instance;
    private static bool quitting;

    private Deck _a;
    private Deck _b;
    private Deck _active;

    /// <summary>The director, created on first use. Null while the game is quitting.</summary>
    public static MusicDirector Instance
    {
        get
        {
            if (instance == null && !quitting)
            {
                GameObject go = new GameObject("[MusicDirector]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<MusicDirector>();
            }
            return instance;
        }
    }

    /// <summary>The track playing (or fading in) now; null when silent.</summary>
    public MusicTrack CurrentTrack => _active != null && _active.FadingIn ? _active.Track : null;

    // Statics survive play-mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        quitting = false;
    }

    private void Awake()
    {
        Application.quitting += () => quitting = true;
        _a = new Deck(gameObject);
        _b = new Deck(gameObject);
        _active = _a;
    }

    /// <summary>
    /// Crossfades to `track` over `fadeSeconds`. Null fades to silence. The
    /// track already playing keeps playing.
    /// </summary>
    public void Play(MusicTrack track, float fadeSeconds = DefaultFadeSeconds)
    {
        if (track == CurrentTrack) return;

        _active.FadeTo(0f, fadeSeconds);
        if (track == null) return;

        _active = _active == _a ? _b : _a;

        // Switched straight back while the old song was still fading out:
        // bring it back up where it is rather than restarting it.
        if (_active.Track == track) _active.FadeTo(1f, fadeSeconds);
        else _active.StartTrack(track, fadeSeconds);
    }

    /// <summary>Fades the music out (same as Play(null)).</summary>
    public void Stop(float fadeSeconds = DefaultFadeSeconds) => Play(null, fadeSeconds);

    /// <summary>
    /// 0 = calm, 1 = full combat: brings in the current track's combat layer
    /// and ducks its main clip. Smoothed here, so call it every frame if you like.
    /// </summary>
    public void SetCombatIntensity(float intensity) => _active.TargetIntensity = Mathf.Clamp01(intensity);

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _a.Tick(dt);
        _b.Tick(dt);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>One track's main + combat sources and their fade.</summary>
    private class Deck
    {
        private readonly AudioSource _main;
        private readonly AudioSource _combat;

        private float _fade;          // 0–1, linear; heard through an equal-power curve
        private float _fadeTarget;
        private float _fadePerSecond;
        private float _intensity;

        public MusicTrack Track { get; private set; }
        public float TargetIntensity { get; set; }

        /// <summary>Playing and not on its way out.</summary>
        public bool FadingIn => Track != null && _fadeTarget > 0f;

        public Deck(GameObject host)
        {
            _main = NewSource(host);
            _combat = NewSource(host);
        }

        private static AudioSource NewSource(GameObject host)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.ignoreListenerPause = true;
            source.outputAudioMixerGroup = AudioManager.MusicGroup;
            return source;
        }

        public void StartTrack(MusicTrack track, float fadeSeconds)
        {
            Track = track;
            _intensity = 0f;
            TargetIntensity = 0f;

            _fade = 0f;
            Begin(_main, track.clip, track.loopStart);
            Begin(_combat, track.combatLayer, track.combatLoopStart);
            FadeTo(1f, fadeSeconds);
            Apply();
        }

        public void FadeTo(float target, float seconds)
        {
            if (Track == null) return;
            _fadeTarget = target;
            _fadePerSecond = seconds > 0f ? 1f / seconds : float.PositiveInfinity;
        }

        public void Tick(float dt)
        {
            if (Track == null) return;

            _fade = Mathf.MoveTowards(_fade, _fadeTarget, _fadePerSecond * dt);
            _intensity = Mathf.MoveTowards(_intensity, TargetIntensity, IntensityPerSecond * dt);

            if (_fade <= 0f && _fadeTarget <= 0f)
            {
                _main.Stop();
                _combat.Stop();
                _main.clip = _combat.clip = null;
                Track = null;
                return;
            }

            KeepLooping(_main, Track.loopStart);
            KeepLooping(_combat, Track.combatLoopStart);
            Apply();
        }

        private void Apply()
        {
            float gain = Mathf.Sin(_fade * Mathf.PI * 0.5f);
            bool hasLayer = Track.combatLayer != null;
            float mainMix = hasLayer ? Mathf.Lerp(1f, Track.mainVolumeInCombat, _intensity) : 1f;

            _main.volume = gain * Track.volume * mainMix;
            _combat.volume = hasLayer ? gain * Track.combatVolume * _intensity : 0f;
        }

        private static void Begin(AudioSource source, AudioClip clip, float loopStart)
        {
            source.Stop();
            source.clip = clip;
            if (clip == null) return;

            // A loop point needs the manual restart in KeepLooping.
            source.loop = loopStart <= 0f;
            source.time = 0f;
            source.Play();
        }

        private static void KeepLooping(AudioSource source, float loopStart)
        {
            if (source.clip == null || source.loop || source.isPlaying) return;

            source.time = Mathf.Min(loopStart, source.clip.length - 0.01f);
            source.Play();
        }
    }
}
