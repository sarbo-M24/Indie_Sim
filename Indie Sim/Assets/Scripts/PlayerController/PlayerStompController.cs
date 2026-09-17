using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Handles the player's Stomp ability + its cooldown UI.
/// Dark icon sits below the light icon in Canvas — no script needed on it.
/// Light icon uses Image Type: Filled, Fill Method: Radial360, Fill Origin: Top.
/// </summary>
public class PlayerStompController : MonoBehaviour
{
    [Header("Stomp Settings")]
    [SerializeField] private float stompRadius = 5f;
    [SerializeField] private int stompDamage = 25;
    [SerializeField] private float stompPushBeyondRadius = 1.5f;
    [SerializeField] private float stompCooldown = 2f;
    [SerializeField] private bool stompCostCoins = false;
    [SerializeField] private int stompCoinCost = 10;
    [SerializeField] private LayerMask stompEnemyLayer;
    [SerializeField] private LayerMask stompWallLayer;
    [SerializeField] private LayerMask stompBulletLayer;
    [SerializeField] private GameObject stompVFXPrefab;
    [SerializeField] private float stompScreenShakeDuration = 0.1f;
    [SerializeField] private float stompScreenShakeAmp = 10.0f;

    // ✅ UI Reference — drag your light icon Image here in Inspector
    [Header("Cooldown UI")]
    [SerializeField] private Image lightIcon; // Image Type: Filled, Radial360, Top
    [SerializeField] private SpriteRenderer stompReadyOverlay; // Drag the overlay sprite here
    private PlayerController playerController;
    private PlayerControls inputActions;
    private bool canStomp = true;
    private Coroutine fillCoroutine;

