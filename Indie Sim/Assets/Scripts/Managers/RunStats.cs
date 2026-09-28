using System.Collections.Generic;

/// <summary>
/// Run-scoped data. Lives only in memory for the duration of one run
/// (Main Menu -> death/demo-complete). Reset by GameSession.StartNewRun()
/// constructing a fresh instance — nothing here has its own reset method.
/// Field shapes mirror what each live manager currently tracks; the managers
/// themselves start reading/writing these in Phase 6.
/// </summary>
[System.Serializable]
public class RunStats
{
    // Coins (CoinManager)
    public int CurrentCoins;
    public int CoinsCollectedThisRun;
    public int MaxCoins;

    // Kills (EnemyKillTracker)
    public int KillsThisRun;

    // Relics (RelicManager) — indexed by Relic.relicIndex, saved as RelicIds
    public bool[] RelicsHeld = new bool[RelicIds.Count];
    public int UniqueRelicsCollectedThisRun;

    // Upgrades (Pack, CigPool)
    public List<CigInstance> HeldCigs = new List<CigInstance>();   // max 5
    public List<string> PurchasedCigIds = new List<string>();      // removed from pool for the run

    // Weapon loadout + ammo (WeaponInventory, WeaponAmmoManager). WeaponData
    // is a ScriptableObject asset — stable reference across scene loads, safe
    // as a dictionary key. Saved by ID (run.weapons), never serialized directly.
    public WeaponData EquippedWeapon;
    public List<WeaponData> OwnedWeapons = new List<WeaponData>(); // the carried loadout; unlocks will add here
    public Dictionary<WeaponData, int> WeaponAmmo = new Dictionary<WeaponData, int>();

    // Dungeon progression (RoguelikeManager.dungeonsClearedCount)
    public int CurrentDungeonLevel = 1;
    public int DungeonsClearedThisRun;
    public int DungeonSizeIncrement; // RoguelikeManager's random size growth
    public RunResumePoint ResumePoint = RunResumePoint.LevelStart;

    // Run timer
    public float RunElapsedSeconds;

    // Player health (PlayerHealth)
    public float CurrentHealth;
    public float MaxHealth;

    // Boss (D1)
    public BossDefinition SelectedBoss;
}

/// <summary>Where a resumed run picks up (save-system-spec.md §4).</summary>
public enum RunResumePoint
{
    LevelStart, // start CurrentDungeonLevel fresh
    Store       // in the store after clearing the previous dungeon
}
