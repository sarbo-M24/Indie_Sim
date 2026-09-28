using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Scene Names")]
    public string mainMenuScene = "Main Menu";
    public string gameScene = "RoguelikeMode";
    public string bossScene = "BossArena";

    [Header("Boss (D1 — one boss for now)")]
    [SerializeField] private BossDefinition defaultBoss;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadScene(string sceneName)
    {
    PauseController.ResetAll();
    UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
    // ───────────── SCENE LOADS ─────────────

    public void LoadMenu()
    {
        PauseController.ResetAll();
        SceneManager.LoadScene(mainMenuScene);
    }

    public void LoadGame()
    {
        PauseController.ResetAll();
        SceneManager.LoadScene(gameScene);
    }

    public void LoadBoss()
    {
        PauseController.ResetAll();
        SceneManager.LoadScene(bossScene);
    }

    public void ReloadCurrentScene()
    {
        PauseController.ResetAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ───────────── RUN LIFECYCLE FUNNEL (Phase 5, D2) ─────────────
    // Every scene transition that starts, ends, or advances a run goes through
    // exactly one of these five methods. Lives here rather than on
    // RoguelikeManager because RetryRun/ReturnToMainMenu/CompleteRun must be
    // callable from BossArena too, where RoguelikeManager does not exist
    // (it's scene-local to RoguelikeMode).

    public void StartNewRun()
    {
        Debug.Log("[GameManager] StartNewRun() called.");
        GameSession.Instance.StartNewRun();
        LoadGame();
    }

    /// <summary>
    /// Identical to StartNewRun() — works whether death happened in
    /// RoguelikeMode or BossArena, since both route into this one method.
    /// </summary>
    public void RetryRun()
    {
        Debug.Log("[GameManager] RetryRun() called.");
        StartNewRun();
    }

    public void ReturnToMainMenu()
    {
        Debug.Log("[GameManager] ReturnToMainMenu() called.");
        GameSession.Instance.EndRun(completed: false);
        LoadMenu();
    }

    /// <summary>No run reset — CurrentRun (coins, upgrades, relics) carries over intact.</summary>
    public void AdvanceToBoss()
    {
        GameSession.Instance.CurrentRun.SelectedBoss = defaultBoss;
        LoadBoss();
    }

    /// <summary>
    /// Terminal success path (D2). Does not load a scene — shows the Demo
    /// Complete screen; its only exit calls ReturnToMainMenu().
    /// </summary>
    public void CompleteRun()
    {
        Debug.Log("[GameManager] CompleteRun() called.");
        GameSession.Instance.EndRun(completed: true);

        // Include inactive: the canvas GameObject itself must stay active to
        // be findable at all, but don't depend on that never being toggled
        // off by accident in the editor — only its child Panel should hide.
        DemoCompleteScreen screen = FindFirstObjectByType<DemoCompleteScreen>(FindObjectsInactive.Include);
        if (screen != null)
            screen.Show();
        else
            Debug.LogError("[GameManager] CompleteRun() called but no DemoCompleteScreen found in the scene!");
    }
}