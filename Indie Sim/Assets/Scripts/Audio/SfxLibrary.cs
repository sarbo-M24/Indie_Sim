using System;
using UnityEngine;

/// <summary>The shared sounds Sfx.Play can play. Add an id here, then its clip in the library asset.</summary>
public enum SfxId
{
    UiClick,
    Dash,
    Stomp,
    PlayerDeath,
    EnemyDeath,
}

/// <summary>
/// The clip and mix for each SfxId. Lives at Assets/Resources/SfxLibrary.asset
/// (loaded by Sfx, so nothing needs wiring in scenes).
/// </summary>
[CreateAssetMenu(menuName = "Audio/Sfx Library", fileName = "SfxLibrary")]
public class SfxLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public SfxId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch ± this much each play, so repeats don't sound identical.")]
        [Range(0f, 0.3f)] public float pitchJitter = 0.05f;
        [Tooltip("Plays closer together than this (seconds) are dropped — e.g. a stomp killing ten enemies at once.")]
        [Min(0f)] public float minInterval = 0.03f;
    }

    public Entry[] entries;

    public Entry Find(SfxId id)
    {
        if (entries == null) return null;
        foreach (Entry entry in entries)
            if (entry.id == id) return entry;
        return null;
    }
}
