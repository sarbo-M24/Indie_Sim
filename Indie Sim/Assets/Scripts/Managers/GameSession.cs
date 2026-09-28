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
    /// Folds run results into PersistentStats and saves. Not called from
    /// anywhere yet — Phase 5 wires death/boss-defeat/quit into this.
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
    }
}
