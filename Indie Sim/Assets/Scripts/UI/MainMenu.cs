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

    private void Start()
    {
        if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);
        if (controlsPanel != null) controlsPanel.gameObject.SetActive(false);

        if (menuButtons != null && !menuButtons.TryGetComponent(out GamepadMenuPanel _))
            menuButtons.AddComponent<GamepadMenuPanel>();
    }

    public void LoadScene1 ()
    {
        // "Play" — funnels through StartNewRun() so GameSession actually
        // resets for the new run (it previously didn't at all).
        GameManager.Instance.StartNewRun();
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
