using UnityEngine;

public class RelicManager : MonoBehaviour
{
    [Header("Relic Tracking - Current Run Only")]
    [Tooltip("8 relics total (0-7). Tracks which ones have been collected this run.")]
    private bool[] relicsCollectedThisRun = new bool[RelicIds.Count];

    [Header("Duplicate Relic Reward")]
    [SerializeField] private int coinsForDuplicateRelic = 50;
    [Tooltip("How many coins to give when player collects a relic already found in this run")]

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // Tracks how many UNIQUE relics collected this run (0-8)
    private int uniqueRelicsCollectedThisRun = 0;

    // Tracks if the relic achievement has been completed (loaded from AchievementManager)
    private bool isRelicAchievementCompleted = false;

    [Header("UI")]
    [SerializeField] private TMPro.TextMeshProUGUI relicsCollectedText;

    private void Start()
    {
        // Check if relic achievement is already completed
        CheckRelicAchievementStatus();

        // Scene-local — resume this run's relics from GameSession.CurrentRun
        // (empty on a fresh run), same pattern as CoinManager.
        LoadFromRunStats();
    }

    private void LoadFromRunStats()
    {
        relicsCollectedThisRun = new bool[RelicIds.Count];
        uniqueRelicsCollectedThisRun = 0;

        if (GameSession.Instance != null)
        {
            bool[] held = GameSession.Instance.CurrentRun.RelicsHeld;
            for (int i = 0; i < relicsCollectedThisRun.Length && i < held.Length; i++)
            {
                relicsCollectedThisRun[i] = held[i];
                if (held[i]) uniqueRelicsCollectedThisRun++;
            }
        }

        UpdateRelicUI();
    }

    // Mirrors state into GameSession.CurrentRun after every mutation so it
    // survives this object being destroyed and gets saved (run.relics).
    private void SyncToRunStats()
    {
        if (GameSession.Instance == null) return;
        RunStats run = GameSession.Instance.CurrentRun;
        run.RelicsHeld = (bool[])relicsCollectedThisRun.Clone();
        run.UniqueRelicsCollectedThisRun = uniqueRelicsCollectedThisRun;
    }

    /// <summary>
    /// Check if the relic achievement has already been unlocked
    /// </summary>
    private void CheckRelicAchievementStatus()
    {
        if (AchievementManager.Instance != null)
        {
            isRelicAchievementCompleted = AchievementManager.Instance.IsAchievementUnlocked("relic_all_complete");

            if (showDebugInfo)
            {
                Debug.Log($"[RelicManager] Relic achievement status: {(isRelicAchievementCompleted ? "COMPLETED" : "NOT COMPLETED")}");
            }
        }
    }

    /// <summary>
    /// Called when player collects a relic
    /// </summary>
    public void CollectRelic(int relicIndex)
    {
       

        // Validate relic index
        if (relicIndex < 0 || relicIndex >= relicsCollectedThisRun.Length)
        {
            Debug.LogError($"[RelicManager] Invalid relic index: {relicIndex}. Must be 0-7.");
            return;
        }

        // If achievement is already completed, ALL relics just give coins
        if (isRelicAchievementCompleted)
        {
            GiveCoinsForDuplicate(relicIndex);
            return;
        }

        // Check if already collected THIS RUN
        if (relicsCollectedThisRun[relicIndex])
        {
            // DUPLICATE - Give coins instead
            GiveCoinsForDuplicate(relicIndex);
            return;
        }

        // THIS IS A NEW/UNIQUE RELIC THIS RUN
        relicsCollectedThisRun[relicIndex] = true;
        uniqueRelicsCollectedThisRun++;
        SyncToRunStats();

        if (showDebugInfo)
        {
            Debug.Log($"✨ [RelicManager] NEW RELIC {relicIndex} collected! Unique relics this run: {uniqueRelicsCollectedThisRun}/8");
        }

        // Check if all 8 unique relics have been collected THIS RUN
        if (uniqueRelicsCollectedThisRun >= 8)
        {
            OnAllUniqueRelicsCollected();
        }

        // Update UI
        UpdateRelicUI();

    }

    private void UpdateRelicUI()
    {
        // Update UI text if assigned
        if (relicsCollectedText != null)
        {
            relicsCollectedText.text = $"Relics: {uniqueRelicsCollectedThisRun}/8";
        }
    }

