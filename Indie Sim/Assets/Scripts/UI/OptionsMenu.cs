using UnityEngine;

public class OptionsMenu : MonoBehaviour
{
    [Header("Menu References")]
    [SerializeField] private GameObject optionsPanel; // The options menu UI panel
    [SerializeField] private GameObject pauseOverlay; // Optional dark overlay when paused
    [SerializeField] private SettingsPanel settingsPanel; // Opened by the pause menu's Settings button
    [SerializeField] private ControlsPanel controlsPanel; // Opened by the pause menu's Controls button
    [Tooltip("Shown by Exit to Main Menu: progress since the last checkpoint is lost. Built by Tools/Save/Build Save & Exit + Quit Warning.")]
    [SerializeField] private ConfirmDialog quitWarning;

    [Header("Scene Management")]
    [SerializeField] private string mainMenuSceneName = "MainMenu"; // Name of your main menu scene

    [Header("UI Elements")]
    [SerializeField] private UnityEngine.UI.Button soundToggleButton; // Optional: to update button text/image
    [SerializeField] private UnityEngine.UI.Text soundButtonText; // Optional: to show "Sound: ON/OFF"

    public string MainMenu = "Main Menu";
    // Private variables
    private bool isGamePaused = false;
    private float volumeBeforeMute = 1f;

    // Events for extensibility
    public System.Action OnGamePaused;
    public System.Action OnGameResumed;
    public System.Action OnSoundToggled;
    public System.Action OnExitToMainMenu;

    private void Start()
    {
        InitializeOptionsMenu();
    }

    private void InitializeOptionsMenu()
    {
        // Make sure options panel is hidden at start
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);

