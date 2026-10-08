using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Scene-local, lives in BossArena. Shown by GameManager.CompleteRun() when
/// the boss is defeated (D2 terminal success path). Its only exit returns to
/// Main Menu — there is no loop back to RoguelikeMode. Layout (title, the
/// thanks/wishlist message, stats, main-menu-style button) is built by
/// Tools/UI/Restyle Dialogs + Demo Complete (DialogStyleBuilder).
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
        {
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            // The only button: D-pad/stick must not move focus to HUD or
            // death-screen buttons behind the panel.
            mainMenuButton.navigation = new Navigation { mode = Navigation.Mode.None };
            if (!mainMenuButton.TryGetComponent(out ButtonFocusScale _))
                mainMenuButton.gameObject.AddComponent<ButtonFocusScale>();
        }

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

        // Pad players need something selected for A to press.
        if (mainMenuButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(mainMenuButton.gameObject);
    }

    // Belt and braces: while the screen is up, the pad's focus can't be lost
    // (a mouse click on empty space deselects, for instance).
    private void Update()
    {
        if (panel == null || !panel.activeInHierarchy || mainMenuButton == null) return;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && InputManager.UsingGamepad && eventSystem.currentSelectedGameObject != mainMenuButton.gameObject)
            eventSystem.SetSelectedGameObject(mainMenuButton.gameObject);
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
