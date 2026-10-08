using UnityEngine;
using System;

/// <summary>
/// Manages the player's coin economy for a single run.
/// 
/// Provides:
///   - OnCoinsChanged event      → CoinUI subscribes to update the HUD
///   - GetCurrentCoins()         → UpgradeButtonUI, StoreManager use this
///   - SpendCoins()              → StoreManager calls this on purchase
///   - HasEnoughCoins()          → PlayerStompController uses this
///   - GetCoinsCollectedThisRun()→ StatTracker uses this for end screen
///   - GetTotalCoinsEverCollected() → PlayerHealth uses this (lifetime stat)
///
/// Scene-local (Phase 6) — reads/writes GameSession.CurrentRun continuously
/// so state survives this object being destroyed on the next scene load.
/// </summary>
public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }

    // Fallback only — a new run's coins come from GameSession (Boot), which
    // seeds CurrentRun. These apply when GameSession is missing (Play
    // pressed directly on this scene) or an old save has no coin data.
    [Header("Fallback starting balance (real value: GameSession in Boot)")]
    [SerializeField] private int startingCoins = 0;

    [Header("Fallback coin cap (real value: GameSession in Boot)")]
    [SerializeField] private int startingMaxCoins = 200;

    // ─────────────────────────────────────────────────────────────────
    //  STATE
    // ─────────────────────────────────────────────────────────────────
    private int _currentCoins;
    private int _coinsCollectedThisRun;
    private int _maxCoins;
    // ─────────────────────────────────────────────────────────────────
    //  EVENTS
    //  Subscribe: CoinManager.Instance.OnCoinsChanged += MyMethod;
    //  Unsubscribe in OnDestroy to avoid memory leaks.
    //  Passes the new coin count so listeners don't need to call Get().
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Fires whenever coins change. Argument = new current balance.</summary>
    public event Action<int> OnCoinsChanged;
    /// <summary>Fires when the coin cap increases (e.g. Coin Purse upgrade).</summary>
    public event Action<int> OnMaxCoinsChanged;
    // ─────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitialiseRun();
    }

    // Scene-local (Phase 6) — reads starting state from GameSession.CurrentRun
    // so a fresh run resets by construction (this object is new) instead of
    // via an external reset call. Falls back to the Inspector starting values
    // only when GameSession isn't present (e.g. play-directly-on-this-scene
    // testing with no Boot bootstrap).
    private void InitialiseRun()
    {
        // GameSession.StartNewRun() seeds a new run's coins and cap, so the run
        // is always the source. MaxCoins == 0 means no coin data at all (no
        // GameSession, or an old save without it) — only then do the
        // Inspector fallback values apply.
        RunStats run = GameSession.Instance != null ? GameSession.Instance.CurrentRun : null;
        if (run != null && run.MaxCoins > 0)
        {
            _currentCoins = run.CurrentCoins;
            _coinsCollectedThisRun = run.CoinsCollectedThisRun;
            _maxCoins = run.MaxCoins;
        }
        else
        {
            _currentCoins = startingCoins;
            _coinsCollectedThisRun = 0;
            _maxCoins = startingMaxCoins;
        }
        SyncToRunStats();
        Debug.Log($"[CoinManager] Initialised — {_currentCoins}/{_maxCoins}");
    }

    // Mirrors state into GameSession.CurrentRun after every mutation so it
    // survives this object being destroyed on the next scene load.
    private void SyncToRunStats()
    {
        if (GameSession.Instance == null) return;
        RunStats run = GameSession.Instance.CurrentRun;
        run.CurrentCoins = _currentCoins;
        run.CoinsCollectedThisRun = _coinsCollectedThisRun;
        run.MaxCoins = _maxCoins;
    }

    // ─────────────────────────────────────────────────────────────────
    //  PUBLIC API — GETTERS
    // ─────────────────────────────────────────────────────────────────

    public int GetCurrentCoins() => _currentCoins;

    /// <summary>Coins collected since the run began (for StatTracker end screen).</summary>
    public int GetCoinsCollectedThisRun() => _coinsCollectedThisRun;

    /// <summary>Lifetime coins across all runs (for PlayerHealth death screen).</summary>
    public int GetTotalCoinsEverCollected() =>
        GameSession.Instance != null ? GameSession.Instance.Persistent.TotalCoinsEverCollected : 0;

    /// <summary>Returns true if the player can afford the given amount.</summary>
    public bool HasEnoughCoins(int amount) => _currentCoins >= amount;

    public int MaxCoins => _maxCoins;

    public int GetMaxCoins() => _maxCoins;


    // ─────────────────────────────────────────────────────────────────
    //  PUBLIC API — MUTATORS
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Add coins (e.g. enemy drop, pickup).
    /// amount must be positive.
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;

        int actual = Mathf.Min(amount, _maxCoins - _currentCoins); // ← clamp to cap
        if (actual <= 0) return;

        _currentCoins += actual;
        _coinsCollectedThisRun += actual;

        if (GameSession.Instance != null)
            GameSession.Instance.Persistent.TotalCoinsEverCollected += actual;

        SyncToRunStats();
        Debug.Log($"[CoinManager] +{actual} coins → {_currentCoins}/{_maxCoins}");
        OnCoinsChanged?.Invoke(_currentCoins);
    }

    /// <summary>
    /// Spend coins (upgrade purchase, stomp cost, etc.).
    /// Returns true if successful, false if not enough coins.
    /// </summary>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0) return true;

        if (!HasEnoughCoins(amount))
        {
            Debug.LogWarning($"[CoinManager] Not enough coins! Have {_currentCoins}, need {amount}");
            return false;
        }

        _currentCoins -= amount;
        SyncToRunStats();
        Debug.Log($"[CoinManager] -{amount} coins → {_currentCoins} remaining");
        OnCoinsChanged?.Invoke(_currentCoins);
        return true;
    }

    /// <summary>
    /// Increase the maximum coin cap. Called by UpgradeManager when a
    /// CoinPurse upgrade is applied.
    /// </summary>
    public void IncreaseMaxCoins(int amount)
    {
        if (amount <= 0) return;
        _maxCoins += amount;
        SyncToRunStats();
        Debug.Log($"[CoinManager] Max coins increased to {_maxCoins}");
        OnMaxCoinsChanged?.Invoke(_maxCoins);
        OnCoinsChanged?.Invoke(_currentCoins); // refresh UI fill bar
    }
}