using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Stomp Bullet Circle Upgrade")]
    [Tooltip("Speed of the bullets fired in a ring when the Stomp Bullet Circle upgrade is held/burned.")]
    [SerializeField] private float stompBulletSpeed = 12f;
    [SerializeField] private float stompBulletLifetime = 2f;
    [Tooltip("Found automatically at runtime via the 'BulletPool' tag if left empty — same pool the enemies use, bullets are reconfigured per-shot via Bullet.Initialize's layer overrides.")]
    [SerializeField] private BulletPool bulletPoolOverride;
    private BulletPool bulletPool;

    // ✅ UI Reference — drag your light icon Image here in Inspector
    [Header("Cooldown UI")]
    [SerializeField] private Image lightIcon; // Image Type: Filled, Radial360, Top
    [SerializeField] private SpriteRenderer stompReadyOverlay; // Drag the overlay sprite here
    private PlayerController playerController;
    private PlayerControls inputActions;
    private int stompCharges;
    private float stompRechargeTimer;

    /// <summary>Chain Stomp upgrade — mirrors PlayerController.MaxDashCharges.</summary>
    private int MaxStompCharges => 1 + (Pack.Instance != null ? Pack.Instance.Stats.StompExtraCharges : 0);

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
        stompCharges = MaxStompCharges;
        stompRechargeTimer = 0f;
        SetFill(1f);
    }

    private void Update()
    {
        TickStompRecharge();
    }

    /// <summary>Chain Stomp upgrade: using any charge resets the shared cooldown; the whole pool refills at once once it elapses uninterrupted (mirrors PlayerController.TickDashRecharge).</summary>
    private void TickStompRecharge()
    {
        int cap = MaxStompCharges;
        if (stompCharges > cap) stompCharges = cap; // pack shrank mid-run (burn/level-clear) — clamp, don't refund

        if (stompCharges < cap)
        {
            stompRechargeTimer -= Time.deltaTime;
            if (stompRechargeTimer <= 0f)
            {
                stompCharges = cap;
                stompRechargeTimer = 0f;
            }
        }
        else
        {
            stompRechargeTimer = 0f;
        }

        SetFill(stompCharges >= cap ? 1f : 1f - Mathf.Clamp01(stompRechargeTimer / stompCooldown));
    }

    private void TryStomp()
    {
        if (!gameplayInputEnabled) return;

        bool isDashing = playerController != null && playerController.IsDashing();
        if (stompCharges <= 0 || isDashing)
        {
            Debug.Log("Cannot stomp: no charges or dashing");
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

        stompCharges--;
        stompRechargeTimer = stompCooldown; // every stomp resets the shared cooldown — full regen only fires after stompCooldown seconds without another stomp
    }

    private void PerformStomp()
    {
        PackStats stats = Pack.Instance != null ? Pack.Instance.Stats : PackStats.Baseline;

        float finalRadius = stompRadius + stats.StompBonusRadius;
        int finalDamage = stompDamage + stats.StompBonusDamage;

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

        if (stats.StompBulletCount > 0)
            FireBulletCircle(playerPos, stats.StompBulletCount, finalDamage);
    }

    /// <summary>
    /// Stomp Bullet Circle upgrade: fires bulletCount player-owned bullets evenly
    /// spaced in a ring. Reuses the enemy Bullet/BulletPool infrastructure —
    /// there is no separate player-bullet prefab, so each spawn overrides the
    /// pooled bullet's damageable/destruction layers to the stomp's own
    /// enemy/wall layers for the duration of that single shot.
    /// </summary>
    private void FireBulletCircle(Vector2 origin, int bulletCount, int damagePerBullet)
    {
        if (!ResolveBulletPool()) return;

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = (360f / bulletCount) * i * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 velocity = direction * stompBulletSpeed;

            bulletPool.SpawnBullet(origin, velocity, damagePerBullet, stompBulletLifetime, stompEnemyLayer, stompWallLayer);
        }
    }

    private bool ResolveBulletPool()
    {
        if (bulletPool != null) return true;

        if (bulletPoolOverride != null)
        {
            bulletPool = bulletPoolOverride;
            return true;
        }

        GameObject poolObj = GameObject.FindWithTag("BulletPool");
        if (poolObj != null) bulletPool = poolObj.GetComponent<BulletPool>();
        if (bulletPool == null) bulletPool = FindFirstObjectByType<BulletPool>();

        if (bulletPool == null)
            Debug.LogWarning("[PlayerStompController] Stomp Bullet Circle is active but no BulletPool exists in this scene.");

        return bulletPool != null;
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

    /// <summary>Fired on every enemy hit through this method — (damage, source label). Debug/telemetry hook only, no gameplay effect.</summary>
    public static event System.Action<int, string> OnAoeHit;

    /// <summary>
    /// Public so the Dash AoE upgrade (PlayerController) can reuse this wholesale.
    /// `damageOnceTracker`, when provided, limits a given IDamageable to one damage
    /// application across repeated calls sharing the same tracker (e.g. one dash's
    /// worth of ticks) — the push below still applies every call regardless.
    /// </summary>
    public void DamageAndPushEnemies(Vector2 playerPos, float radius, int damage, string sourceLabel = "Stomp", HashSet<IDamageable> damageOnceTracker = null)
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
            bool alreadyHitThisTracker = damageOnceTracker != null && damageable != null && damageOnceTracker.Contains(damageable);
            if (damageable != null && !wasAlreadyDead && damage > 0 && !alreadyHitThisTracker)
            {
                damageable.TakeDamage(damage); // uses upgraded damage

                if (ScoreManager.Instance != null) ScoreManager.Instance.AddDamage(damage);
                if (DamageNumberManager.Instance != null) DamageNumberManager.Instance.Spawn(enemyCol.transform.position, damage);
                OnAoeHit?.Invoke(damage, sourceLabel);
                damageOnceTracker?.Add(damageable);
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
    public bool CanStomp() => gameplayInputEnabled && stompCharges > 0 && !(playerController != null && playerController.IsDashing());

    private void OnDrawGizmosSelected()
    {
        float previewRadius = stompRadius;

        Gizmos.color = stompCharges > 0 ? Color.cyan : Color.gray;
        Gizmos.DrawWireSphere(transform.position, previewRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, previewRadius + stompPushBeyondRadius);
    }
}
