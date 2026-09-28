using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class CustomCrosshair : MonoBehaviour
{
    public static CustomCrosshair Instance;

    [Header("Crosshair UI")]
    [SerializeField] private Image crosshairImage;
    [SerializeField] private RectTransform crosshairRect;
    [SerializeField] private Canvas canvas;

    [Header("Hitmarker")]
    [SerializeField] private GameObject hitmarkerObject;
    [SerializeField] private float hitmarkerDuration = 0.15f;

    [Header("Scale Feedback")]
    [SerializeField] private bool enableScaleFeedback = true;
    [SerializeField] private float hitScaleMultiplier = 1.3f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);

    [Header("Gamepad")]
    [Tooltip("World units from the player to the reticle while aiming with the right stick.")]
    [SerializeField] private float gamepadReticleDistance = 4f;

    private bool isShowingHitFeedback = false;
    private Transform player; // found lazily — only needed for gamepad aim

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        ApplyForScene(SceneManager.GetActiveScene());

        if (hitmarkerObject != null)
            hitmarkerObject.SetActive(false);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyForScene(scene);
    }

    // Whether the crosshair graphic should render is a rendering concern of
    // this script; Cursor.visible/lockState is CursorController's job now.
    private void ApplyForScene(Scene scene)
    {
        SceneUIMode uiMode = FindFirstObjectByType<SceneUIMode>();
        bool isMenuScene = uiMode != null && uiMode.ShowCursor;

        if (!isMenuScene)
        {
            // Re-grab canvas and crosshair references from new game scene.
            // Scenes can have more than one Canvas (e.g. BossArena's
            // DemoCompleteCanvas alongside the HUD), so checking only the
            // first Canvas found isn't reliable — it can silently grab the
            // wrong one (crosshairImage/crosshairRect then keep their stale
            // references to the previous scene's now-destroyed canvas,
            // freezing the crosshair in place). Search all canvases for the
            // one that actually has a "Crosshair" child.
            Transform crosshairTransform = null;
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                crosshairTransform = c.transform.Find("Crosshair"); // ← match exact name
                if (crosshairTransform != null)
                {
                    canvas = c;
                    break;
                }
            }

            if (crosshairTransform != null)
            {
                crosshairImage = crosshairTransform.GetComponent<Image>();
                crosshairRect = crosshairTransform.GetComponent<RectTransform>();

                // Re-grab hitmarker from crosshair's children
                if (hitmarkerObject == null)
                    hitmarkerObject = crosshairTransform.Find("Hitmarker")?.gameObject; // ← match exact name
            }
            else
                Debug.LogWarning("[Crosshair] No canvas with a Crosshair child found in this scene!");
        }

        if (crosshairImage != null)
            crosshairImage.gameObject.SetActive(!isMenuScene);

        if (hitmarkerObject != null)
            hitmarkerObject.SetActive(false);

        Debug.Log($"[Crosshair] Scene: {scene.name}, isMenu: {isMenuScene}, crosshairImage: {crosshairImage}");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        // Hidden while a menu (shop, pause, death) has player input off —
        // otherwise it keeps orbiting the player on the right stick.
        if (crosshairImage != null) crosshairImage.enabled = InputManager.Controls.Player.enabled;

        if (crosshairRect != null && canvas != null)
        {
            Vector2 mousePosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                CrosshairScreenPoint(),
                canvas.worldCamera,
                out mousePosition
            );

            crosshairRect.localPosition = mousePosition;
        }
    }

    /// <summary>The pointer, or on gamepad a point gamepadReticleDistance along the stick aim from the player.</summary>
    private Vector2 CrosshairScreenPoint()
    {
        if (InputManager.TryGetGamepadAim(out Vector2 aim) && Camera.main != null)
        {
            if (player == null)
            {
                PlayerConeShooter shooter = FindFirstObjectByType<PlayerConeShooter>();
                if (shooter != null) player = shooter.transform;
            }

            if (player != null)
                return Camera.main.WorldToScreenPoint(player.position + (Vector3)(aim * gamepadReticleDistance));
        }

        return InputManager.PointerPosition;
    }

    public void ShowHitFeedback()
    {
        if (!isShowingHitFeedback)
            StartCoroutine(HitFeedbackCoroutine());
    }

    private IEnumerator HitFeedbackCoroutine()
    {
        isShowingHitFeedback = true;

        if (hitmarkerObject != null)
            hitmarkerObject.SetActive(true);

        Vector3 originalScale = crosshairRect.localScale;
        Vector3 targetScale = originalScale * hitScaleMultiplier;

        float elapsedTime = 0f;
        while (elapsedTime < hitmarkerDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / hitmarkerDuration;

            if (enableScaleFeedback)
                crosshairRect.localScale = Vector3.Lerp(targetScale, originalScale, t);

            yield return null;
        }

        if (hitmarkerObject != null)
            hitmarkerObject.SetActive(false);

        if (enableScaleFeedback)
            crosshairRect.localScale = originalScale;

        isShowingHitFeedback = false;
    }
}