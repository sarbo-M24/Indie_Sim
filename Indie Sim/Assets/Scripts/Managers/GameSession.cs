using UnityEngine;

/// <summary>
/// The sole persistent object permitted to hold run-scoped mutable state.
/// Lives under the [Persistent] root in Boot.unity.
/// </summary>
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    public RunStats CurrentRun { get; private set; } = new RunStats();
    public PersistentStats Persistent { get; private set; } = new PersistentStats();

    /// <summary>The active save slot's own data (survives death). Set when a slot is picked (4C).</summary>
    public SlotData Slot { get; private set; } = new SlotData();

    /// <summary>Slot/profile save files (save-system-spec.md). Built in Awake by SaveBootstrap.</summary>
    public SaveService Saves { get; private set; }

    // Guards EndRun() against double-invocation (D2: death and boss-defeat
    // both route into it). Cleared by StartNewRun().
    private bool _runEnding;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Saves = SaveBootstrap.Create(this);
        LegacySaveImport.LoadProfile(Saves, Persistent);
    }

    /// <summary>
    /// Writes the global profile (achievements, lifetime stats, demo unlock).
    /// Never touches a slot — slot writes happen only at the save points.
    /// </summary>
    public void Save() => Saves.WriteProfile();

    /// <summary>
    /// The only reset in the project (Phase 6). Every manager that used to
    /// need an external reset call (CoinManager, EnemyKillTracker,
    /// UpgradeManager, WeaponUnlockManager) and the player itself are now
    /// scene-local — resetting a fresh RunStats here is sufficient because
    /// each of them reads its starting state from CurrentRun in its own
    /// Awake()/Start() when the next scene load recreates them.
    /// </summary>
    public void StartNewRun()
    {
        Debug.Log("[GameSession] StartNewRun() called.");
        CurrentRun = new RunStats();
        Persistent.TotalRuns++;
        _runEnding = false;
    }

    /// <summary>
    /// Slot select picked an Empty slot (newSlotName = what the player typed)
    /// or a named slot with no active run (newSlotName = null). Makes it the
    /// active slot for the session, starts a fresh run and writes the slot
    /// (spec §4: new run → full write, LevelStart(1)). False if the slot
    /// can't be read.
    /// </summary>
    public bool BeginNewRunInSlot(int slot, string newSlotName)
    {
        Saves.SetActiveSlot(slot);
        Slot = new SlotData();

        if (newSlotName != null)
            Slot.Name = newSlotName;
        else if (Saves.LoadActiveSlot() == LoadResult.Corrupted)
            return false;

        StartNewRun();
        return Saves.WriteActiveSlot();
    }

    /// <summary>
    /// Slot select / Continue picked a slot with an active run: makes it the
    /// active slot and restores its Slot + Run data. False if it can't be read.
    /// </summary>
    public bool ResumeRunInSlot(int slot)
    {
        Saves.SetActiveSlot(slot);
        Slot = new SlotData();
        CurrentRun = new RunStats();

        if (Saves.LoadActiveSlot() != LoadResult.Loaded)
        {
            Debug.LogError($"[GameSession] Slot {slot} has no run to resume.");
            return false;
        }

        _runEnding = false;
        return true;
    }

    /// <summary>
    /// Save point (spec §4): writes the whole run to the active slot with the
    /// given resume point — Store on entering the store / Save & Exit,
    /// LevelStart when leaving the store for the next dungeon.
    /// </summary>
    public bool SaveRun(RunResumePoint resumePoint)
    {
        CurrentRun.ResumePoint = resumePoint;
        return Saves.WriteActiveSlot();
    }

    /// <summary>
    /// Run end (death or victory — reached only through GameManager.FinishRun).
    /// Write order matters (spec §1): the profile first (lifetime stats, and
    /// the demo unlock on victory), then the slot with its Run data wiped. A
    /// crash between the two leaves the unlock set and the run resumable —
    /// harmless; the reverse order could lose the unlock. Idempotent: death
    /// and boss defeat both route here, and the death/demo screens' Main Menu
    /// button calls it again via ReturnToMainMenu.
    /// </summary>
    public void EndRun(bool completed)
    {
        Debug.Log($"[GameSession] EndRun(completed:{completed}) called. _runEnding was {_runEnding}.");
        if (_runEnding) return;
        _runEnding = true;

        Persistent.BestRunDungeonsCleared = Mathf.Max(Persistent.BestRunDungeonsCleared, CurrentRun.DungeonsClearedThisRun);
        if (completed)
            Persistent.DemoCompleted = true;

        Save();
        Saves.WriteActiveSlotWipingRun();
    }
}
