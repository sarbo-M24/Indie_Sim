using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePlayerRotation : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float gamePlayPlaneZ = 0f; // Z position of your game plane (usually 0 for 2D)

    [Header("Settings")]
    [SerializeField] private float rotationSpeed = 0f; // 0 = instant
    [SerializeField] private float minRotationDistance = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLine = true;

    private Vector2 mousePositionInput;
    private Vector3 cachedWorldMousePos;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (mainCamera == null) mainCamera = Camera.main;
    }

    // Shared instance (InputManager) — subscribe per enable so a destroyed player leaves no callbacks behind.
    void OnEnable() => InputManager.Controls.Player.Look.performed += OnLook;
    void OnDisable() => InputManager.Controls.Player.Look.performed -= OnLook;

    private void OnLook(InputAction.CallbackContext ctx) => mousePositionInput = ctx.ReadValue<Vector2>();

    void Update()
    {
        UpdateWorldMousePosition();
        RotateTowardsMouse();
    }

    /// <summary>
    /// ✅ NEW METHOD: Use Ray intersection instead of ScreenToWorldPoint
    /// This is NOT affected by camera position/lag!
    /// </summary>
    private void UpdateWorldMousePosition()
    {
        // Resolve lazily, not once in Awake: the persistent camera lives in Boot
        // and is not guaranteed to exist yet on the first frames when this scene
        // is entered directly (additive scene loads complete at end of frame).
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Create a ray from camera through mouse position
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(mousePositionInput.x, mousePositionInput.y, 0));

        // Create a plane at your game's Z position (usually 0 for 2D games)
        Plane gamePlane = new Plane(Vector3.forward, new Vector3(0, 0, gamePlayPlaneZ));

        // Find where the ray intersects the plane
        if (gamePlane.Raycast(ray, out float distance))
        {
            cachedWorldMousePos = ray.GetPoint(distance);
        }
    }

    private void RotateTowardsMouse()
    {
        Vector2 direction = new Vector2(
            cachedWorldMousePos.x - transform.position.x,
            cachedWorldMousePos.y - transform.position.y
        );

        // Gamepad right stick overrides the cursor while it's the active aim source
        if (InputManager.TryGetGamepadAim(out Vector2 stickDirection))
            direction = stickDirection;

        // Only rotate if mouse is far enough from player
        if (direction.sqrMagnitude < minRotationDistance * minRotationDistance) return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Adjust for sprite orientation
        float adjustedAngle = targetAngle + 90; // Change to targetAngle - 90 if sprite faces UP

        if (rotationSpeed > 0)
        {
            float currentAngle = rb.rotation;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, adjustedAngle, rotationSpeed * Time.deltaTime);
            rb.rotation = newAngle;
        }
        else
        {
            rb.rotation = adjustedAngle;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugLine || mainCamera == null || !Application.isPlaying) return;

        // Cyan line to mouse
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, cachedWorldMousePos);
        Gizmos.DrawWireSphere(cachedWorldMousePos, 0.5f);

        // Red line showing player forward direction
        Gizmos.color = Color.red;
        Vector2 forward = new Vector2(Mathf.Cos(rb.rotation * Mathf.Deg2Rad), Mathf.Sin(rb.rotation * Mathf.Deg2Rad));
        Gizmos.DrawRay(transform.position, forward * 2f);

        // Yellow line showing the ray from camera to mouse (for debugging)
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(mousePositionInput.x, mousePositionInput.y, 0));
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(ray.origin, ray.direction * 50f);
    }
}