            // Added while hidden so its OnEnable (input block, cursor, focus) first runs on pause.
            if (!optionsPanel.TryGetComponent(out GamepadMenuPanel _))
                optionsPanel.AddComponent<GamepadMenuPanel>();
        }

        if (pauseOverlay != null)
        {
            pauseOverlay.SetActive(false);
        }

        if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);
        if (controlsPanel != null) controlsPanel.gameObject.SetActive(false);
        if (quitWarning != null) quitWarning.Close();

        // Update UI elements
        UpdateSoundButtonDisplay();

        Debug.Log("Options Menu initialized");
    }

    #region Public Methods for UI Buttons

    /// <summary>
    /// Call this method when the options button is pressed
    /// </summary>
    public void ToggleOptionsMenu()
    {
        if (isGamePaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    /// <summary>
    /// Pause the game and show options menu
    /// </summary>
    public void PauseGame()
    {
        if (isGamePaused) return;

        isGamePaused = true;
        PauseController.SetFrozen(this, true);

        // Show options menu
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
        }

        if (pauseOverlay != null)
        {
            pauseOverlay.SetActive(true);
        }

        // Trigger event for other systems
        OnGamePaused?.Invoke();

        Debug.Log("Game Paused - Options Menu Opened");
    }

    /// <summary>
    /// Resume the game and hide options menu
    /// </summary>
    public void ResumeGame()
    {
        if (!isGamePaused) return;

        isGamePaused = false;
        PauseController.SetFrozen(this, false);
        if (quitWarning != null) quitWarning.Close();
        // Hide options menu
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }

        if (pauseOverlay != null)
        {
            pauseOverlay.SetActive(false);
        }

        // Trigger event for other systems
        OnGameResumed?.Invoke();

        Debug.Log("Game Resumed - Options Menu Closed");
    }

    /// <summary>Wire to the pause menu's Settings button.</summary>
    public void OpenSettings() => OpenSubMenu(settingsPanel);

    /// <summary>Wire to the pause menu's Controls button.</summary>
    public void OpenControls() => OpenSubMenu(controlsPanel);

    // Hides the pause buttons while the sub-menu is open and brings them back
    // (focus on the button that opened it) when it closes.
    private void OpenSubMenu(TabbedMenuPanel panel)
    {
        if (panel == null || !isGamePaused) return;

        UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
        GameObject openedFrom = eventSystem != null ? eventSystem.currentSelectedGameObject : null;

        panel.Open(() =>
        {
            if (optionsPanel != null) optionsPanel.SetActive(true);
            if (openedFrom != null && eventSystem != null && InputManager.UsingGamepad)
                eventSystem.SetSelectedGameObject(openedFrom);
        });

        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    /// <summary>
    /// Toggle sound on/off. Mutes via the Master volume setting (AudioManager
    /// applies it); stands in until the settings panel's volume sliders exist.
    /// </summary>
    public void ToggleSound()
    {
        bool mute = IsSoundEnabled();
        if (mute) volumeBeforeMute = SettingsService.Current.masterVolume;
        SettingsService.Change(s => s.masterVolume = mute ? 0f : Mathf.Max(volumeBeforeMute, 0.05f));
        SettingsService.Save();

        // Update UI
        UpdateSoundButtonDisplay();

        // Trigger event
        OnSoundToggled?.Invoke();

        Debug.Log($"Sound {(IsSoundEnabled() ? "Enabled" : "Disabled")}");
    }

    /// <summary>
    /// Exit to main menu — after a warning, since a mid-level quit saves
    /// nothing: the run resumes from its last checkpoint (save-system-spec.md §4).
    /// </summary>
    public void ExitToMainMenu()
    {
        if (quitWarning == null)
        {
            ConfirmedExitToMainMenu();
            return;
        }

        quitWarning.Open("Quit to the main menu?\nProgress since your last checkpoint will be lost.", ConfirmedExitToMainMenu);
    }

    private void ConfirmedExitToMainMenu()
    {
        Debug.Log("Exiting to Main Menu...");

        // Trigger event before exiting
        OnExitToMainMenu?.Invoke();

        // Resume time before changing scenes
        PauseController.ResetAll();

        // Not a run end: no write, the run stays active at its last checkpoint.
        GameManager.Instance.QuitToMainMenu();
    }

    #endregion

    #region Audio Management

    private void UpdateSoundButtonDisplay()
    {
        bool isSoundEnabled = IsSoundEnabled();

        if (soundButtonText != null)
        {
            soundButtonText.text = $"Sound: {(isSoundEnabled ? "ON" : "OFF")}";
        }

        // Optional: Change button color or sprite based on sound state
        if (soundToggleButton != null)
        {
            var colors = soundToggleButton.colors;
            colors.normalColor = isSoundEnabled ? Color.green : Color.red;
            soundToggleButton.colors = colors;
        }
    }

    #endregion

    #region Public Getters (for other systems to check state)

    public bool IsGamePaused() { return isGamePaused; }
    public bool IsSoundEnabled() { return SettingsService.Current.masterVolume > 0f; }

    #endregion

    #region Future Extension Methods

    /// <summary>
    /// Add custom settings here - example method for future features
    /// </summary>
    public void SetCustomSetting(string settingName, bool value)
    {
        PlayerPrefs.SetInt(settingName, value ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"Custom setting '{settingName}' set to: {value}");
    }

    /// <summary>
    /// Get custom settings - example method for future features
    /// </summary>
    public bool GetCustomSetting(string settingName, bool defaultValue = false)
    {
        return PlayerPrefs.GetInt(settingName, defaultValue ? 1 : 0) == 1;
    }

    #endregion

    #region Input Handling

    // Pause action (Esc / Start) lives in the always-on UI map, so it also resumes.
    // Input blocking, cursor and gamepad focus while open are the panel's
    // GamepadMenuPanel's job (added in InitializeOptionsMenu).
    private void OnEnable()
    {
        InputManager.Controls.UI.Pause.performed += OnPausePressed;
        InputManager.Controls.UI.Cancel.performed += OnCancelPressed;
    }

    private void OnDisable()
    {
        InputManager.Controls.UI.Pause.performed -= OnPausePressed;
        InputManager.Controls.UI.Cancel.performed -= OnCancelPressed;

        // Destroyed while paused (scene change) — don't leave the freeze behind.
        PauseController.SetFrozen(this, false);
    }

    private void OnPausePressed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        // Something else already froze the game (death screen, countdown, demo
        // complete) — the pause menu would just sit on top of it.
        if (!isGamePaused && PauseController.IsFrozen) return;

        // This press is closing Settings/Controls (back to the pause menu), not resuming.
        if (TabbedMenuPanel.BlocksPauseInput) return;

        // Esc / Start with the quit warning up backs out of it, not the pause
        // menu. Esc is also Cancel, which may have closed it first this frame.
        if (quitWarning != null && (quitWarning.IsOpen || quitWarning.ClosedFrame == Time.frameCount))
        {
            if (quitWarning.IsOpen) quitWarning.Answer(false);
            return;
        }

        ToggleOptionsMenu();
    }

    // B / Esc steps back one level: quit warning → pause menu → gameplay.
    // Settings/Controls close themselves on Cancel (back to the pause menu).
    private void OnCancelPressed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (!isGamePaused) return;

        if (quitWarning != null && (quitWarning.IsOpen || quitWarning.ClosedFrame == Time.frameCount))
        {
            if (quitWarning.IsOpen) quitWarning.Answer(false);
            return;
        }

        // This press is closing Settings/Controls (back to the pause menu), not resuming.
        if (TabbedMenuPanel.BlocksPauseInput) return;

        // Esc is both Cancel and Pause; the Pause handler already toggles, so
        // resuming here too would let it re-pause on the same press.
        if (IsPauseControl(ctx.control)) return;

        ResumeGame();
    }

    private static bool IsPauseControl(UnityEngine.InputSystem.InputControl control)
    {
        foreach (UnityEngine.InputSystem.InputControl pauseControl in InputManager.Controls.UI.Pause.controls)
            if (pauseControl == control) return true;
        return false;
    }

    #endregion
}
