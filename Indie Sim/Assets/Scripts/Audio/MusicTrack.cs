using UnityEngine;

/// <summary>
/// One piece of music as a scene asks for it (SceneMusic → MusicDirector).
/// Scenes that name the same MusicTrack asset share it: moving between them
/// keeps the song playing instead of restarting it (tutorial → dungeon,
/// dungeon → next floor).
///
/// The optional combat layer plays alongside the main clip, silent until a
/// SceneMusic with enemy detection raises the combat intensity; the main clip
/// ducks as it comes in.
/// </summary>
[CreateAssetMenu(menuName = "Audio/Music Track", fileName = "MusicTrack")]
public class MusicTrack : ScriptableObject
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Seconds into the clip where it loops back to. 0 = loop the whole clip.")]
    [Min(0f)] public float loopStart;

    [Header("Combat layer (optional)")]
    [Tooltip("Fades in over the main clip as enemies get close. Leave empty for no layer.")]
    public AudioClip combatLayer;
    [Range(0f, 1f)] public float combatVolume = 1f;
    [Tooltip("Seconds into the combat clip where it loops back to. 0 = loop the whole clip.")]
    [Min(0f)] public float combatLoopStart;
    [Tooltip("Main clip volume (× volume) at full combat intensity.")]
    [Range(0f, 1f)] public float mainVolumeInCombat = 0.5f;
}
