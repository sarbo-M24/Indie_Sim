using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DamageIndicator : MonoBehaviour
{
    [Header("Vignette Settings")]
    [SerializeField] private Volume postProcessVolume; // Assign your Post Process Volume here

    [Header("Damage Flash Settings")]
    [SerializeField] private Color damageColor = new Color(0.8f, 0f, 0f, 1f); // Red color
    [SerializeField] private Color normalColor = Color.black; // Default black
    [SerializeField] private float flashIntensity = 0.5f; // How strong the vignette becomes (0-1)
    [SerializeField] private int flashCount = 3; // Number of flashes
    [SerializeField] private float flashDuration = 0.15f; // Duration of each flash (seconds)
    [SerializeField] private float flashInterval = 0.1f; // Time between flashes (seconds)

    [Header("Normal Vignette Settings")]
    [SerializeField] private float normalIntensity = 0.3f; // Your normal vignette intensity

    [Header("Hitstop Settings")]
    [SerializeField] private bool enableHitstop = true; // Toggle hitstop on/off
    [SerializeField] private float hitstopDuration = 0.1f; // How long to freeze (seconds)
    [SerializeField] private float hitstopIntensity = 0f; // Time scale during hitstop (0 = full freeze)

    [Header("Player Flicker Settings")]
    [SerializeField] private GameObject playerSpriteObject; // Assign the sprite GameObject here
    [SerializeField] private int flickerCount = 5;
    [SerializeField] private float flickerInterval = 0.06f; // Time between ON/OFF


    // The Damage Flash Intensity setting (photosensitivity) blends the flash toward
    // the normal vignette: 0 = no visible flash, 1 = as authored.
    private float ScaledFlashIntensity => Mathf.Lerp(normalIntensity, flashIntensity, SettingsService.Current.flashIntensity);
    private Color ScaledDamageColor => Color.Lerp(normalColor, damageColor, SettingsService.Current.flashIntensity);

    private Vignette vignette;
    private bool isFlashing = false;
    private bool isHitstopped = false;

    private void Start()
    {
        // Scene-local player (Phase 6) — postProcessVolume is normally wired
        // directly in the Inspector on the scene-baked RoguelikeMode player
        // instance, but BossArena's player is runtime-spawned from the prefab
        // asset, which carries no scene-specific override. Re-acquire from
        // this scene's own global Volume (there's only one per scene today).
        if (postProcessVolume == null)
            postProcessVolume = FindFirstObjectByType<Volume>();

        // Get the Vignette effect from the Volume
        if (postProcessVolume == null)
        {
            Debug.LogWarning("[DamageIndicator] No Post Process Volume found in this scene — damage vignette will not appear (cosmetic only).");
            return;
        }

        if (postProcessVolume.profile.TryGet(out vignette))
        {
            // Initialize vignette to normal state
            vignette.color.value = normalColor;
            vignette.intensity.value = normalIntensity;
            Debug.Log("[DamageIndicator] Vignette successfully initialized");
        }
        else
        {
            Debug.LogError("[DamageIndicator] Vignette effect not found in Volume Profile! Add Vignette override to your Volume.");
        }
    }

    /// <summary>
    /// Call this method when the player takes damage
    /// </summary>
    public void TriggerDamageFlash()
    {
        if (vignette == null)
        {
            Debug.LogWarning("[DamageIndicator] Vignette is not set up!");
            return;
        }

        if (!isFlashing)
        {
            StartCoroutine(DamageFlashCoroutine());
        }

        // 🔥 Start player sprite flicker
        if (playerSpriteObject != null)
        {
            StartCoroutine(PlayerFlickerCoroutine());
        }
    }

    /// <summary>
    /// Coroutine that handles the flashing effect with hitstop
    /// </summary>
    private IEnumerator DamageFlashCoroutine()
    {
        isFlashing = true;

        // ✅ HITSTOP: Freeze the game at the moment of damage
        if (enableHitstop && !isHitstopped)
        {
            yield return StartCoroutine(HitstopCoroutine());
        }

        for (int i = 0; i < flashCount; i++)
        {
            // Flash to red
            yield return StartCoroutine(LerpVignetteColor(ScaledDamageColor, ScaledFlashIntensity, flashDuration));

            // Wait briefly
            yield return new WaitForSeconds(flashInterval);

            // Flash back to black (but keep intensity for a moment)
            yield return StartCoroutine(LerpVignetteColor(normalColor, ScaledFlashIntensity, flashDuration));

            // Wait before next flash
            if (i < flashCount - 1) // Don't wait after the last flash
            {
                yield return new WaitForSeconds(flashInterval);
            }
        }

        // Final fade back to normal intensity
        yield return StartCoroutine(LerpVignetteIntensity(normalIntensity, flashDuration));

        isFlashing = false;
    }

    /// <summary>
    /// Hitstop coroutine - freezes time briefly
    /// </summary>
    private IEnumerator HitstopCoroutine()
    {
        isHitstopped = true;

        // PauseController owns timeScale: ending the hit-stop can't undo a
        // pause opened during it.
        PauseController.BeginHitStop(hitstopIntensity);

        // ✅ Trigger camera shake using unscaled time
        CameraShake.Instance?.ShakeCamera(3f, hitstopDuration);


        yield return new WaitForSecondsRealtime(hitstopDuration);

        PauseController.EndHitStop();
        isHitstopped = false;
    }

    private IEnumerator PlayerFlickerCoroutine()
    {
        // Safety check
        if (playerSpriteObject == null)
            yield break;

        // Make sure it starts visible
        playerSpriteObject.SetActive(true);

        for (int i = 0; i < flickerCount; i++)
        {
            // Turn off
            playerSpriteObject.SetActive(false);
            yield return new WaitForSeconds(flickerInterval);

            // Turn on
            playerSpriteObject.SetActive(true);
            yield return new WaitForSeconds(flickerInterval);
        }

        // Ensure it's ON at the end
        playerSpriteObject.SetActive(true);
    }



    /// <summary>
    /// Smoothly change vignette color and intensity
    /// </summary>
    private IEnumerator LerpVignetteColor(Color targetColor, float targetIntensity, float duration)
    {
        Color startColor = vignette.color.value;
        float startIntensity = vignette.intensity.value;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            vignette.color.value = Color.Lerp(startColor, targetColor, t);
            vignette.intensity.value = Mathf.Lerp(startIntensity, targetIntensity, t);

            yield return null;
        }

        vignette.color.value = targetColor;
        vignette.intensity.value = targetIntensity;
    }

    /// <summary>
    /// Smoothly change only the vignette intensity
    /// </summary>
    private IEnumerator LerpVignetteIntensity(float targetIntensity, float duration)
    {
        float startIntensity = vignette.intensity.value;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            vignette.intensity.value = Mathf.Lerp(startIntensity, targetIntensity, t);

            yield return null;
        }

        vignette.intensity.value = targetIntensity;
    }

    /// <summary>
    /// Manual method to set vignette back to normal (if needed)
    /// </summary>
    public void ResetVignette()
    {
        if (vignette != null)
        {
            StopAllCoroutines();
            vignette.color.value = normalColor;
            vignette.intensity.value = normalIntensity;
            if (isHitstopped) PauseController.EndHitStop();
            isFlashing = false;
            isHitstopped = false;
        }
    }

    /// <summary>
    /// For testing - call this to preview the effect
    /// </summary>
    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Press 'H' to test damage flash with hitstop (editor / dev builds only)
        if (Input.GetKeyDown(KeyCode.H))
        {
            TriggerDamageFlash();
            Debug.Log("[DamageIndicator] Test damage flash + hitstop triggered!");
        }
#endif
    }
}
