using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    
    [Header("Scene Names")]
    [SerializeField] private string Scene1 = "CasualMode";
    [SerializeField] private string Scene2 = "HardcoreMode";

    [Header("Settings / Controls")]
    [Tooltip("The object holding the menu's buttons. Gets gamepad navigation, and hides while Settings or Controls is open.")]
    [SerializeField] private GameObject menuButtons;
    [SerializeField] private SettingsPanel settingsPanel;
    [SerializeField] private ControlsPanel controlsPanel;

    [Header("Save Slots")]
    [Tooltip("Opened by Start (LoadScene1). Built by Tools/Save/Build Slot Select UI.")]
    [SerializeField] private SlotSelectPanel slotSelectPanel;
    [Tooltip("Resumes the most recently played slot with an active run. Hidden when there is none.")]
    [SerializeField] private GameObject continueButton;

    [Header("Tutorial")]
    [Tooltip("Replays the tutorial (back to this menu at the end). Hidden until it has been finished once.")]
    [SerializeField] private GameObject tutorialButton;

    private void Start()
    {
        if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);
        if (controlsPanel != null) controlsPanel.gameObject.SetActive(false);
        if (slotSelectPanel != null) slotSelectPanel.gameObject.SetActive(false);

        if (continueButton != null) continueButton.SetActive(false);
        if (tutorialButton != null) tutorialButton.SetActive(false);
        StartCoroutine(RefreshContinueWhenReady());

        if (menuButtons != null && !menuButtons.TryGetComponent(out GamepadMenuPanel _))
            menuButtons.AddComponent<GamepadMenuPanel>();
    }

    /// <summary>"Start" — opens slot select; the chosen slot starts or resumes the run.</summary>
    public void LoadScene1 ()
    {
        if (slotSelectPanel == null)
        {
            Debug.LogError("[MainMenu] No SlotSelectPanel assigned — run Tools/Save/Build Slot Select UI.");
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        GameObject openedFrom = eventSystem != null ? eventSystem.currentSelectedGameObject : null;

        slotSelectPanel.Open(() =>
        {
            if (menuButtons != null) menuButtons.SetActive(true);
            RefreshContinue(); // a slot may have been deleted
            if (openedFrom != null && eventSystem != null && InputManager.UsingGamepad)
                eventSystem.SetSelectedGameObject(openedFrom.activeInHierarchy ? openedFrom : null);
        });

        if (menuButtons != null) menuButtons.SetActive(false);
    }

    /// <summary>"Continue" — resumes the most recently written slot with an active run, skipping slot select.</summary>
    public void ContinueRun()
    {
        int slot = GameSession.Instance != null ? GameSession.Instance.Saves.FindContinueSlot() : SaveService.NoSlot;
        if (slot == SaveService.NoSlot)
        {
            RefreshContinue();
            return;
        }
        GameManager.Instance.ContinueRunInSlot(slot);
    }

    // GameSession can arrive a frame late when this scene is played directly
    // in the editor (SceneBootstrapGuard), so wait for it.
    private IEnumerator RefreshContinueWhenReady()
    {
        while (GameSession.Instance == null) yield return null;
        RefreshContinue();
        if (tutorialButton != null) tutorialButton.SetActive(GameSession.Instance.Persistent.TutorialCompleted);
    }

    private void RefreshContinue()
    {
        if (continueButton == null || GameSession.Instance == null) return;
        continueButton.SetActive(GameSession.Instance.Saves.FindContinueSlot() != SaveService.NoSlot);
    }

    /// <summary>Wire to the main menu's Tutorial button.</summary>
    public void PlayTutorial()
    {
        GameManager.Instance.ReplayTutorial();
    }

    /// <summary>
    /// Wire to the main menu's Quit button — the game's only exit besides
    /// Alt-F4. Nothing to save here: runs save at their checkpoints, the
    /// profile on run end and settings on change.
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("[MainMenu] Quit.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void LoadScene2 ()
    {
        GameManager.Instance.LoadScene(Scene2);
    }

    /// <summary>Wire to the main menu's Settings button.</summary>
    public void OpenSettings() => OpenSubMenu(settingsPanel);

    /// <summary>Wire to the main menu's Controls button.</summary>
    public void OpenControls() => OpenSubMenu(controlsPanel);

    // Hides the menu buttons while the sub-menu is open and brings them back
    // (focus on the button that opened it) when it closes.
    private void OpenSubMenu(TabbedMenuPanel panel)
    {
        if (panel == null) return;

        EventSystem eventSystem = EventSystem.current;
        GameObject openedFrom = eventSystem != null ? eventSystem.currentSelectedGameObject : null;

        panel.Open(() =>
        {
            if (menuButtons != null) menuButtons.SetActive(true);
            if (openedFrom != null && eventSystem != null && InputManager.UsingGamepad)
                eventSystem.SetSelectedGameObject(openedFrom);
        });

        if (menuButtons != null) menuButtons.SetActive(false);
    }

}
