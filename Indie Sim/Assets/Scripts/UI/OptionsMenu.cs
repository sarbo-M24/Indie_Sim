using UnityEngine;

public class OptionsMenu : MonoBehaviour
{
    [Header("Menu References")]
    [SerializeField] private GameObject optionsPanel; // The options menu UI panel
    [SerializeField] private GameObject pauseOverlay; // Optional dark overlay when paused
    [SerializeField] private SettingsPanel settingsPanel; // Opened by the pause menu's Settings button

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
    public System.Action OnRetryLevel;
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

    /// <summary>
    /// Wire to the pause menu's Settings button. Hides the pause buttons
    /// while Settings is open and brings them back (focus on Settings) after.
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel == null || !isGamePaused) return;

        UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
        GameObject settingsButton = eventSystem != null ? eventSystem.currentSelectedGameObject : null;

        settingsPanel.Open(() =>
        {
            if (optionsPanel != null) optionsPanel.SetActive(true);
            if (settingsButton != null && eventSystem != null && InputManager.UsingGamepad)
                eventSystem.SetSelectedGameObject(settingsButton);
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
    /// Restart the current level/scene
    /// </summary>
    public void RetryLevel()
    {
        Debug.Log("Retrying Level...");

        // Trigger event before reloading
        OnRetryLevel?.Invoke();

        // Resume time before reloading scene
        PauseController.ResetAll();

        // The dungeon clears in place — there's no separate "level" scene to
        // reload, so restarting the level means restarting the run (D2).
        GameManager.Instance.RetryRun();
    }

    /// <summary>
    /// Exit to main menu
    /// </summary>
    public void ExitToMainMenu()
    {
        Debug.Log("Exiting to Main Menu...");

        // Trigger event before exiting
        OnExitToMainMenu?.Invoke();

        // Resume time before changing scenes
        PauseController.ResetAll();

        // Return to main menu — funnels through ReturnToMainMenu() so the
        // abandoned run is folded into lifetime stats (D2 abort path).
        GameManager.Instance.ReturnToMainMenu();
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
    private void OnEnable() => InputManager.Controls.UI.Pause.performed += OnPausePressed;

    private void OnDisable()
    {
        InputManager.Controls.UI.Pause.performed -= OnPausePressed;

        // Destroyed while paused (scene change) — don't leave the freeze behind.
        PauseController.SetFrozen(this, false);
    }

    private void OnPausePressed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        // Something else already froze the game (death screen, countdown, demo
        // complete) — the pause menu would just sit on top of it.
        if (!isGamePaused && PauseController.IsFrozen) return;

        // This press is closing Settings (back to the pause menu), not resuming.
        if (SettingsPanel.BlocksPauseInput) return;

        ToggleOptionsMenu();
    }

    #endregion
}
