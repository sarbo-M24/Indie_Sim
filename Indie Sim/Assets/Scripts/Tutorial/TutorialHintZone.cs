using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public enum TutorialAction
{
    None,         // no action needed — with Require Clear, done once the room is cleared; otherwise the hint just shows
    Move,
    Fire,
    Dash,
    Stomp,
    Reload,
    SwitchWeapon
}

/// <summary>
/// One room of the tutorial. A trigger box: when the player walks in, its hint
/// shows ("{Move} to move"); once they perform Required Action, the banner
/// switches to Complete Text ("Now go to the next room") and the door
/// blockers in Disable On Complete open.
///
/// Combat rooms: list the room's spawners under Spawners. They wake when the
/// player walks in (give them Activate By Proximity off so they don't wake
/// through walls early), and with Require Clear the room is only done once
/// the action has been used AND every enemy they released is dead.
///
/// Add one with Tools ▸ Tutorial ▸ Add Hint Zone, then resize the box to
/// cover the room.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class TutorialHintZone : MonoBehaviour
{
    [Header("Hint")]
    [Tooltip("Shown while the task isn't done. Tokens: {Move} {Fire} {Dash} {Stomp} {Reload} {SwitchWeapon} {Pause}")]
    [TextArea] [SerializeField] private string hint = "{Move} to move";
    [Tooltip("Shown once the task is done. Empty = the TutorialDirector's Default Complete Text.")]
    [TextArea] [SerializeField] private string completeText = "";
    [Tooltip("Shown instead of Complete Text when the tutorial was replayed from the main menu. Empty = Complete Text.")]
    [TextArea] [SerializeField] private string replayCompleteText = "";

    [Header("Task")]
    [SerializeField] private TutorialAction requiredAction = TutorialAction.Move;
    [Tooltip("Move: seconds of movement needed.")]
    [SerializeField] private float moveSeconds = 1f;
    [Tooltip("Fire / Dash / Stomp / Reload / Switch Weapon: presses needed.")]
    [Min(1)] [SerializeField] private int pressCount = 1;

    [Header("Enemies")]
    [Tooltip("Woken when the player first walks into this zone.")]
    [SerializeField] private EnemySpawner[] spawners;
    [Tooltip("The task also needs every spawner above finished and all of their enemies dead.")]
    [SerializeField] private bool requireClear = true;

    [Header("On Complete")]
    [Tooltip("Turned off when the task is done — e.g. a door-blocker tilemap or collider.")]
    [SerializeField] private GameObject[] disableOnComplete;
    [Tooltip("Turned on when the task is done — e.g. an arrow pointing to the next room.")]
    [SerializeField] private GameObject[] enableOnComplete;
    [SerializeField] private UnityEvent onComplete;

    public string Hint => hint;
    public string CompleteText =>
        GameManager.Instance != null && GameManager.Instance.IsTutorialReplay && !string.IsNullOrEmpty(replayCompleteText)
            ? replayCompleteText
            : completeText;
    public bool Completed { get; private set; }

    private float moveTime;
    private int presses;
    private bool actionDone;
    private bool spawnersWoken;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        SetAll(enableOnComplete, false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        WakeSpawners();
        TutorialDirector director = TutorialDirector.Instance;
        if (director != null && director.CurrentZone != this) director.EnterZone(this);
    }

    private bool HasEnemies => requireClear && spawners != null && spawners.Length > 0;

    private void Update()
    {
        if (Completed) return;
        if (requiredAction == TutorialAction.None && !HasEnemies) return; // info-only zone

        // Progress only counts in the zone the player last walked into.
        TutorialDirector director = TutorialDirector.Instance;
        if (director == null || director.CurrentZone != this) return;

        if (!actionDone) actionDone = TrackAction();
        if (actionDone && (!HasEnemies || RoomCleared())) Complete();
    }

    private bool TrackAction()
    {
        PlayerControls.PlayerActions player = InputManager.Controls.Player;
        switch (requiredAction)
        {
            case TutorialAction.Move:
                // deltaTime, not unscaled: no progress while paused.
                if (player.Move.ReadValue<Vector2>().sqrMagnitude > 0.04f) moveTime += Time.deltaTime;
                return moveTime >= moveSeconds;
            case TutorialAction.Fire: return CountPress(player.Fire);
            case TutorialAction.Dash: return CountPress(player.Dash);
            case TutorialAction.Stomp: return CountPress(player.Stomp);
            case TutorialAction.Reload: return CountPress(player.Reload);
            case TutorialAction.SwitchWeapon: return CountPress(player.SwitchWeapon);
            default: return true; // None
        }
    }

    // The Player map is off while paused / in a menu, so those presses never count.
    private bool CountPress(InputAction action)
    {
        if (action.WasPerformedThisFrame()) presses++;
        return presses >= pressCount;
    }

    private void WakeSpawners()
    {
        if (spawnersWoken || spawners == null) return;
        spawnersWoken = true;
        foreach (EnemySpawner spawner in spawners)
            if (spawner != null) spawner.ActivateSpawner();
    }

    private bool RoomCleared()
    {
        foreach (EnemySpawner spawner in spawners)
            if (spawner != null && !spawner.IsCleared()) return false;
        return true;
    }

    /// <summary>Marks the task done. Public so it can also be driven from a UnityEvent (e.g. a dummy dying).</summary>
    public void Complete()
    {
        if (Completed) return;
        Completed = true;

        SetAll(disableOnComplete, false);
        SetAll(enableOnComplete, true);
        onComplete?.Invoke();

        if (TutorialDirector.Instance != null) TutorialDirector.Instance.ZoneCompleted(this);
    }

    private static void SetAll(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (GameObject go in objects)
            if (go != null) go.SetActive(active);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null) return;

        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.08f);
        Gizmos.DrawCube(box.offset, box.size);
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireCube(box.offset, box.size);
        Gizmos.matrix = old;

        Vector3 top = transform.TransformPoint(box.offset + new Vector2(-box.size.x * 0.5f, box.size.y * 0.5f));
        UnityEditor.Handles.Label(top, $"{name}\n{requiredAction}{(HasEnemies ? " + clear room" : "")}: {hint}");

        // Lines to this room's spawners.
        if (spawners == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        foreach (EnemySpawner spawner in spawners)
            if (spawner != null) Gizmos.DrawLine(transform.position, spawner.transform.position);
    }
#endif
}
