using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The instruction panel that will be shown during the tutorial")]
    public GameObject instructionPanel;

    [Tooltip("The button that allows the player to skip the timer")]
    public Button skipButton;

    [Tooltip("The TextMeshPro text that displays the countdown timer")]
    public TextMeshProUGUI countdownText;

    [Header("Player Scripts to Disable")]
    [Tooltip("Add all player scripts that should be disabled during the tutorial")]
    public MonoBehaviour[] playerScripts;

    [Header("Tutorial Settings")]
    [Tooltip("Duration of the tutorial in seconds")]
    public float tutorialDuration = 5f;

    private bool tutorialSkipped = false;
    private Coroutine countdownCoroutine; // Track the coroutine

    void Start()
    {
        // Resumed into the store: the shop opens straight away and the popup
        // would only freeze time and grab pad focus behind it.
        if (GameSession.Instance != null && GameSession.Instance.CurrentRun.ResumePoint == RunResumePoint.Store)
        {
            if (instructionPanel != null) instructionPanel.SetActive(false);
            return;
        }

        // Start the tutorial when the scene loads
        StartTutorial();
    }

    public void StartTutorial()
    {
        // Reset the skipped flag
        tutorialSkipped = false;

        // Stop any existing countdown coroutine
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
        }

        // Pause the game
        PauseController.SetFrozen(this, true);

        // Enable the instruction panel
        if (instructionPanel != null)
        {
            instructionPanel.SetActive(true);

            // Gamepad focus on Skip, visible cursor, player input off while it shows.
            if (!instructionPanel.TryGetComponent(out GamepadMenuPanel _))
                instructionPanel.AddComponent<GamepadMenuPanel>();
        }

        // Disable all player scripts
        DisablePlayerScripts();

        // Set up the skip button
        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(SkipTutorial);
        }

        // Start the countdown timer
        countdownCoroutine = StartCoroutine(TutorialCountdown());
    }

    IEnumerator TutorialCountdown()
    {
        float timeRemaining = tutorialDuration;

        // Use unscaled time since the game is paused
        while (timeRemaining > 0 && !tutorialSkipped)
        {
            // Update the countdown text display
            UpdateCountdownDisplay(timeRemaining);

            // Decrease time using unscaled delta time
            timeRemaining -= Time.unscaledDeltaTime;

            yield return null;
        }

        // Set the timer to 0 when finished
        if (countdownText != null)
        {
            countdownText.text = "0";
        }

        // End tutorial if timer runs out
        if (!tutorialSkipped)
        {
            EndTutorial();
        }
    }

    void UpdateCountdownDisplay(float timeToDisplay)
    {
        if (countdownText != null)
        {
            // Round up the time so it shows 5, 4, 3, 2, 1 instead of 4.9, 3.9, etc.
            int displayTime = Mathf.CeilToInt(timeToDisplay);

            // Update the text
            countdownText.text = displayTime.ToString();
        }
    }

    void SkipTutorial()
    {
        tutorialSkipped = true;
        EndTutorial();
    }

    void EndTutorial()
    {
        // Disable the instruction panel
        if (instructionPanel != null)
        {
            instructionPanel.SetActive(false);
        }

        // Enable all player scripts
        EnablePlayerScripts();

        // Unpause the game
        PauseController.SetFrozen(this, false);

        // Remove the button listener to prevent memory leaks
        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(SkipTutorial);
        }

        // Clear coroutine reference
        countdownCoroutine = null;
    }

    void DisablePlayerScripts()
    {
        foreach (MonoBehaviour script in playerScripts)
        {
            if (script != null)
            {
                script.enabled = false;
            }
        }
    }

    void EnablePlayerScripts()
    {
        foreach (MonoBehaviour script in playerScripts)
        {
            if (script != null)
            {
                script.enabled = true;
            }
        }
    }
}
