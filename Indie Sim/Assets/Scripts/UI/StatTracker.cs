using UnityEngine;
using TMPro;

/// <summary>
/// Tracks and displays end-run stats (kills + coins) on the death/victory panel.
/// Attach to a persistent GameObject or the Player.
/// </summary>
public class StatTracker : MonoBehaviour
{
    [Header("Stat UI References")]
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text coinsText;

   

    [Header("Script References")]
    [SerializeField] private EnemyKillTracker killTracker; // drag your kill tracker here
    [SerializeField] private CoinManager coinManager;      // drag your coin manager here

    public static StatTracker Instance;
    private void Awake()
    {

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Auto-find if not assigned
        if (killTracker == null)
            killTracker = FindObjectOfType<EnemyKillTracker>();

        if (coinManager == null)
            coinManager = FindObjectOfType<CoinManager>();

       
    }

    /// <summary>
    /// Call this when the player dies — pulls current values and shows panel
    /// </summary>
    public void ShowDeathStats()
    {
       

        // ✅ Pull values at the moment of death
        int totalKills = killTracker != null ? killTracker.GetKillsThisRun() : 0;
        int totalCoins = coinManager != null ? coinManager.GetCoinsCollectedThisRun() : 0;

        if (killsText != null)
            killsText.text = $"Kills : {totalKills}";

        if (coinsText != null)
            coinsText.text = $"Coins : {totalCoins}";

        // No freeze here: this runs the moment the player dies, before
        // PlayerHealth's (scaled-time) deathDelay — freezing now would stop
        // that delay forever. PlayerHealth freezes when the death panel shows.

        Debug.Log($"[StatTracker] Death stats — Kills: {totalKills} | Coins: {totalCoins}");
    }
}
