using UnityEngine;
using Unity.Cinemachine;

public class CinemachineCursorLead : MonoBehaviour
{
    public static CinemachineCursorLead Instance;

    [Header("References")]
    [Tooltip("The Cinemachine Camera to control")]
    public CinemachineCamera cinemachineCamera;

    [Tooltip("The player transform")]
    public Transform player;

    [Header("Cursor Lead Settings")]
    [Tooltip("How far the camera target shifts towards cursor (in world units)")]
    public float maxLeadDistance = 3f;

    [Tooltip("How much influence the cursor has (0 = only follow player, 1 = move fully toward cursor)")]
    [Range(0f, 1f)]
    public float cursorInfluence = 0.5f;

    [Tooltip("How smoothly the target moves")]
    public float smoothSpeed = 5f;

    [Header("Camera Feedback Mode")]
    [Tooltip("Choose between directional recoil or traditional camera shake")]
    public CameraFeedbackMode feedbackMode = CameraFeedbackMode.Recoil;

    [Header("Camera Recoil Settings")]
    [Tooltip("How far the camera kicks back when shooting (in world units)")]
    public float recoilStrength = 0.5f;

    [Tooltip("Strength of the spring that pulls camera back")]
    public float returnStrength = 15f;

    [Tooltip("Dampening to smooth the return motion")]
    public float dampening = 5f;

    [Header("Camera Shake Settings")]
    [Tooltip("Intensity of camera shake (amplitude)")]
    public float shakeIntensity = 0.3f; // REDUCED - low amplitude

    [Tooltip("Duration of camera shake")]
    public float shakeDuration = 0.15f;

    [Tooltip("Frequency of shake vibration (higher = more rapid)")]
    public float shakeFrequency = 30f; // NEW - high frequency

    [Tooltip("How quickly shake fades out (higher = faster fade)")]
    public float shakeFadeSpeed = 5f; // NEW - controls fade curve

    [Header("Zoom Settings")]
    public float minZoom = 5f;
    public float maxZoom = 12f;
    public float firingZoom = 8f;
    public float zoomDamp = 3f;

    // Enum for feedback mode
    public enum CameraFeedbackMode
    {
        Recoil,
        Shake
    }

    private GameObject cameraTarget;
    private Vector3 targetPosition;
    private Camera mainCamera;

    // Recoil variables
    private Vector3 recoilOffset = Vector3.zero;
    private Vector3 recoilVelocity = Vector3.zero;

    // Shake variables
    private Vector3 shakeOffset = Vector3.zero;
    private float shakeTimeRemaining = 0f;
    private float currentShakeIntensity = 0f;

    // Zoom variables
    private CinemachineFollow followComponent;
    private bool isFiring = false;
    private float currentZoom;
    private Vector3 baseFollowOffset;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        Debug.Log("=== CinemachineCursorLead Start ===");

