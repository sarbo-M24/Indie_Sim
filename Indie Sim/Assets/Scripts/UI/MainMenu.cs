using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    
    [Header("Scene Names")]
    [SerializeField] private string Scene1 = "CasualMode";
    [SerializeField] private string Scene2 = "HardcoreMode";

    [Header("Settings")]
    [Tooltip("The object holding the menu's buttons. Gets gamepad navigation, and hides while Settings is open.")]
    [SerializeField] private GameObject menuButtons;
    [SerializeField] private SettingsPanel settingsPanel;

    private void Start()
    {
        if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);

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
    public void OpenSettings()
    {
        if (settingsPanel == null) return;

        EventSystem eventSystem = EventSystem.current;
        GameObject settingsButton = eventSystem != null ? eventSystem.currentSelectedGameObject : null;

        settingsPanel.Open(() =>
        {
            if (menuButtons != null) menuButtons.SetActive(true);
            if (settingsButton != null && eventSystem != null && InputManager.UsingGamepad)
                eventSystem.SetSelectedGameObject(settingsButton);
        });

        if (menuButtons != null) menuButtons.SetActive(false);
    }

}
