using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Scene Names")]
    public string mainMenuScene = "Main Menu";
    public string gameScene = "RoguelikeMode";
    public string bossScene = "BossArena";
    [Tooltip("Played instead of the first dungeon until the player finishes it once (PersistentStats.TutorialCompleted).")]
    public string tutorialScene = "Tutorial";

    /// <summary>
    /// True while the tutorial was opened from the main menu's Tutorial
    /// button: no run, no slot writes, and its exit leads back to the menu.
    /// Cleared by any load of the menu or the game.
    /// </summary>
    public bool IsTutorialReplay { get; private set; }

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
        IsTutorialReplay = false;
        PauseController.ResetAll();
        SceneManager.LoadScene(mainMenuScene);
    }

    public void LoadGame()
    {
        IsTutorialReplay = false;
        PauseController.ResetAll();
        SceneManager.LoadScene(gameScene);
    }

    public void LoadTutorial()
    {
        PauseController.ResetAll();
        SceneManager.LoadScene(tutorialScene);
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
    // these methods, and so do the save points (save-system-spec.md §4). Lives
    // here rather than on RoguelikeManager because RetryRun/ReturnToMainMenu/
    // FinishRun must be callable from BossArena too, where RoguelikeManager
    // does not exist (it's scene-local to RoguelikeMode).

    /// <summary>
    /// Slot select: new run in `slot` (empty slot → newSlotName is its name;
    /// named slot with no run → null). The slot stays active for the session.
    /// Until the tutorial has been finished once, the run starts in it instead
    /// of the first dungeon (TutorialDirector then calls FinishTutorial).
    /// </summary>
    public void StartNewRunInSlot(int slot, string newSlotName)
    {
        Debug.Log($"[GameManager] StartNewRunInSlot({slot}) called.");
        if (!GameSession.Instance.BeginNewRunInSlot(slot, newSlotName)) return;
        LoadRunStart();
    }

    /// <summary>Where a brand-new run begins: the tutorial until it's been finished once, then the first dungeon.</summary>
    private void LoadRunStart()
    {
        if (!GameSession.Instance.Persistent.TutorialCompleted && !string.IsNullOrEmpty(tutorialScene))
            LoadTutorial();
        else
            LoadGame();
    }

    /// <summary>
    /// Tutorial exit: marks it done in the profile (so it never plays again)
    /// and drops the player into the run's first dungeon. The run itself was
    /// already started and written by StartNewRunInSlot — quitting mid-tutorial
    /// leaves it at LevelStart, and Continue puts it back in the tutorial
    /// (ContinueRunInSlot) since it was never finished. Re-saved here so the coins
    /// picked up in the tutorial survive a quit during the first dungeon.
    /// </summary>
    public void FinishTutorial()
    {
        Debug.Log($"[GameManager] FinishTutorial() called (replay: {IsTutorialReplay}).");
        if (IsTutorialReplay)
        {
            LoadMenu();
            return;
        }

        GameSession.Instance.Persistent.TutorialCompleted = true;
        GameSession.Instance.Save();
        GameSession.Instance.SaveRun(RunResumePoint.LevelStart);
        LoadGame();
    }

    /// <summary>
    /// Slot select / main-menu Continue: resume the run saved in `slot`.
    /// RoguelikeManager picks the resume point up from CurrentRun — the store,
    /// or a fresh layout of the saved dungeon. A run quit mid-tutorial goes
    /// back to the tutorial (see IsRunInTutorial).
    /// </summary>
    public void ContinueRunInSlot(int slot)
    {
        Debug.Log($"[GameManager] ContinueRunInSlot({slot}) called.");
        if (!GameSession.Instance.ResumeRunInSlot(slot)) return;

        if (IsRunInTutorial(GameSession.Instance.CurrentRun)) LoadTutorial();
        else LoadGame();
    }

    /// <summary>
    /// A run only leaves the tutorial through FinishTutorial, which sets
    /// TutorialCompleted — so while that's unset, a run still at the very
    /// start (dungeon 1, nothing cleared, not in a store) was quit in the
    /// tutorial. The progress check keeps a run already deep in the dungeon
    /// (an old save from before the tutorial, or the flag reset by the
    /// Tools ▸ Tutorial menu) resuming where it was.
    /// </summary>
    private bool IsRunInTutorial(RunStats run)
    {
        return !GameSession.Instance.Persistent.TutorialCompleted
            && !string.IsNullOrEmpty(tutorialScene)
            && run.CurrentDungeonLevel <= 1
            && run.DungeonsClearedThisRun == 0
            && run.ResumePoint == RunResumePoint.LevelStart;
    }

    /// <summary>
    /// Tutorial death (TutorialDirector claims it as a run-end interceptor,
    /// so the run never ends and no death screen shows): a fresh run — coins
    /// from the failed attempt don't carry over — and the tutorial again.
    /// Not counted as a run.
    /// </summary>
    public void RestartTutorial()
    {
        Debug.Log($"[GameManager] RestartTutorial() called (replay: {IsTutorialReplay}).");
        GameSession.Instance.StartNewRun(countAsRun: false);
        if (!IsTutorialReplay) GameSession.Instance.SaveRun(RunResumePoint.LevelStart);
        LoadTutorial();
    }

    /// <summary>
    /// Main menu Tutorial button (shown once the tutorial has been finished):
    /// plays it again outside any run. CurrentRun is swapped for a blank one
    /// so the tutorial starts clean — safe, since a slot's run is always
    /// reloaded from disk on Continue — and nothing is written to a slot
    /// while IsTutorialReplay is set. The exit returns to the main menu.
    /// </summary>
    public void ReplayTutorial()
    {
        Debug.Log("[GameManager] ReplayTutorial() called.");
        IsTutorialReplay = true;
        GameSession.Instance.StartNewRun(countAsRun: false);
        LoadTutorial();
    }

    /// <summary>
    /// Death screen Retry: a new run in the same slot — works whether
    /// death happened in RoguelikeMode, BossArena or the tutorial (which it
    /// restarts, since it isn't finished yet). The new run is written
    /// straight away, like any new run.
    /// </summary>
    public void RetryRun()
    {
        Debug.Log("[GameManager] RetryRun() called.");
        GameSession.Instance.StartNewRun();
        GameSession.Instance.SaveRun(RunResumePoint.LevelStart);
        LoadRunStart();
    }

    /// <summary>
    /// Pause-menu Give Up: the run ends as a death (profile updated, slot's
    /// run wiped) and a new run starts in the same slot straight away — no
    /// death screen. Skips the run-end interceptors: a voluntary end isn't
    /// something an extra life should catch.
    /// </summary>
    public void GiveUpRun()
    {
        Debug.Log("[GameManager] GiveUpRun() called.");
        if (IsTutorialReplay)
        {
            RestartTutorial(); // no run to end — and the active slot (if any) must not be wiped
            return;
        }

        GameSession.Instance.EndRun(completed: false);
        RetryRun();
    }

    /// <summary>Exit after a finished run (death / Demo Complete screens).</summary>
    public void ReturnToMainMenu()
    {
        Debug.Log("[GameManager] ReturnToMainMenu() called.");
        GameSession.Instance.EndRun(completed: false); // no-op once FinishRun has run
        LoadMenu();
    }

    /// <summary>No run reset — CurrentRun (coins, upgrades, relics) carries over intact.</summary>
    public void AdvanceToBoss()
    {
        GameSession.Instance.CurrentRun.SelectedBoss = defaultBoss;
        LoadBoss();
    }

    /// <summary>Boss defeated — terminal success path (D2): finish the run, then show Demo Complete.</summary>
    public void CompleteRun()
    {
        Debug.Log("[GameManager] CompleteRun() called.");
        FinishRun(RunEndReason.Victory, () =>
        {
            // Include inactive: the canvas GameObject itself must stay active to
            // be findable at all, but don't depend on that never being toggled
            // off by accident in the editor — only its child Panel should hide.
            DemoCompleteScreen screen = FindFirstObjectByType<DemoCompleteScreen>(FindObjectsInactive.Include);
            if (screen != null)
                screen.Show();
            else
                Debug.LogError("[GameManager] CompleteRun() called but no DemoCompleteScreen found in the scene!");
        });
    }

    // ───────────── SAVE POINTS (save-system-spec.md §4) ─────────────

    /// <summary>Entering the store (Store) / leaving it for the next dungeon (LevelStart).</summary>
    public void SaveCheckpoint(RunResumePoint resumePoint)
    {
        Debug.Log($"[GameManager] SaveCheckpoint({resumePoint}).");
        GameSession.Instance.SaveRun(resumePoint);
    }

    /// <summary>Store → Save & Exit: full write, then the main menu. The run stays active.</summary>
    public void SaveAndExitToMenu()
    {
        Debug.Log("[GameManager] SaveAndExitToMenu() called.");
        GameSession.Instance.SaveRun(RunResumePoint.Store);
        GameSession.Instance.Save();
        LoadMenu();
    }

    /// <summary>
    /// Pause-menu quit (after its warning): no write — the last checkpoint
    /// stands and the run stays active in its slot. Alt-F4 behaves the same.
    /// </summary>
    public void QuitToMainMenu()
    {
        Debug.Log("[GameManager] QuitToMainMenu() called — run kept at its last checkpoint.");
        LoadMenu();
    }

    // ───────────── RUN END (death / victory) ─────────────

    /// <summary>
    /// Extra-life hook (spec §1). An interceptor that returns true takes over
    /// this run end: it may veto it (never call finalize — e.g. revive the
    /// player) or defer it (call finalize later — e.g. after a "use extra
    /// life?" prompt is declined). Return false to let it through.
    /// </summary>
    public delegate bool RunEndInterceptor(RunEndReason reason, Action finalize);

    private readonly List<RunEndInterceptor> _runEndInterceptors = new List<RunEndInterceptor>();

    public void AddRunEndInterceptor(RunEndInterceptor interceptor) => _runEndInterceptors.Add(interceptor);
    public void RemoveRunEndInterceptor(RunEndInterceptor interceptor) => _runEndInterceptors.Remove(interceptor);

    /// <summary>
    /// The single run-end point — death (PlayerHealth) and victory
    /// (CompleteRun) both come through here. Once no interceptor claims it,
    /// the run is finalised (profile, then slot wipe — GameSession.EndRun)
    /// BEFORE onFinalized shows the death / Demo Complete screen, so quitting
    /// on that screen can't keep the run.
    /// </summary>
    public void FinishRun(RunEndReason reason, Action onFinalized)
    {
        Debug.Log($"[GameManager] FinishRun({reason}) called.");

        bool finalized = false;
        void Finalize()
        {
            if (finalized) return;
            finalized = true;
            GameSession.Instance.EndRun(completed: reason == RunEndReason.Victory);
            onFinalized?.Invoke();
        }

        foreach (RunEndInterceptor interceptor in _runEndInterceptors.ToArray())
            if (interceptor(reason, Finalize))
                return;

        Finalize();
    }
}

public enum RunEndReason
{
    Death,
    Victory
}
