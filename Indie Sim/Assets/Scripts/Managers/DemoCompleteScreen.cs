using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Scene-local, lives in BossArena. Shown by GameManager.CompleteRun() when
/// the boss is defeated (D2 terminal success path). Its only exit returns to
/// Main Menu — there is no loop back to RoguelikeMode. Kept plain (a state,
/// not a feature) per the plan; polish later.
/// </summary>
public class DemoCompleteScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private Button mainMenuButton;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);

        Debug.Log($"[DemoCompleteScreen] Awake() on {gameObject.name}. panel: {(panel != null ? panel.name : "NULL")}, mainMenuButton: {(mainMenuButton != null ? mainMenuButton.name : "NULL")}");
    }

    /// <summary>
    /// Call after GameSession.EndRun() has already saved, so lifetime stats
    /// shown here are accurate.
    /// </summary>
    public void Show()
    {
        if (summaryText != null)
            summaryText.text = BuildSummary();

        if (panel != null)
            panel.SetActive(true);

        // BossArena is a gameplay scene, so CursorController's per-scene
        // SceneUIMode has the cursor hidden/confined — no scene change
        // happens here to flip that, so without this the button is
        // unclickable-by-eye (cursor invisible) even though it's technically
        // interactable.
        if (CursorController.Instance != null)
            CursorController.Instance.SetCursorOverride(true);

        PauseController.SetFrozen(this, true);
    }

    // Reads via the managers' own Instance singletons, same as StatTracker's
    // death-screen summary. Scene-local since Phase 6, but Instance still
    // resolves correctly — it's just reset fresh per scene now instead of
    // persisting. GameSession.CurrentRun holds the same values underneath.
    private string BuildSummary()
    {
        int kills = EnemyKillTracker.Instance != null ? EnemyKillTracker.Instance.GetKillsThisRun() : 0;
        int coins = CoinManager.Instance != null ? CoinManager.Instance.GetCoinsCollectedThisRun() : 0;
        int dungeonsCleared = GameSession.Instance != null ? GameSession.Instance.CurrentRun.DungeonsClearedThisRun : 0;

        return $"Kills: {kills}\nCoins: {coins}\nDungeons Cleared: {dungeonsCleared}";
    }

    private void OnMainMenuClicked()
    {
        Debug.Log($"[DemoCompleteScreen] OnMainMenuClicked() fired. GameManager.Instance: {(GameManager.Instance != null)}");
        PauseController.ResetAll();
        GameManager.Instance.ReturnToMainMenu();
    }
}