        // Get main camera
        mainCamera = Camera.main;
        Debug.Log($"Main Camera found: {mainCamera != null}");

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player == null)
        {
            // Not an error: CameraTargetBinder will call BindPlayer() once the
            // player exists (it may not be in the scene yet at author time).
            Debug.Log("[CinemachineCursorLead] No player found at Start — waiting for CameraTargetBinder.");
            return;
        }

        BindPlayer(player);
    }

    /// <summary>
    /// Assigns the player to follow and sets up the vcam's Follow target.
    /// Callable once, either from Start() (player already present) or later
    /// from CameraTargetBinder (player found/spawned after this vcam's Start()).
    /// </summary>
    public void BindPlayer(Transform playerTransform)
    {
        if (cameraTarget != null)
            return; // already bound

        player = playerTransform;

        // Create a new GameObject to act as the camera's follow target
        cameraTarget = new GameObject("CameraFollowTarget");
        cameraTarget.transform.position = player.position;
        Debug.Log($"Camera target created at: {cameraTarget.transform.position}");

        // Set the Cinemachine camera to track this new target
        if (cinemachineCamera == null)
        {
            Debug.LogError("CINEMACHINE CAMERA NOT ASSIGNED!");
            return;
        }

        cinemachineCamera.Follow = cameraTarget.transform;
        Debug.Log("Camera Follow set to CameraFollowTarget");

        // Get follow component for zoom control
        followComponent = cinemachineCamera.GetComponent<CinemachineFollow>();

        if (followComponent == null)
        {
            Debug.LogError("CINEMACHINEFOLLOW NOT FOUND! Check your camera components.");
            return;
        }

        // Store the initial offset and use it as base
        baseFollowOffset = followComponent.FollowOffset;
        currentZoom = Mathf.Abs(baseFollowOffset.z);
        Debug.Log($"Initial Follow Offset: {baseFollowOffset}, Zoom: {currentZoom}");
        Debug.Log($"Camera Feedback Mode: {feedbackMode}");
        Debug.Log("=== Camera System Initialized ===");
    }

    void LateUpdate()
    {
        // Resolve lazily, not once in Start: the persistent camera lives in Boot
        // and is not guaranteed to exist yet on the first frames when this scene
        // is entered directly (additive scene loads complete at end of frame).
        if (mainCamera == null) mainCamera = Camera.main;

        if (cameraTarget == null || player == null || mainCamera == null || followComponent == null)
            return;

        // Calculate the target position (blend between player and cursor direction)
        CalculateTargetPosition();

        // UPDATE CAMERA FEEDBACK based on mode
        if (feedbackMode == CameraFeedbackMode.Recoil)
        {
            UpdateRecoil();
        }
        else if (feedbackMode == CameraFeedbackMode.Shake)
        {
            UpdateShake();
        }

        // UPDATE ZOOM based on player speed and firing
        UpdateZoom();

        // Apply cursor lead + feedback (recoil or shake) to camera target position
        Vector3 feedbackOffset = (feedbackMode == CameraFeedbackMode.Recoil) ? recoilOffset : shakeOffset;
        feedbackOffset *= SettingsService.Current.screenShake;
        Vector3 finalPosition = targetPosition + feedbackOffset;

        // Smoothly move the camera target to the calculated position
        cameraTarget.transform.position = Vector3.Lerp(
            cameraTarget.transform.position,
            finalPosition,
            smoothSpeed * Time.deltaTime
        );
    }

    void CalculateTargetPosition()
    {
        Vector3 playerToMouse;
        if (InputManager.TryGetGamepadAim(out Vector2 aim))
        {
            // Gamepad: full lead along the aim, held after the stick is
            // released — same as a mouse parked past maxLeadDistance.
            playerToMouse = (Vector3)(aim * maxLeadDistance);
        }
        else
        {
            // Get mouse position in world space
            Vector3 mouseWorldPos = GetMouseWorldPosition();

            // Calculate direction from player to mouse
            playerToMouse = mouseWorldPos - player.position;
        }

        // For 2D (XY plane), ignore Z depth
        playerToMouse.z = 0f;

        // Clamp the distance
        Vector3 cursorOffset = Vector3.ClampMagnitude(playerToMouse, maxLeadDistance);

        // Apply cursor influence, scaled by the Camera lead setting
        cursorOffset *= cursorInfluence * SettingsService.Current.cameraLead;

        // Target position = player position + cursor offset
        targetPosition = player.position + cursorOffset;
    }

    void UpdateRecoil()
    {
        float deltaTime = Time.deltaTime;

        // Spring acceleration (pulls back to zero)
        Vector3 acceleration = -returnStrength * recoilOffset - dampening * recoilVelocity;

        // Update velocity and position
        recoilVelocity += acceleration * deltaTime;
        recoilOffset += recoilVelocity * deltaTime;
    }

    void UpdateShake()
    {
        if (shakeTimeRemaining > 0)
        {
            shakeTimeRemaining -= Time.deltaTime;

            // Exponential fade
            float fadePercent = shakeTimeRemaining / shakeDuration;
            float fadeFactor = Mathf.Pow(fadePercent, shakeFadeSpeed);

            // ULTRA HIGH FREQUENCY using sine waves
            float time = Time.time * shakeFrequency;

            // Two offset sine waves for X and Y create circular vibration
            float shakeX = Mathf.Sin(time) + Mathf.Sin(time * 1.3f + 0.5f);
            float shakeY = Mathf.Cos(time * 0.9f) + Mathf.Cos(time * 1.1f + 1.2f);

            // Normalize and apply amplitude
            shakeOffset = new Vector3(
                shakeX * currentShakeIntensity * fadeFactor * 0.5f,
                shakeY * currentShakeIntensity * fadeFactor * 0.5f,
                0f
            );

            currentShakeIntensity = shakeIntensity * fadeFactor;
        }
        else
        {
            shakeOffset = Vector3.zero;
            currentShakeIntensity = 0f;
        }
    }



    void UpdateZoom()
    {
        if (followComponent == null || player == null) return;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            return;
        }

        float speed = rb.linearVelocity.magnitude;
        float targetZoom = GetTargetZoom(speed, isFiring);

        // Smoothly lerp to target zoom
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * zoomDamp);

        // Apply zoom by modifying the Z component of Follow Offset
        Vector3 newOffset = baseFollowOffset;
        newOffset.z = -currentZoom;
        followComponent.FollowOffset = newOffset;
    }

    float GetTargetZoom(float speed, bool firing)
    {
        if (firing)
        {
            return firingZoom;
        }
        else if (speed > 0.1f)
        {
            return maxZoom;
        }
        else
        {
            return minZoom;
        }
    }

    /// <summary>
    /// Call this from your shooting script to apply camera feedback
    /// Automatically uses the selected mode (Recoil or Shake)
    /// </summary>
    public void ApplyRecoil(Vector2 shootDirection)
    {
        if (feedbackMode == CameraFeedbackMode.Recoil)
        {
            ApplyDirectionalRecoil(shootDirection);
        }
        else if (feedbackMode == CameraFeedbackMode.Shake)
        {
            ApplyCameraShake();
        }
    }

    /// <summary>
    /// Apply directional recoil (kicks opposite to shoot direction)
    /// </summary>
    private void ApplyDirectionalRecoil(Vector2 shootDirection)
    {
        // Kick camera in OPPOSITE direction of shooting
        Vector2 recoilDirection = -shootDirection.normalized;

        // For 2D top-down (XY plane), recoil affects X and Y
        Vector3 recoilKick = new Vector3(
            recoilDirection.x * recoilStrength,
            recoilDirection.y * recoilStrength,
            0f
        );

        // Add to current recoil (allows stacking for rapid fire)
        recoilOffset += recoilKick;

        Debug.Log($"Recoil applied: {recoilKick}");
    }

    /// <summary>
    /// Apply traditional camera shake (random directions)
    /// </summary>
    private void ApplyCameraShake()
    {
        // Start shake timer
        shakeTimeRemaining = shakeDuration;
        currentShakeIntensity = shakeIntensity;

        Debug.Log($"Camera shake applied: Intensity {shakeIntensity}, Duration {shakeDuration}");
    }

    /// <summary>
    /// Apply recoil/shake with custom strength (for different weapons)
    /// </summary>
    public void ApplyRecoil(Vector2 shootDirection, float customStrength)
    {
        if (feedbackMode == CameraFeedbackMode.Recoil)
        {
            Vector2 recoilDirection = -shootDirection.normalized;

            Vector3 recoilKick = new Vector3(
                recoilDirection.x * customStrength,
                recoilDirection.y * customStrength,
                0f
            );

            recoilOffset += recoilKick;
        }
        else if (feedbackMode == CameraFeedbackMode.Shake)
        {
            shakeTimeRemaining = shakeDuration;
            currentShakeIntensity = customStrength;
        }
    }

    /// <summary>
    /// Call when player starts firing
    /// </summary>
    public void StartFiring()
    {
        isFiring = true;
    }

    /// <summary>
    /// Call when player stops firing
    /// </summary>
    public void StopFiring()
    {
        isFiring = false;
    }

    /// <summary>
    /// Reset all camera feedback
    /// </summary>
    public void ResetRecoil()
    {
        recoilOffset = Vector3.zero;
        recoilVelocity = Vector3.zero;
        shakeOffset = Vector3.zero;
        shakeTimeRemaining = 0f;
        currentShakeIntensity = 0f;
    }

    Vector3 GetMouseWorldPosition()
    {
        // Convert mouse screen position to world position
        Vector3 mouseScreenPos = InputManager.PointerPosition;

        // Set Z distance for ScreenToWorldPoint
        mouseScreenPos.z = Mathf.Abs(mainCamera.transform.position.z - player.position.z);

        // Convert to world space
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);

        // Keep same Z as player (2D plane)
        mouseWorldPos.z = player.position.z;

        return mouseWorldPos;
    }

    void OnDestroy()
    {
        // Clean up the camera target when script is destroyed
        if (cameraTarget != null)
        {
            Destroy(cameraTarget);
        }
    }

    // Debug visualization
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || cameraTarget == null) return;

        // Draw feedback offset based on mode
        Gizmos.color = (feedbackMode == CameraFeedbackMode.Recoil) ? Color.red : Color.yellow;
        Vector3 feedbackOffset = (feedbackMode == CameraFeedbackMode.Recoil) ? recoilOffset : shakeOffset;
        Vector3 feedbackVisual = feedbackOffset * 5f; // Scale for visibility
        Gizmos.DrawLine(cameraTarget.transform.position, cameraTarget.transform.position + feedbackVisual);
        Gizmos.DrawWireSphere(cameraTarget.transform.position + feedbackVisual, 0.5f);

        // Draw target position (without feedback)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetPosition, 0.3f);

        // Draw player position
        if (player != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(player.position, 0.2f);
        }
    }
}
