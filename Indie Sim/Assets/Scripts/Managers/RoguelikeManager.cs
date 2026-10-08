using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class RoguelikeManager : MonoBehaviour
{
    public static RoguelikeManager Instance;

    [SerializeField] private DungeonMapGenerator dungeonGenerator;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Teleporter teleporter;

    [Header("Demo Settings")]
    [SerializeField] private int roomsTillBoss = 5; // Raised from 3 (Step 4) so a run has enough shop visits (4-5) to actually fill the pack and exercise Burn.
    [SerializeField] private string bossSceneName = "BossLevel";
    [SerializeField] private string roguelikeScene2Name = "RoguelikeModeEmpty";

    [Header("Dungeon Timer")]
    [Tooltip("Master switch. When off, no countdown runs and the player is never killed by it.")]
    [SerializeField] private bool dungeonTimerEnabled = false;
    [Tooltip("Seconds the player has to clear a dungeon before dying. Resets on every new dungeon (teleporter -> next dungeon).")]
    [SerializeField] private float dungeonTimeLimit = 120f;

    [Header("Dungeon Timer UI (optional)")]
    [Tooltip("Countdown label, e.g. \"1:23\".")]
    [SerializeField] private TextMeshProUGUI dungeonTimerText;
    [SerializeField] private TextMeshProUGUI dungeonTimerTextShadow;
    [Tooltip("Image set to Image Type: Filled — drains as time runs out.")]
    [SerializeField] private Image dungeonTimerFillImage;
    [Tooltip("Fill/text colour once remaining time drops below the warning threshold.")]
    [SerializeField] private Color dungeonTimerWarningColor = Color.red;
    [SerializeField] private float dungeonTimerWarningThreshold = 15f;

    private float dungeonTimeRemaining = 0f;
    private bool dungeonTimerActive = false;
    private Color dungeonTimerNormalColor = Color.white;

    private int dungeonsClearedCount = 0;
    private int dungeonSizeIncrement = 0;
    private const int BASE_DUNGEON_SIZE = 5;
    private const float SIZE_INCREASE_CHANCE = 0.5f;
    private int currentLevel = 1;

    private WeaponInventory weaponInventory;
    private WeaponAmmoManager AmmoManager;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            if (dungeonTimerText != null)
                dungeonTimerNormalColor = dungeonTimerText.color;
            else if (dungeonTimerFillImage != null)
                dungeonTimerNormalColor = dungeonTimerFillImage.color;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == roguelikeScene2Name)
        {
            Debug.Log("[RoguelikeManager] RoguelikeScene_2 detected — waiting for scene init...");
            StartCoroutine(InitRoguelikeScene2());
        }
        else
        {
            StartCoroutine(ReinitialiseReferences());
        }
    }

    // ✅ NEW — re-grab all scene references on reload
    private IEnumerator ReinitialiseReferences()
    {
        yield return null; // wait one frame for scene to finish loading

        dungeonGenerator = FindFirstObjectByType<DungeonMapGenerator>();
        playerTransform = FindFirstObjectByType<PlayerController>()?.transform;
        weaponInventory = FindFirstObjectByType<WeaponInventory>();
        AmmoManager = FindFirstObjectByType<WeaponAmmoManager>();

        Debug.Log($"[RoguelikeManager] References re-grabbed — dungeonGenerator: {dungeonGenerator}");
        Debug.Log($"[RoguelikeManager] playerTransform: {playerTransform}");

        if (AmmoManager != null && weaponInventory != null)
            AmmoManager.InitialiseAmmo(weaponInventory.GetAllWeapons());
    }

    private IEnumerator InitRoguelikeScene2()
    {
        yield return null;

        dungeonGenerator = FindFirstObjectByType<DungeonMapGenerator>();
        teleporter = FindFirstObjectByType<Teleporter>();
        playerTransform = FindFirstObjectByType<PlayerController>()?.transform;
        weaponInventory = FindFirstObjectByType<WeaponInventory>();
        AmmoManager = FindFirstObjectByType<WeaponAmmoManager>();

        Debug.Log($"[RoguelikeManager] dungeonGenerator: {dungeonGenerator}");
        Debug.Log($"[RoguelikeManager] teleporter: {teleporter}");
        Debug.Log($"[RoguelikeManager] playerTransform: {playerTransform}");

        if (dungeonGenerator == null)
        {
            Debug.LogError("[RoguelikeManager] DungeonMapGenerator not found! Is it in RoguelikeScene_2?");
            yield break;
        }

        GenerateNewDungeon();
        UpdateSpawnerDifficulty();
    }

    private void Start()
    {
        weaponInventory = FindFirstObjectByType<WeaponInventory>();
        AmmoManager = FindFirstObjectByType<WeaponAmmoManager>();
        if (weaponInventory == null)
            Debug.LogError("[RoguelikeManager] WeaponInventory not found in scene!");
        if (AmmoManager == null)
            Debug.LogError("[RoguelikeManager] WeaponAmmoManager not found in scene!");

        bool resumeInStore = SeedFromRun();
        AmmoManager.InitialiseAmmo(weaponInventory.GetAllWeapons());

        // Resumed in the store — including the one before the boss.
        if (resumeInStore)
        {
            StartCoroutine(OpenStoreOnResume());
            return;
        }

        // Resumed past the last dungeon (checkpoint written on leaving the
        // pre-boss store): the "next dungeon" is the boss fight.
        if (dungeonsClearedCount >= roomsTillBoss)
        {
            LoadBossLevel();
            return;
        }

        GenerateNewDungeon();
        UpdateSpawnerDifficulty();
    }

    // Resume (save-system-spec.md §4): the progression counters come from
    // GameSession.CurrentRun. A fresh run's defaults (0 cleared, LevelStart)
    // give the same starting values as before. currentLevel is cleared + 1
    // while in a dungeon and == cleared while in the store after it.
    private bool SeedFromRun()
    {
        if (GameSession.Instance == null) return false;

        RunStats run = GameSession.Instance.CurrentRun;
        bool inStore = run.ResumePoint == RunResumePoint.Store;
        dungeonsClearedCount = run.DungeonsClearedThisRun;
        dungeonSizeIncrement = run.DungeonSizeIncrement;
        currentLevel = inStore ? dungeonsClearedCount : dungeonsClearedCount + 1;

        if (dungeonsClearedCount > 0)
            Debug.Log($"[RoguelikeManager] Resumed run — cleared {dungeonsClearedCount}, level {currentLevel}, size +{dungeonSizeIncrement}, {(inStore ? "in the store" : "fresh dungeon")}.");
        return inStore;
    }

    // Resumed in the store: no dungeon is generated — the store opens as if
    // the previous one was just cleared, and its Continue generates the next.
    // A frame late so Pack/CigPool/CoinManager have rehydrated first.
    private IEnumerator OpenStoreOnResume()
    {
        yield return null;

        if (ShopUIController.Instance != null)
            ShopUIController.Instance.Open();
        else
            ContinueDungeon();
    }

    private int GetCurrentDungeonSize()
    {
        return BASE_DUNGEON_SIZE + dungeonSizeIncrement;
    }

    public void GenerateNewDungeon()
    {
        int dungeonSize = GetCurrentDungeonSize();
        Debug.Log($"[RoguelikeManager] Generating dungeon #{dungeonsClearedCount + 1} (Level {currentLevel})");
        dungeonGenerator.GenerateNewMap(dungeonSize);
        Debug.Log($"[RoguelikeManager] Dungeon generation complete!");

        IsDungeonActive = true;
        StartDungeonTimer();
    }

    /// <summary>
    /// True from GenerateNewDungeon until that dungeon is completed. False in
    /// the store — including a store resumed from a save, where no dungeon was
    /// generated. Teleporter.ActivateTeleporter does nothing while false.
    /// </summary>
    public bool IsDungeonActive { get; private set; }

    #region Dungeon Timer

    private void Update()
    {
        if (!dungeonTimerActive) return;

        dungeonTimeRemaining -= Time.deltaTime;

        if (dungeonTimeRemaining <= 0f)
        {
            dungeonTimeRemaining = 0f;
            dungeonTimerActive = false;
            RefreshTimerUI();
            OnDungeonTimerExpired();
            return;
        }

        RefreshTimerUI();
    }

    private void RefreshTimerUI()
    {
        bool warning = dungeonTimeRemaining <= dungeonTimerWarningThreshold;
        Color c = warning ? dungeonTimerWarningColor : dungeonTimerNormalColor;

        if (dungeonTimerText != null)
        {
            int minutes = Mathf.FloorToInt(dungeonTimeRemaining / 60f);
            int seconds = Mathf.FloorToInt(dungeonTimeRemaining % 60f);
            dungeonTimerText.text = $"{minutes}:{seconds:00}";
            if (dungeonTimerTextShadow != null)
                dungeonTimerTextShadow.text = $"{minutes}:{seconds:00}";
            dungeonTimerText.color = c;
        }

        if (dungeonTimerFillImage != null)
        {
            dungeonTimerFillImage.fillAmount = dungeonTimeLimit > 0f
                ? Mathf.Clamp01(dungeonTimeRemaining / dungeonTimeLimit)
                : 0f;
            dungeonTimerFillImage.color = c;
        }
    }

    /// <summary>Starts / resets the dungeon countdown. Called on every new dungeon.</summary>
    public void StartDungeonTimer()
    {
        if (!dungeonTimerEnabled)
        {
            dungeonTimerActive = false;
            return;
        }

        dungeonTimeRemaining = dungeonTimeLimit;
        dungeonTimerActive = true;
        RefreshTimerUI();
        Debug.Log($"[RoguelikeManager] Dungeon timer started: {dungeonTimeLimit}s");
    }

    public void StopDungeonTimer() => dungeonTimerActive = false;

    public float GetDungeonTimeRemaining() => dungeonTimeRemaining;
    public float GetDungeonTimeLimit() => dungeonTimeLimit;
    public bool IsDungeonTimerActive() => dungeonTimerActive;

    private void OnDungeonTimerExpired()
    {
        Debug.LogWarning("[RoguelikeManager] Dungeon timer EXPIRED — killing the player. " +
                         "(Disable 'Dungeon Timer Enabled' or raise 'Dungeon Time Limit' if this is unwanted.)");

        if (playerTransform == null)
            playerTransform = FindFirstObjectByType<PlayerController>()?.transform;

        PlayerHealth playerHealth = playerTransform != null
            ? playerTransform.GetComponent<PlayerHealth>()
            : FindFirstObjectByType<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.KillPlayer();
        else
            Debug.LogError("[RoguelikeManager] Timer expired but no PlayerHealth found to kill!");
    }

    #endregion

    public void CompleteDungeon()
    {
        if (!IsDungeonActive)
        {
            Debug.LogWarning("[RoguelikeManager] CompleteDungeon() ignored — no dungeon is in progress (store / resumed store).");
            return;
        }
        IsDungeonActive = false;

        // Dungeon cleared via teleporter — freeze the timer until the next
        // dungeon is generated (ContinueDungeon -> GenerateNewDungeon).
        StopDungeonTimer();

        dungeonsClearedCount++;

        // Kept in sync so GameSession.EndRun()'s BestRunDungeonsCleared means
        // something; full RunStats wiring is Phase 6.
        if (GameSession.Instance != null)
            GameSession.Instance.CurrentRun.DungeonsClearedThisRun = dungeonsClearedCount;

        Debug.Log($"[RoguelikeManager] dungeonsClearedCount: {dungeonsClearedCount} / {roomsTillBoss}");
        if (GameSession.Instance != null)
            GameSession.Instance.CurrentRun.CurrentDungeonLevel++;

        Debug.Log($"[RoguelikeManager] Dungeon #{dungeonsClearedCount} completed!");

        ClearCurrentDungeon();

        // Removes anything burned during the shop that just ended, before
        // every store visit (the pre-boss one included).
        LevelBoundary.RaiseLevelEnded();

        // The store opens after every dungeon, the last one included — its
        // Continue (ContinueDungeon) then heads to the boss instead.
        if (ShopUIController.Instance != null)
        {
            // Save point: entering the store (resumes here after a crash / Save & Exit).
            GameManager.Instance.SaveCheckpoint(RunResumePoint.Store);
            ShopUIController.Instance.Open();
        }
        else
            ContinueDungeon(); // no shop wired into this scene yet — don't block play
    }

    private void LoadBossLevel()
    {
        GameManager.Instance.AdvanceToBoss();
    }

    private void ClearCurrentDungeon()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies) Destroy(enemy);
        Debug.Log($"[RoguelikeManager] Cleared {enemies.Length} enemies");

        GameObject[] enemySpawners = GameObject.FindGameObjectsWithTag("EnemySpawner");
        foreach (GameObject spawner in enemySpawners) Destroy(spawner);
        Debug.Log($"[RoguelikeManager] Cleared {enemySpawners.Length} spawners");

        GameObject[] coins = GameObject.FindGameObjectsWithTag("Coin");
        foreach (GameObject coin in coins) Destroy(coin);
        Debug.Log($"[RoguelikeManager] Cleared {coins.Length} coins");
    }

    public void ContinueDungeon()
    {
        PauseController.ResetAll();

        if (dungeonsClearedCount >= roomsTillBoss)
        {
            Debug.Log($"<color=red>[RoguelikeManager] {roomsTillBoss} dungeons cleared — heading to Boss!</color>");

            // Save point: leaving the store. Resuming this checkpoint routes
            // straight back to the boss (see Start).
            if (GameSession.Instance != null)
                GameManager.Instance.SaveCheckpoint(RunResumePoint.LevelStart);

            LoadBossLevel();
            return;
        }

        currentLevel++;
        Debug.Log($"[RoguelikeManager] Level increased to {currentLevel}!");

        float randomRoll = Random.value;
        if (randomRoll <= SIZE_INCREASE_CHANCE)
        {
            dungeonSizeIncrement++;
            Debug.Log($"[RoguelikeManager] Size increased! Difficulty: {dungeonSizeIncrement}");
        }

        // Save point: leaving the store — resumes as a fresh layout of this dungeon.
        if (GameSession.Instance != null)
        {
            GameSession.Instance.CurrentRun.DungeonSizeIncrement = dungeonSizeIncrement;
            GameManager.Instance.SaveCheckpoint(RunResumePoint.LevelStart);
        }

        playerTransform.position = Vector3.zero;
        GenerateNewDungeon();
        UpdateSpawnerDifficulty();
    }

    private void UpdateSpawnerDifficulty()
    {
        EnemySpawner[] spawners = FindObjectsOfType<EnemySpawner>();
        foreach (EnemySpawner spawner in spawners)
            spawner.UpdateDifficultyForLevel(currentLevel);

        Debug.Log($"[RoguelikeManager] {spawners.Length} spawners updated for Level {currentLevel}");
    }

    public void OnLevelStart(int levelNumber)
    {
        EnemySpawner[] spawners = FindObjectsOfType<EnemySpawner>();
        foreach (EnemySpawner spawner in spawners)
            spawner.UpdateDifficultyForLevel(levelNumber);
    }

    [ContextMenu("DEBUG - Skip Current Dungeon")]
    public void DEBUG_SkipDungeon() => CompleteDungeon();

    [ContextMenu("DEBUG - Print Progression Stats")]
    public void DEBUG_PrintStats()
    {
        Debug.Log($"╔════════════ ROGUELIKE PROGRESSION STATS ════════════╗");
        Debug.Log($"║ Current Level:        {currentLevel}");
        Debug.Log($"║ Dungeons Cleared:     {dungeonsClearedCount} / {roomsTillBoss}");
        Debug.Log($"║ Difficulty Increments:{dungeonSizeIncrement}");
        Debug.Log($"║ Current Dungeon Size: {GetCurrentDungeonSize()} nodes");
        Debug.Log($"╚═════════════════════════════════════════════════════╝");
    }

    [ContextMenu("DEBUG - Reset All Progression")]
    public void DEBUG_ResetProgression()
    {
        dungeonsClearedCount = 0;
        dungeonSizeIncrement = 0;
        currentLevel = 1;
        GenerateNewDungeon();
        UpdateSpawnerDifficulty();
        DEBUG_PrintStats();
    }

    /// <summary>True whenever the player has control — false while the shop is open. Burn timers gate on this.</summary>
    public bool GameplayInputEnabled { get; private set; } = true;

    /// <summary>
    /// Single flag movement/shooting/dash/stomp all respect. Routed through
    /// by the upgrade menu's open/close instead of scattered per-script flags.
    /// </summary>
    public void SetGameplayInputEnabled(bool enabled)
    {
        GameplayInputEnabled = enabled;

        if (playerTransform == null) return;

        PlayerController pc = playerTransform.GetComponent<PlayerController>();
        if (pc != null) pc.SetInputEnabled(enabled);

        PlayerConeShooter shooter = playerTransform.GetComponent<PlayerConeShooter>();
        if (shooter != null) shooter.SetInputEnabled(enabled);

        PlayerStompController stomp = playerTransform.GetComponent<PlayerStompController>();
        if (stomp != null) stomp.SetInputEnabled(enabled);
    }

    public int GetDungeonsClearedCount() => dungeonsClearedCount;
    public int GetDungeonSizeIncrement() => dungeonSizeIncrement;
    public int GetCurrentDungeonNodeCount() => GetCurrentDungeonSize();
    public int GetCurrentLevel() => currentLevel;
}