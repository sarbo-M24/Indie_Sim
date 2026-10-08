using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-local brain of Tutorial.unity. Owns the hint banner, knows which
/// TutorialHintZone the player is currently in, and ends the tutorial
/// (GameManager.FinishTutorial → first dungeon) when the player takes the
/// scene's Teleporter (Finishes Tutorial ticked).
///
/// Dying here doesn't end the run: the director claims the death as a
/// GameManager run-end interceptor, skips the death screen, and restarts the
/// tutorial with a fresh run after Restart Delay.
///
/// Hint text can name controls with tokens — {Move} {Fire} {Dash} {Stomp}
/// {Reload} {SwitchWeapon} {Pause} — which show the player's current binding
/// for the device they're using, and re-render when they swap keyboard ↔ pad.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    public static TutorialDirector Instance { get; private set; }

    [Header("Hint Banner")]
    [SerializeField] private CanvasGroup hintGroup;
    [SerializeField] private TextMeshProUGUI hintText;
    [Tooltip("Colour of the {Token} key names inside hints.")]
    [SerializeField] private Color keyColor = new Color(1f, 0.84f, 0.29f);
    [SerializeField] private float fadeDuration = 0.15f;

    [Header("Text")]
    [Tooltip("Shown when the scene starts, before the player enters any zone. Empty = no banner.")]
    [TextArea] [SerializeField] private string introHint = "";
    [Tooltip("Shown when a zone's task is done and that zone has no Complete Text of its own.")]
    [TextArea] [SerializeField] private string defaultCompleteText = "Nice! Now head to the next room.";
    [Tooltip("Shown on death, while the tutorial restarts.")]
    [TextArea] [SerializeField] private string deathText = "Ouch! Let's try that again.";

    [Header("Death")]
    [Tooltip("Seconds (real time) the player sees their corpse before the tutorial restarts.")]
    [SerializeField] private float restartDelay = 1.5f;

    public TutorialHintZone CurrentZone { get; private set; }

    private string rawText;
    private bool renderedForGamepad;
    private bool finishing;
    private bool interceptorAdded;
    private Coroutine fade;

    private void Awake()
    {
        Instance = this;
        if (hintGroup != null) hintGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (interceptorAdded && GameManager.Instance != null) GameManager.Instance.RemoveRunEndInterceptor(InterceptDeath);
    }

    private IEnumerator Start()
    {
        if (!string.IsNullOrEmpty(introHint)) Show(introHint);

        // GameManager arrives a frame late when Play is pressed on this scene
        // directly (SceneBootstrapGuard) — wait for it.
        while (GameManager.Instance == null) yield return null;
        GameManager.Instance.AddRunEndInterceptor(InterceptDeath);
        interceptorAdded = true;
    }

    // Claims every death in the tutorial: the run isn't ended (finalize is
    // never called), so no death screen and no slot wipe.
    private bool InterceptDeath(RunEndReason reason, System.Action finalize)
    {
        if (reason != RunEndReason.Death || finishing) return false;
        finishing = true;
        StartCoroutine(RestartAfterDeath());
        return true;
    }

    private IEnumerator RestartAfterDeath()
    {
        Show(deathText);
        yield return new WaitForSecondsRealtime(restartDelay);
        GameManager.Instance.RestartTutorial();
    }

    private void Update()
    {
        // Device swapped since the text was rendered — redo the key names.
        if (rawText != null && renderedForGamepad != InputManager.UsingGamepad)
            Render();
    }

    /// <summary>The player walked into a zone: it becomes current and its text shows.</summary>
    public void EnterZone(TutorialHintZone zone)
    {
        CurrentZone = zone;
        Show(zone.Completed ? CompleteTextFor(zone) : zone.Hint);
    }

    /// <summary>A zone's task is done.</summary>
    public void ZoneCompleted(TutorialHintZone zone)
    {
        if (CurrentZone == zone) Show(CompleteTextFor(zone));
    }

    /// <summary>Ends the tutorial and starts the run's first dungeon. Safe to call more than once.</summary>
    public void Finish()
    {
        if (finishing) return;
        finishing = true;
        Hide();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.FinishTutorial();
        }
        else
        {
            Debug.LogWarning("[TutorialDirector] No GameManager — loading RoguelikeMode directly.");
            SceneManager.LoadScene("RoguelikeMode");
        }
    }

    public void Show(string text)
    {
        rawText = text;
        Render();
        FadeTo(1f);
    }

    public void Hide()
    {
        rawText = null;
        FadeTo(0f);
    }

    private string CompleteTextFor(TutorialHintZone zone) =>
        string.IsNullOrEmpty(zone.CompleteText) ? defaultCompleteText : zone.CompleteText;

    private void Render()
    {
        renderedForGamepad = InputManager.UsingGamepad;
        if (hintText != null) hintText.text = TutorialText.Format(rawText, keyColor);
    }

    private void FadeTo(float target)
    {
        if (hintGroup == null) return;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Fade(target));
    }

    // Unscaled so the banner still settles while the game is paused or in hitstop.
    private IEnumerator Fade(float target)
    {
        float start = hintGroup.alpha;
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            hintGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        hintGroup.alpha = target;
        fade = null;
    }
}