    /// <summary>
    /// Give coins when player collects a duplicate relic
    /// </summary>
    private void GiveCoinsForDuplicate(int relicIndex)
    {
        if (showDebugInfo)
        {
            if (isRelicAchievementCompleted)
            {
                Debug.Log($"💰 [RelicManager] Relic {relicIndex} collected (Achievement already complete). Giving {coinsForDuplicateRelic} coins.");
            }
            else
            {
                Debug.Log($"💰 [RelicManager] DUPLICATE RELIC {relicIndex}! Giving {coinsForDuplicateRelic} coins.");
            }
        }

        // TODO: Add coins to player's currency
        // Example: CoinManager.Instance.AddCoins(coinsForDuplicateRelic);

        // BEHAVIOR CHANGE (Phase 4): routed through GameSession instead of a
        // direct PlayerPrefs write. Phase 7 converts this to an event.
        if (GameSession.Instance != null)
            GameSession.Instance.Persistent.TotalCoinsEverCollected += coinsForDuplicateRelic;

        // Also check coin achievements
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.CheckCoinAchievements();
        }
    }

    /// <summary>
    /// Get total number of UNIQUE relics collected THIS RUN
    /// </summary>
    public int GetUniqueRelicsCollectedThisRun()
    {
        return uniqueRelicsCollectedThisRun;
    }

    /// <summary>
    /// Get total number of relics collected THIS RUN (including duplicates)
    /// </summary>
    public int GetTotalRelicsCollectedThisRun()
    {
        int count = 0;
        foreach (bool collected in relicsCollectedThisRun)
        {
            if (collected) count++;
        }
        return count;
    }

    /// <summary>
    /// Check if a specific relic has been collected this run
    /// </summary>
    public bool IsRelicCollectedThisRun(int relicIndex)
    {
        if (relicIndex < 0 || relicIndex >= relicsCollectedThisRun.Length)
        {
            return false;
        }
        return relicsCollectedThisRun[relicIndex];
    }

    /// <summary>
    /// Check if the relic achievement is already completed
    /// </summary>
    public bool IsRelicAchievementCompleted()
    {
        return isRelicAchievementCompleted;
    }

    /// <summary>
    /// Called when all 8 UNIQUE relics are collected in a single run (achievement unlock)
    /// </summary>
    private void OnAllUniqueRelicsCollected()
    {
        Debug.Log($"🎉 [RelicManager] ALL 8 UNIQUE RELICS COLLECTED THIS RUN!");

        // Mark achievement as completed
        isRelicAchievementCompleted = true;

       
        // UNCOMMENT THIS WHEN YOU WANT TO ENABLE THE ACHIEVEMENT:
       
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnRelicCollected(8);
        }
      

        // You can trigger special events here
        // Example: Play special animation, show UI, unlock reward, etc.
    }

    /// <summary>
    /// Reset relics for a new run (call this when starting a new dungeon run)
    /// This is also automatically called when leaving to main menu since the object is destroyed
    /// </summary>
    public void ResetForNewRun()
    {
        for (int i = 0; i < relicsCollectedThisRun.Length; i++)
        {
            relicsCollectedThisRun[i] = false;
        }

        uniqueRelicsCollectedThisRun = 0;
        SyncToRunStats();
        UpdateRelicUI();

        // Re-check achievement status (in case it was just unlocked)
        CheckRelicAchievementStatus();

        if (showDebugInfo)
        {
            Debug.Log("[RelicManager] Relics reset for new run");
        }
    }

    /// <summary>
    /// DEBUG: Print current relic status
    /// </summary>
    public void DEBUG_PrintRelicStatus()
    {
        Debug.Log("========== RELIC STATUS ==========");
        Debug.Log($"Achievement Completed: {(isRelicAchievementCompleted ? "YES" : "NO")}");
        Debug.Log($"Unique Relics Collected THIS RUN: {uniqueRelicsCollectedThisRun}/8");
        Debug.Log("");

        for (int i = 0; i < relicsCollectedThisRun.Length; i++)
        {
            string status = relicsCollectedThisRun[i] ? "✓ COLLECTED" : "✗ NOT COLLECTED";
            Debug.Log($"  Relic {i}: {status}");
        }

        Debug.Log("==================================");
    }
}
