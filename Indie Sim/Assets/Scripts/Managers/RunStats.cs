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

    // Relics (RelicManager)
    public bool[] RelicsHeld = new bool[8];
    public int UniqueRelicsCollectedThisRun;

    // Upgrades (Pack, CigPool)
    public List<CigInstance> HeldCigs = new List<CigInstance>();   // max 5
    public List<string> PurchasedCigIds = new List<string>();      // removed from pool for the run

    // Weapon loadout + ammo (WeaponInventory, WeaponAmmoManager). WeaponData
    // is a ScriptableObject asset — stable reference across scene loads, safe
    // as a dictionary key. Never JSON-serialized (only PersistentStats is).
    public WeaponData EquippedWeapon;
    public Dictionary<WeaponData, int> WeaponAmmo = new Dictionary<WeaponData, int>();

    // Dungeon progression (RoguelikeManager.dungeonsClearedCount)
    public int CurrentDungeonLevel = 1;
    public int DungeonsClearedThisRun;

    // Run timer
    public float RunElapsedSeconds;

    // Player health (PlayerHealth)
    public float CurrentHealth;
    public float MaxHealth;

    // Boss (D1)
    public BossDefinition SelectedBoss;
}
