using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-scene marker: "this scene plays this track". On Start it hands the
/// track to MusicDirector, which crossfades to it (or keeps playing if it's
/// already on). An empty track fades the music out. Scenes without one keep
/// whatever is playing. Wait For Instructions Panel holds the track until
/// the panel's Skip / timer (TutorialManager → ReleaseCue).
///
/// With Drive Combat Layer on, it also raises the track's combat layer as
/// enemies the player can see (no wall in between) get close — the old
/// MusicManager behaviour, now on top of the shared director.
/// </summary>
public class SceneMusic : MonoBehaviour
{
    private const float CheckInterval = 0.1f; // the director smooths between checks

    [SerializeField] private MusicTrack track;
    [Tooltip("Crossfade length from the previous scene's music, in seconds.")]
    [SerializeField, Min(0f)] private float fadeSeconds = 1.5f;
    [Tooltip("Hold the track until the instructions panel closes (TutorialManager's Skip / timer → SceneMusic.ReleaseCue). " +
             "The previous scene's music fades out meanwhile. If this track is already playing (the next floor), it just carries on.")]
    [SerializeField] private bool waitForInstructionsPanel;

    [Header("Combat layer")]
    [Tooltip("Raise the track's combat layer as visible enemies get close.")]
    [SerializeField] private bool driveCombatLayer;
    [Tooltip("Optional. Found by the Player tag when empty or destroyed.")]
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask wallLayer;
    [Tooltip("Enemies inside this radius count; intensity is 1 at the player, 0 at the edge.")]
    [SerializeField, Min(0.1f)] private float detectionRadius = 10f;

    private readonly List<Collider2D> _hits = new List<Collider2D>();
    private float _nextCheck;
    private bool _started;
    private bool _cueReleased;

    /// <summary>
    /// Starts the current scene's music if it was waiting for the instructions
    /// panel. Safe to call before this scene's SceneMusic has started, or when
    /// it isn't waiting at all.
    /// </summary>
    public static void ReleaseCue()
    {
        SceneMusic music = FindFirstObjectByType<SceneMusic>();
        if (music == null || music._cueReleased) return;

        music._cueReleased = true;
        if (music._started) music.PlayTrack();
    }

    private void Start()
    {
        _started = true;

        MusicDirector director = MusicDirector.Instance;
        if (director == null) return;

        if (waitForInstructionsPanel && !_cueReleased && director.CurrentTrack != track)
            director.Stop(fadeSeconds);
        else
            PlayTrack();
    }

    private void PlayTrack()
    {
        MusicDirector director = MusicDirector.Instance;
        if (director != null) director.Play(track, fadeSeconds);
    }

    private void Update()
    {
        if (!driveCombatLayer || Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + CheckInterval;

        MusicDirector director = MusicDirector.Instance;
        if (director != null && director.CurrentTrack == track)
            director.SetCombatIntensity(MeasureIntensity());
    }

    private void OnDisable()
    {
        // Don't leave the next scene's music stuck in combat.
        if (driveCombatLayer && MusicDirector.Instance != null && MusicDirector.Instance.CurrentTrack == track)
            MusicDirector.Instance.SetCombatIntensity(0f);
    }

    private float MeasureIntensity()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found == null) return 0f;
            player = found.transform;
        }

        Vector2 origin = player.position;
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(enemyLayer);
        Physics2D.OverlapCircle(origin, detectionRadius, filter, _hits);

        float closest = detectionRadius;
        foreach (Collider2D enemy in _hits)
        {
            Vector2 toEnemy = (Vector2)enemy.transform.position - origin;
            float distance = toEnemy.magnitude;
            if (distance >= closest) continue;
            if (Physics2D.Raycast(origin, toEnemy / Mathf.Max(distance, 0.0001f), distance, wallLayer).collider != null) continue;
            closest = distance;
        }

        return 1f - closest / detectionRadius;
    }

    private void OnDrawGizmosSelected()
    {
        if (!driveCombatLayer || player == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(player.position, detectionRadius);
    }
}
