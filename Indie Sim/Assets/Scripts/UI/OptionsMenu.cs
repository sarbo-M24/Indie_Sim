using UnityEngine;
using UnityEngine.Audio;

public class OptionsMenu : MonoBehaviour
{
    [Header("Menu References")]
    [SerializeField] private GameObject optionsPanel; // The options menu UI panel
    [SerializeField] private GameObject pauseOverlay; // Optional dark overlay when paused

    [Header("Audio Settings")]
    [SerializeField] private AudioMixerGroup masterAudioMixer; // Optional: for advanced audio control
    [SerializeField] private AudioSource[] allAudioSources; // Array of audio sources to control

    [Header("Scene Management")]
    [SerializeField] private string mainMenuSceneName = "MainMenu"; // Name of your main menu scene

    [Header("UI Elements")]
    [SerializeField] private UnityEngine.UI.Button soundToggleButton; // Optional: to update button text/image
    [SerializeField] private UnityEngine.UI.Text soundButtonText; // Optional: to show "Sound: ON/OFF"

    public string MainMenu = "Main Menu";
    // Private variables
    private bool isGamePaused = false;
    private bool isSoundEnabled = true;
    private float originalTimeScale = 1f;

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
        // Store original time scale
        originalTimeScale = Time.timeScale;

        // Load saved sound preference
        isSoundEnabled = PlayerPrefs.GetInt("SoundEnabled", 1) == 1;
        ApplySoundSettings();

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
        Time.timeScale = 0f; // Pause the game

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
        Time.timeScale = 1f; // Resume the game
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
    /// Toggle sound on/off
    /// </summary>
    public void ToggleSound()
    {
        isSoundEnabled = !isSoundEnabled;

        // Save preference
        PlayerPrefs.SetInt("SoundEnabled", isSoundEnabled ? 1 : 0);
        PlayerPrefs.Save();

        // Apply sound settings
        ApplySoundSettings();

        // Update UI
        UpdateSoundButtonDisplay();

        // Trigger event
        OnSoundToggled?.Invoke();

        Debug.Log($"Sound {(isSoundEnabled ? "Enabled" : "Disabled")}");
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
        Time.timeScale = originalTimeScale;

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
        Time.timeScale = originalTimeScale;

        // Return to main menu — funnels through ReturnToMainMenu() so the
        // abandoned run is folded into lifetime stats (D2 abort path).
        GameManager.Instance.ReturnToMainMenu();
    }

    #endregion

    #region Audio Management

    private void ApplySoundSettings()
    {
        float volumeLevel = isSoundEnabled ? 1f : 0f;

        // Method 1: Using Audio Mixer (Recommended)
        if (masterAudioMixer != null)
        {
            masterAudioMixer.audioMixer.SetFloat("MasterVolume", isSoundEnabled ? 0f : -80f);
        }

        // Method 2: Individual Audio Sources (Fallback)
        if (allAudioSources != null && allAudioSources.Length > 0)
        {
            foreach (AudioSource audioSource in allAudioSources)
            {
                if (audioSource != null)
                {
                    audioSource.mute = !isSoundEnabled;
                }
            }
        }

        // Method 3: Global Audio Listener (Simple but affects everything)
        AudioListener.volume = volumeLevel;
    }

    private void UpdateSoundButtonDisplay()
    {
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
    public bool IsSoundEnabled() { return isSoundEnabled; }

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
    private void OnDisable() => InputManager.Controls.UI.Pause.performed -= OnPausePressed;

    private void OnPausePressed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        // Something else already froze the game (death screen, demo complete) —
        // pausing over it and resuming would set timeScale back to 1 under it.
        if (!isGamePaused && Time.timeScale == 0f) return;

        ToggleOptionsMenu();
    }

    #endregion
}
