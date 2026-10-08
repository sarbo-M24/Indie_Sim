using UnityEngine;

/// <summary>
/// Plays the shared sound effects (SfxId) from Resources/SfxLibrary.asset:
/// Sfx.Play(SfxId.Dash). Static, like AudioManager — the first call makes a
/// small pool of 2D AudioSources that survives scene loads, so a sound isn't
/// cut off when its object is destroyed or the scene changes (a menu click
/// that loads the game, an enemy that dies and despawns).
///
/// UiClick goes through the mixer's UI group, everything else through SFX,
/// so the volume sliders apply.
/// </summary>
public static class Sfx
{
    private const string LibraryResourcePath = "SfxLibrary";
    private const int PoolSize = 12;

    private static SfxLibrary library;
    private static bool libraryLookedUp;
    private static AudioSource[] pool;
    private static int next;
    private static float[] lastPlayed;

    // Statics survive play-mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        library = null;
        libraryLookedUp = false;
        pool = null;
        next = 0;
        lastPlayed = null;
    }

    public static void Play(SfxId id)
    {
        SfxLibrary.Entry entry = Library != null ? Library.Find(id) : null;
        if (entry == null || entry.clip == null) return;

        float now = Time.unscaledTime;
        int index = (int)id;
        if (now - lastPlayed[index] < entry.minInterval) return;
        lastPlayed[index] = now;

        if (pool == null || pool[0] == null) CreatePool();

        // Round-robin; the oldest sound gives way when all are busy.
        AudioSource source = pool[next];
        next = (next + 1) % pool.Length;

        source.outputAudioMixerGroup = id == SfxId.UiClick ? AudioManager.UiGroup : AudioManager.SfxGroup;
        source.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
        source.clip = entry.clip;
        source.volume = entry.volume;
        source.Play();
    }

    private static SfxLibrary Library
    {
        get
        {
            if (!libraryLookedUp)
            {
                libraryLookedUp = true;
                library = Resources.Load<SfxLibrary>(LibraryResourcePath);
                lastPlayed = new float[System.Enum.GetValues(typeof(SfxId)).Length];
                for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = float.NegativeInfinity;
                if (library == null) Debug.LogWarning("[Sfx] No Resources/SfxLibrary.asset — sound effects are silent.");
            }
            return library;
        }
    }

    private static void CreatePool()
    {
        GameObject host = new GameObject("[Sfx]");
        Object.DontDestroyOnLoad(host);

        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            pool[i] = source;
        }
    }
}
