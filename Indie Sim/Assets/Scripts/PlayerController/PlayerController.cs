using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI; // ✅ NEW — needed for Image
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float baseMoveSpeed = 10f;

    private Rigidbody2D rb;
    private float currentMoveSpeed;
    private Vector2 moveInput;
    private PlayerControls inputActions;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 30f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private bool invulnerableDuringDash = false;

    // ✅ NEW — drag your dash light icon Image here in Inspector
    [Header("Dash Cooldown UI")]
    [SerializeField] private Image dashLightIcon; // Image Type: Filled, Radial360, Top
    [SerializeField] private Volume dashBlurVolume; // Drag DashBlurVolume GameObject here

    [Header("Dash Trail")]
    [SerializeField] private TrailRenderer dashTrail; // Drag the Sprite Body's TrailRenderer here

    [Header("Dash Deflect Upgrade")]
    [Tooltip("Layer enemy bullets live on, checked around the player while dashing.")]
    [SerializeField] private LayerMask enemyBulletLayer;
    [Tooltip("Layer a deflected bullet should be able to damage (the enemy layer) once re-owned.")]
    [SerializeField] private LayerMask deflectedBulletTargetLayer;
    [SerializeField] private float deflectDetectionRadius = 1.2f;

    [Header("Dash Damage Window Upgrade")]
    [SerializeField] private Color dashDamageWindowTint = new Color(1f, 0.35f, 0.35f);

    [Header("Dash Invulnerability / Pass-Through")]
    [Tooltip("Radius around the player, checked every dash physics step, used to find enemy colliders to temporarily ignore so the player can pass through them.")]
    [SerializeField] private float dashPassthroughRadius = 1.5f;
    [Tooltip("Always-on (not upgrade-gated): radius enemies get pushed out of, with no damage, the instant a dash ends.")]
    [SerializeField] private float dashEndClearRadius = 1.75f;

    [Header("Dash AoE Debug Gizmo (visual only - no gameplay logic)")]
    [SerializeField] private bool showDashAoeDebugRadius = false;
    [Tooltip("Static preview radius for tuning in the Scene view before Play mode.")]
    [SerializeField] private float debugDashAoeRadiusPreview = 2f;

    private bool isDashing = false;
    private int dashCharges;
    private float dashRechargeTimer;
    private Vector2 dashDirection;
    private float dashTimeRemaining;

    private PlayerStompController stompController;
    private PlayerHealth playerHealth;
    private Collider2D playerCollider;
    private SpriteRenderer bodySpriteRenderer;
    private Color bodySpriteBaseColor = Color.white;
    private float damageWindowTimeRemaining;
    private Coroutine damageWindowCoroutine;

    private int MaxDashCharges => 1 + (Pack.Instance != null ? Pack.Instance.Stats.DashExtraCharges : 0);

    [Header("Recoil Knockback")]
    [SerializeField] private float knockbackDecay = 8f; // Higher = knockback fades out faster
    private Vector2 knockbackVelocity = Vector2.zero;

    [Header("Bulldozer")]
    [SerializeField] private float pushRadius = 2f;
    [SerializeField] private float pushStr = 5f;
    [SerializeField] private LayerMask enemyLayer;
    private ContactFilter2D enemyFilter;
    private List<Collider2D> pushResults = new List<Collider2D>();

    [Header("Collision Safety")]
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private float wallCheckDistance = 0.5f;

    // Gates movement/dash while a menu (e.g. the upgrade store) is open.
    // Set via SetInputEnabled(), routed through RoguelikeManager.SetGameplayInputEnabled().
    private bool gameplayInputEnabled = true;
    public void SetInputEnabled(bool enabled) => gameplayInputEnabled = enabled;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stompController = GetComponent<PlayerStompController>();
        playerHealth = GetComponent<PlayerHealth>();
        playerCollider = GetComponent<Collider2D>();
        inputActions = new PlayerControls();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        inputActions.Player.Dash.performed += ctx => TryDash();
    }

    void OnEnable() => inputActions.Enable();
    void OnDisable() => inputActions.Disable();

    void Start()
    {
        RefreshSpeed();

        rb.gravityScale = 0;
        rb.linearDamping = 0;
        rb.angularDamping = 0;
        rb.interpolation = RigidbodyInterpolation2D.None;

        enemyFilter = new ContactFilter2D();
        enemyFilter.SetLayerMask(enemyLayer);
        enemyFilter.useLayerMask = true;

        bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodySpriteRenderer != null) bodySpriteBaseColor = bodySpriteRenderer.color;

        // ✅ NEW — start fully ready
        dashCharges = MaxDashCharges;
        dashRechargeTimer = 0f;
        SetDashFill(1f);

        if (dashTrail != null) dashTrail.emitting = false;
    }

    void Update()
    {
        TickDashRecharge();
    }

    /// <summary>
    /// Chain Dash upgrade: charges recharge one at a time on a shared timer
    /// (not gated per-dash), so extra charges let the player dash again
    /// immediately instead of waiting out the full cooldown each time.
    /// </summary>
    private void TickDashRecharge()
    {
        int cap = MaxDashCharges;
        if (dashCharges > cap) dashCharges = cap; // pack shrank mid-run (burn/level-clear) — clamp, don't refund

        if (dashCharges < cap)
        {
            dashRechargeTimer -= Time.deltaTime;
            if (dashRechargeTimer <= 0f)
            {
                dashCharges++;
                dashRechargeTimer = dashCharges < cap ? dashCooldown : 0f;
            }
        }
        else
        {
            dashRechargeTimer = 0f;
        }

        SetDashFill(dashCharges > 0 ? 1f : 1f - Mathf.Clamp01(dashRechargeTimer / dashCooldown));
    }

    public void SetSpeed(float newSpeed) => currentMoveSpeed = newSpeed;


    /// <summary>
    /// Recalculates currentMoveSpeed = baseMoveSpeed + upgrade bonus.
    /// Call this from Start() and after any speed upgrade is applied.
    /// </summary>
    public void RefreshSpeed()
    {
        currentMoveSpeed = baseMoveSpeed;
        Debug.Log($"[PlayerController] Speed refreshed: {currentMoveSpeed}");
    }

    #region Dash System

    private void TryDash()
    {
        if (!gameplayInputEnabled || dashCharges <= 0 || isDashing || moveInput.magnitude < 0.1f)
            return;

        dashDirection = moveInput.normalized;
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            dashDirection,
            dashSpeed * dashDuration,
            collisionMask
        );

        float safeDashDistance = dashSpeed * dashDuration;
        if (hit.collider != null)
        {
            safeDashDistance = Mathf.Max(0, hit.distance - wallCheckDistance);
            if (safeDashDistance < 1f) return;
        }

        StartCoroutine(DashCoroutine(safeDashDistance));
    }

    private IEnumerator DashCoroutine(float maxDistance)
    {
        isDashing = true;
        dashCharges--;
        if (dashRechargeTimer <= 0f) dashRechargeTimer = dashCooldown;
        dashTimeRemaining = dashDuration;

        // Enable blur when dash starts
        if (dashBlurVolume != null) dashBlurVolume.weight = 1f;

        // Start emitting the dash trail
        if (dashTrail != null) dashTrail.emitting = true;

        bool grantInvuln = invulnerableDuringDash;
        if (grantInvuln) playerHealth?.SetDashInvulnerable(true);

        HashSet<Collider2D> dashIgnoredColliders = grantInvuln ? new HashSet<Collider2D>() : null;
        HashSet<IDamageable> dashAoeHitThisDash = new HashSet<IDamageable>();

        float distanceTraveled = 0f;

        try
        {
            while (dashTimeRemaining > 0 && distanceTraveled < maxDistance)
            {
                dashTimeRemaining -= Time.fixedDeltaTime;

                float frameDistance = dashSpeed * Time.fixedDeltaTime;
                if (distanceTraveled + frameDistance > maxDistance)
                    frameDistance = maxDistance - distanceTraveled;

                RaycastHit2D immediateHit = Physics2D.Raycast(
                    transform.position,
                    dashDirection,
                    frameDistance + 0.1f,
                    collisionMask
                );

                if (immediateHit.collider != null) break;

                rb.linearVelocity = dashDirection * dashSpeed;
                distanceTraveled += frameDistance;

                TryDeflectBulletsNearby();
                if (grantInvuln) MaintainDashPassthrough(dashIgnoredColliders);
                TickDashAoe(dashAoeHitThisDash);

                yield return new WaitForFixedUpdate();
            }
        }
        finally
        {
            isDashing = false;
            if (rb != null) rb.linearVelocity = Vector2.zero;

            // Disable blur when dash ends
            if (dashBlurVolume != null) dashBlurVolume.weight = 0f;

            // Stop emitting the dash trail (existing trail segments still fade out naturally)
            if (dashTrail != null) dashTrail.emitting = false;

            if (grantInvuln)
            {
                if (dashIgnoredColliders != null && playerCollider != null)
                {
                    foreach (Collider2D col in dashIgnoredColliders)
                        if (col != null) Physics2D.IgnoreCollision(playerCollider, col, false);
                }
                playerHealth?.SetDashInvulnerable(false);
            }

            // Always-on, not upgrade-gated: clear a small radius around the player, no damage.
            if (stompController != null)
                stompController.DamageAndPushEnemies(transform.position, dashEndClearRadius, 0, "Dash Clear");
        }

        StartDashDamageWindowIfActive();
    }

    /// <summary>Always-on while invulnerableDuringDash: lets the player physically pass through enemy colliders for the dash's duration.</summary>
    private void MaintainDashPassthrough(HashSet<Collider2D> dashIgnoredColliders)
    {
        if (playerCollider == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, dashPassthroughRadius, enemyLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit == null || dashIgnoredColliders.Contains(hit)) continue;
            Physics2D.IgnoreCollision(playerCollider, hit, true);
            dashIgnoredColliders.Add(hit);
        }
    }

    /// <summary>Dash AoE upgrade — ticks every dash physics step. dashAoeHitThisDash enforces damage-once-per-enemy; the push is unconditional every tick.</summary>
    private void TickDashAoe(HashSet<IDamageable> dashAoeHitThisDash)
    {
        if (Pack.Instance == null || stompController == null) return;
        PackStats stats = Pack.Instance.Stats;
        if (stats.DashAoeRadius <= 0f) return;

        stompController.DamageAndPushEnemies(transform.position, stats.DashAoeRadius, stats.DashAoeDamage, "Dash AoE", dashAoeHitThisDash);
    }

    private void StartDashDamageWindowIfActive()
    {
        if (Pack.Instance == null) return;
        PackStats stats = Pack.Instance.Stats;
        if (stats.DashDamageWindowDuration <= 0f) return;

        if (damageWindowCoroutine != null) StopCoroutine(damageWindowCoroutine);
        damageWindowCoroutine = StartCoroutine(DashDamageWindowRoutine(stats.DashDamageWindowDuration));
    }

    /// <summary>Dash Damage Window upgrade — red tint + a timer PlayerConeShooter reads via GetDashDamageMultiplier().</summary>
    private IEnumerator DashDamageWindowRoutine(float duration)
    {
        damageWindowTimeRemaining = duration;
        if (bodySpriteRenderer != null) bodySpriteRenderer.color = dashDamageWindowTint;

        while (damageWindowTimeRemaining > 0f)
        {
            damageWindowTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        if (bodySpriteRenderer != null) bodySpriteRenderer.color = bodySpriteBaseColor;
        damageWindowCoroutine = null;
    }

    /// <summary>Read by PlayerConeShooter every shot — 1x outside the window, the upgrade's multiplier while it's running.</summary>
    public float GetDashDamageMultiplier()
    {
        if (damageWindowTimeRemaining <= 0f || Pack.Instance == null) return 1f;
        return Pack.Instance.Stats.DashDamageWindowMultiplier;
    }

    /// <summary>Dash Deflect upgrade — checked every dash physics step while the upgrade is held/burned.</summary>
    private void TryDeflectBulletsNearby()
    {
        if (Pack.Instance == null || !Pack.Instance.Stats.DashDeflectEnabled) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, deflectDetectionRadius, enemyBulletLayer);
        foreach (Collider2D hit in hits)
        {
            Bullet bullet = hit.GetComponent<Bullet>();
            if (bullet == null) continue;

            Vector2 reflected = -bullet.CurrentVelocity;
            bullet.Deflect(deflectedBulletTargetLayer, reflected);
        }
    }

    #endregion

    // ✅ NEW — sets fill on the dash light icon
    private void SetDashFill(float amount)
    {
        if (dashLightIcon != null)
            dashLightIcon.fillAmount = amount;
    }

    void FixedUpdate()
    {
        if (isDashing) return;

        if (!gameplayInputEnabled)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = moveInput * currentMoveSpeed + knockbackVelocity;
        knockbackVelocity = Vector2.Lerp(knockbackVelocity, Vector2.zero, knockbackDecay * Time.fixedDeltaTime);

        if (moveInput.magnitude > 0.1f)
        {
            HandleBulldozerPhysics();
        }
    }

    /// <summary>
    /// Pushes the player backward, opposite to fireDirection. Call from weapon VFX on shoot.
    /// Fades out over time rather than being an instant impulse, since FixedUpdate
    /// overwrites rb.linearVelocity every step (a raw AddForce would be wiped next frame).
    /// </summary>
    public void ApplyKnockback(Vector2 fireDirection, float force)
    {
        if (force <= 0f || fireDirection == Vector2.zero) return;
        knockbackVelocity += -fireDirection.normalized * force;
    }

    private void HandleBulldozerPhysics()
    {
        pushResults.Clear();
        Physics2D.OverlapCircle(transform.position, pushRadius, enemyFilter, pushResults);

        foreach (Collider2D col in pushResults)
        {
            if (col == null) continue;

            Rigidbody2D enemyRb = col.attachedRigidbody;
            if (enemyRb != null)
            {
                Vector2 toEnemy = (Vector2)col.transform.position - (Vector2)transform.position;
                Vector2 awayDir = toEnemy.normalized;
                Vector2 moveDir = moveInput.normalized;
                Vector2 sideDir = new Vector2(-moveDir.y, moveDir.x);

                float dot = Vector2.Dot(sideDir, toEnemy);
                if (dot < 0) sideDir = -sideDir;

                Vector2 finalPush = (awayDir * 0.3f) + (sideDir * 0.7f);
                enemyRb.linearVelocity = finalPush.normalized * pushStr;
            }
        }
    }

    public bool IsDashing() => isDashing;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pushRadius);

        if (moveInput.magnitude > 0.1f)
        {
            Gizmos.color = dashCharges > 0 ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position, moveInput.normalized * dashSpeed * dashDuration);
        }

        if (showDashAoeDebugRadius)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, debugDashAoeRadiusPreview);

            if (Application.isPlaying && Pack.Instance != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f);
                Gizmos.DrawWireSphere(transform.position, Pack.Instance.Stats.DashAoeRadius);
            }
        }
    }
}