    // Gates stomp while a menu (e.g. the upgrade store) is open.
    // Set via SetInputEnabled(), routed through RoguelikeManager.SetGameplayInputEnabled().
    private bool gameplayInputEnabled = true;
    public void SetInputEnabled(bool enabled) => gameplayInputEnabled = enabled;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        inputActions = new PlayerControls();
        inputActions.Player.Stomp.performed += ctx => TryStomp();
    }

    private void OnEnable() => inputActions.Enable();
    private void OnDisable() => inputActions.Disable();

    private void Start()
    {
        // Start fully ready
        SetFill(1f);
    }

    private void TryStomp()
    {
        if (!gameplayInputEnabled) return;

        bool isDashing = playerController != null && playerController.IsDashing();
        if (!canStomp || isDashing)
        {
            Debug.Log("Cannot stomp: on cooldown or dashing");
            return;
        }

        if (stompCostCoins)
        {
            if (CoinManager.Instance == null || !CoinManager.Instance.HasEnoughCoins(stompCoinCost))
            {
                Debug.Log($"Not enough coins for stomp! Need: {stompCoinCost}");
                return;
            }
            CoinManager.Instance.SpendCoins(stompCoinCost);
        }

        PerformStomp();

        // ✅ Start cooldown + UI together
        if (fillCoroutine != null) StopCoroutine(fillCoroutine);
        fillCoroutine = StartCoroutine(StompCooldownRoutine());
    }

    private void PerformStomp()
    {
        float finalRadius = stompRadius;
        int finalDamage = stompDamage;

        Debug.Log($"STOMP! Radius: {finalRadius}, Damage: {finalDamage}");

        Vector2 playerPos = transform.position;

        if (stompVFXPrefab != null)
        {
            GameObject vfx = Instantiate(stompVFXPrefab, transform.position, Quaternion.identity);
            StompShockwave shockwave = vfx.GetComponent<StompShockwave>();
            if (shockwave != null) shockwave.Initialize(finalRadius); // pass upgraded radius to VFX
        }

        CameraShake.Instance?.ShakeCamera(stompScreenShakeAmp, stompScreenShakeDuration);

        DestroyBulletsInRange(playerPos, finalRadius);
        DamageAndPushEnemies(playerPos, finalRadius, finalDamage);
    }

    private void DestroyBulletsInRange(Vector2 playerPos, float radius)
    {
        Collider2D[] bullets = Physics2D.OverlapCircleAll(playerPos, radius, stompBulletLayer);

        foreach (Collider2D bulletCol in bullets)
        {
            Vector2 toTarget = (Vector2)bulletCol.transform.position - playerPos;
            RaycastHit2D wallCheck = Physics2D.Raycast(playerPos, toTarget.normalized, toTarget.magnitude, stompWallLayer);

            if (wallCheck.collider == null)
            {
                Destroy(bulletCol.gameObject);
            }
        }
    }

    private void DamageAndPushEnemies(Vector2 playerPos, float radius, int damage)
    {
        Collider2D[] enemies = Physics2D.OverlapCircleAll(playerPos, radius, stompEnemyLayer);

        foreach (Collider2D enemyCol in enemies)
        {
            Vector2 toEnemy = (Vector2)enemyCol.transform.position - playerPos;
            float distanceToEnemy = toEnemy.magnitude;

            RaycastHit2D wallCheck = Physics2D.Raycast(playerPos, toEnemy.normalized, distanceToEnemy, stompWallLayer);
            if (wallCheck.collider != null) continue;

            IDamageable damageable = enemyCol.GetComponent<IDamageable>();
            bool wasAlreadyDead = damageable != null && damageable.IsDead();
            if (damageable != null && !wasAlreadyDead)
            {
                damageable.TakeDamage(damage); // uses upgraded damage

                if (ScoreManager.Instance != null) ScoreManager.Instance.AddDamage(damage);
                if (DamageNumberManager.Instance != null) DamageNumberManager.Instance.Spawn(enemyCol.transform.position, damage);
            }

            // Only physically shove things that can actually move (have a Rigidbody2D) and
            // weren't just killed by the damage above — stationary structures like the
            // EnemySpawner have no Rigidbody2D, so without this guard they'd get teleported
            // to the edge of the stomp radius instantly, looking like they vanished.
            Rigidbody2D enemyRb = enemyCol.GetComponent<Rigidbody2D>();
            bool justDied = damageable != null && !wasAlreadyDead && damageable.IsDead();
            if (enemyRb != null && !justDied)
            {
                Vector2 pushDirection = toEnemy.normalized;
                float targetDistance = radius + stompPushBeyondRadius;
                Vector2 targetPosition = playerPos + (pushDirection * targetDistance);

                RaycastHit2D pushWallCheck = Physics2D.Raycast(
                    enemyCol.transform.position, pushDirection,
                    Vector2.Distance(enemyCol.transform.position, targetPosition), stompWallLayer);

                if (pushWallCheck.collider != null)
                {
                    float safeDistance = pushWallCheck.distance - 0.5f;
                    targetPosition = (Vector2)enemyCol.transform.position + (pushDirection * safeDistance);
                }

                enemyCol.transform.position = targetPosition;
                enemyRb.linearVelocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// Handles both the cooldown timer AND the UI fill animation in one coroutine.
    /// </summary>
    private IEnumerator StompCooldownRoutine()
    {
        canStomp = false;

        // ✅ Instantly empty the light icon
        SetFill(0f);

        float elapsed = 0f;

        // ✅ Refill the icon smoothly over the cooldown duration
        while (elapsed < stompCooldown)
        {
            elapsed += Time.deltaTime;
            SetFill(Mathf.Clamp01(elapsed / stompCooldown));
            yield return null;
        }

        // ✅ Ensure perfect full fill at the end
        SetFill(1f);
        canStomp = true;
        fillCoroutine = null;

        Debug.Log("Stomp ready!");
    }

    private void SetFill(float amount)
    {
        if (lightIcon != null)
            lightIcon.fillAmount = amount;

        if (stompReadyOverlay != null)
        {
            // Invisible during cooldown, full visibility when ready
            Color c = stompReadyOverlay.color;
            c.a = (amount >= 1f) ? 1f : 0f;
            stompReadyOverlay.color = c;
        }
    }
    public bool CanStomp() => gameplayInputEnabled && canStomp && !(playerController != null && playerController.IsDashing());

    private void OnDrawGizmosSelected()
    {
        float previewRadius = stompRadius;

        Gizmos.color = canStomp ? Color.cyan : Color.gray;
        Gizmos.DrawWireSphere(transform.position, previewRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, previewRadius + stompPushBeyondRadius);
    }
}
