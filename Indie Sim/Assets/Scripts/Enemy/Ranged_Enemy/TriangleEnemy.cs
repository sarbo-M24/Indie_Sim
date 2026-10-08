using UnityEngine;

/// <summary>
/// Squid-like triangle ranged enemy.
///
/// ORIENTATION REFERENCE (for lining up your triangle sprite child):
///   HEAD TIP  = local +Y  (pointy tip   — movement direction while Roaming)
///   BUTT BASE = local -Y  (flat/wide end — firing direction while Shooting)
///   So in your sprite child: tip pointing UP, flat base pointing DOWN.
///
/// BULLET POOL REFERENCE:
///   Found automatically at runtime via the "BulletPool" tag — leave Inspector slot empty for PCG.
///
/// LAYER SETUP (required for bullets to damage this enemy):
///   Set this GameObject's Layer to whatever layer is in Bullet's "Damageable Layers" mask.
///
/// STATES:
///   Roaming  — moves head (+Y) forward, wanders, keeps distance band
///   Turning  — stops, fast-spins butt (-Y) toward player
///   Shooting — frozen, butt locked on player, fires bursts while sightline holds
///              sightline breaks → Roaming
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class TriangleEnemy : MonoBehaviour, IDamageable
{
    // =========================================================================
    // INSPECTOR
    // =========================================================================

    [Header("Enemy Settings")]
    public int maxHealth = 100;

    [Header("── Pool ─────────────────────────────────────────")]
    [Tooltip("Leave EMPTY for PCG maps — pool is found at runtime via the 'BulletPool' tag.\n" +
             "Only assign manually if you need to override (e.g. a handcrafted test scene).")]
    [SerializeField] private BulletPool bulletPoolOverride;

    [Header("── Bullet ───────────────────────────────────────")]
    [Tooltip("Damage each bullet deals to the player.")]
    [SerializeField] private int bulletDamage = 10;

    [Tooltip("Speed of fired bullets (units/sec).")]
    [SerializeField] private float bulletSpeed = 8f;

    [Tooltip("How long each bullet lives before returning to pool.")]
    [SerializeField] private float bulletLifetime = 3f;

    [Tooltip("Seconds between each firing burst while in Shooting state.")]
    [SerializeField] private float fireRate = 1.2f;

    [Tooltip("Angle spread in degrees between the two simultaneous bullets.")]
    [SerializeField] private float bulletSpread = 8f;

    [Header("── Sight ───────────────────────────────────────")]
    [Tooltip("Layer(s) that block line-of-sight (walls, obstacles).")]
    [SerializeField] private LayerMask wallLayers;

    [Tooltip("Layer(s) the enemy treats as the player.")]
    [SerializeField] private LayerMask playerLayer;

    [Header("── Distance Band ──────────────────────────────")]
    [Tooltip("Enemy backs away if closer than this.")]
    [SerializeField] private float minRange = 4f;

    [Tooltip("Enemy moves in if farther than this.")]
    [SerializeField] private float maxRange = 9f;

    [Header("── Roaming Movement ────────────────────────────")]
    [Tooltip("Speed while roaming.")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("How fast the enemy turns to face its wander direction (deg/sec).")]
    [SerializeField] private float roamTurnSpeed = 90f;

    [Tooltip("Seconds between picking a new random wander direction.")]
    [SerializeField] private float wanderInterval = 2f;

    [Tooltip("Random variance added to wander interval (±seconds).")]
    [SerializeField] private float wanderVariance = 0.5f;

    [Header("── Attack Turn ──────────────────────────────────")]
    [Tooltip("How fast the enemy spins to point its butt at the player (deg/sec).")]
    [SerializeField] private float attackTurnSpeed = 320f;

    [Tooltip("Degrees from perfect aim at which shooting begins.")]
    [SerializeField] private float aimTolerance = 4f;
    [SerializeField] private GameObject deathParticlePrefab; // Assign in Inspector
    [SerializeField] private CoinDrop coinDrop = new CoinDrop();
    [Header("── NosePoint Offset ─────────────────────────────")]
    [Tooltip("Local -Y offset for the NosePoint (bullet spawn). Adjust to match " +
             "wherever your triangle sprite's flat base centre sits in local space.")]
    [SerializeField] private float noseLocalY = -0.45f;

    // =========================================================================
    // PRIVATE
    // =========================================================================

    private enum State { Roaming, Turning, Shooting }

    private State       state = State.Roaming;
    private Rigidbody2D rb;
    private Transform   playerTransform;
    private Transform   nosePoint;
    private BulletPool  bulletPool;

    // Health
    private int  currentHealth;
    private bool isDead = false;
    public bool IsInAttackMode => state == State.Turning || state == State.Shooting;
    // Roaming
    private float currentMoveAngle;
    private float nextWanderTime;

    // Shooting
    private float nextFireTime;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        rb                = GetComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.freezeRotation = false;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        currentHealth = maxHealth;

        SetupNosePoint();
    }

    private void Start()
    {
        ResolveBulletPool();
        ResolvePlayer();

        currentMoveAngle = Random.Range(0f, 360f);
        nextWanderTime   = Time.time + wanderInterval;
        nextFireTime     = Time.time + Random.Range(0.3f, fireRate);
    }

    private void Update()
    {
        if (isDead || playerTransform == null) return;

        switch (state)
        {
            case State.Roaming:  UpdateRoaming();  break;
            case State.Turning:  UpdateTurning();  break;
            case State.Shooting: UpdateShooting(); break;
        }
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        if (state == State.Roaming)
            ApplyRoamMovement();
        else
            rb.linearVelocity = Vector2.zero;
    }

    // =========================================================================
    // IDAMAGEABLE IMPLEMENTATION
    // =========================================================================

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth  = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"TriangleEnemy took {damage} damage. Health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
            Die();
    }

    public bool IsDead() => isDead;

    public GameObject GetGameObject() => gameObject;

    // =========================================================================
    // DEATH
    // =========================================================================

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        rb.linearVelocity = Vector2.zero;

        // Kill tracker integration — same pattern as Enemy.cs
        if (EnemyKillTracker.Instance != null)
            EnemyKillTracker.Instance.RegisterEnemyKill();

        Debug.Log($"TriangleEnemy died!");
        if (deathParticlePrefab != null)
        {
            Instantiate(deathParticlePrefab, transform.position, Quaternion.identity);
        }
        Sfx.Play(SfxId.EnemyDeath);
        BloodSplatterEffect.SpawnDeathSplatter(transform.position);
        coinDrop.Drop(transform.position);
        // Let EnemyDeath script handle visuals/cleanup if present, otherwise just destroy
        EnemyDeath deathHandler = GetComponent<EnemyDeath>();
        if (deathHandler != null)
            deathHandler.HandleDeath();
        else
            Destroy(gameObject);
    }

    // =========================================================================
    // RUNTIME RESOLUTION
    // =========================================================================

    private void ResolveBulletPool()
    {
        if (bulletPoolOverride != null)
        {
            bulletPool = bulletPoolOverride;
            return;
        }

        GameObject poolObj = GameObject.FindWithTag("BulletPool");
        if (poolObj != null)
        {
            bulletPool = poolObj.GetComponent<BulletPool>();
            if (bulletPool != null) return;
        }

        bulletPool = FindFirstObjectByType<BulletPool>();

        if (bulletPool == null)
            Debug.LogError("[TriangleEnemy] Could not find a BulletPool in the scene! " +
                           "Tag your BulletPool GameObject with 'BulletPool'.", this);
    }

    private void ResolvePlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
            playerTransform = p.transform;
        else
            Debug.LogWarning("[TriangleEnemy] No GameObject tagged 'Player' found in scene.");
    }

    // =========================================================================
    // STATE — ROAMING
    // =========================================================================

    private void UpdateRoaming()
    {
        AdjustWanderForDistanceBand();

        if (Time.time >= nextWanderTime)
            PickNewWanderAngle();

        float targetRot = currentMoveAngle - 90f;
        float newRot    = Mathf.MoveTowardsAngle(
            transform.eulerAngles.z, targetRot, roamTurnSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, newRot);

        if (HasSightLine())
            EnterTurning();
    }

    private void ApplyRoamMovement()
    {
        rb.linearVelocity = AngleToVector(currentMoveAngle) * moveSpeed;
    }

    private void AdjustWanderForDistanceBand()
    {
        float   dist     = Vector2.Distance(transform.position, playerTransform.position);
        Vector2 toPlayer = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;

        if (dist < minRange)
        {
            float away = Mathf.Atan2(-toPlayer.y, -toPlayer.x) * Mathf.Rad2Deg;
            currentMoveAngle = Mathf.MoveTowardsAngle(currentMoveAngle, away, 200f * Time.deltaTime);
        }
        else if (dist > maxRange)
        {
            float toward = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            currentMoveAngle = Mathf.MoveTowardsAngle(currentMoveAngle, toward, 200f * Time.deltaTime);
        }
    }

    private void PickNewWanderAngle()
    {
        currentMoveAngle += Random.Range(-90f, 90f);
        nextWanderTime    = Time.time + wanderInterval + Random.Range(-wanderVariance, wanderVariance);
    }

    // =========================================================================
    // STATE — TURNING
    // =========================================================================

    private void EnterTurning()
    {
        state = State.Turning;
    }

    private void UpdateTurning()
    {
        if (!HasSightLine()) { EnterRoaming(); return; }

        float desiredZ = ButtTowardPlayerAngle();
        float newZ     = Mathf.MoveTowardsAngle(
            transform.eulerAngles.z, desiredZ, attackTurnSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, newZ);

        if (Mathf.Abs(Mathf.DeltaAngle(newZ, desiredZ)) <= aimTolerance)
            EnterShooting();
    }

    // =========================================================================
    // STATE — SHOOTING
    // =========================================================================

    private void EnterShooting()
    {
        state        = State.Shooting;
        nextFireTime = Time.time + 0.08f;
    }

    private void UpdateShooting()
    {
        if (!HasSightLine()) { EnterRoaming(); return; }

        float desiredZ = ButtTowardPlayerAngle();
        transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.MoveTowardsAngle(
                transform.eulerAngles.z, desiredZ, attackTurnSpeed * Time.deltaTime));

        if (Time.time >= nextFireTime)
        {
            FireTwoBullets();
            nextFireTime = Time.time + fireRate;
        }
    }

    private void EnterRoaming()
    {
        state            = State.Roaming;
        currentMoveAngle = transform.eulerAngles.z + 90f;
        nextWanderTime   = Time.time + wanderInterval;
    }

    // =========================================================================
    // FIRING
    // =========================================================================

    private void FireTwoBullets()
    {
        GetComponent<TriangleEnemyJuice>().OnFired();
        if (bulletPool == null) return;

        Vector2 buttDir = -(Vector2)transform.up;

        SpawnBullet(buttDir,  bulletSpread * 0.5f);
        SpawnBullet(buttDir, -bulletSpread * 0.5f);
    }

    private void SpawnBullet(Vector2 baseDir, float angleOffset)
    {
        float   r   = angleOffset * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(
            baseDir.x * Mathf.Cos(r) - baseDir.y * Mathf.Sin(r),
            baseDir.x * Mathf.Sin(r) + baseDir.y * Mathf.Cos(r)
        ).normalized;

        bulletPool.SpawnBullet(nosePoint.position, dir * bulletSpeed, bulletDamage, bulletLifetime);
    }

    // =========================================================================
    // SIGHT LINE
    // =========================================================================

    private bool HasSightLine()
    {
        if (playerTransform == null) return false;

        Vector2 origin = transform.position;
        Vector2 target = playerTransform.position;
        Vector2 dir    = (target - origin).normalized;
        float   dist   = Vector2.Distance(origin, target);

        RaycastHit2D wall = Physics2D.Raycast(origin, dir, dist, wallLayers);
        if (wall.collider != null) return false;

        RaycastHit2D player = Physics2D.Raycast(origin, dir, dist + 0.5f, playerLayer);
        return player.collider != null;
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private float ButtTowardPlayerAngle()
    {
        Vector2 toPlayer = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        return Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg + 90f;
    }

    private static Vector2 AngleToVector(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    private void SetupNosePoint()
    {
        Transform existing = transform.Find("NosePoint");
        if (existing != null)
        {
            nosePoint = existing;
        }
        else
        {
            var g = new GameObject("NosePoint");
            g.transform.SetParent(transform);
            g.transform.localPosition = new Vector3(0f, noseLocalY, 0f);
            g.transform.localRotation = Quaternion.identity;
            g.transform.localScale    = Vector3.one;
            nosePoint                 = g.transform;
        }
    }

    // Public getters to match Enemy.cs pattern
    public int GetCurrentHealth()    => currentHealth;
    public int GetMaxHealth()        => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;

    // =========================================================================
    // EDITOR GIZMOS
    // =========================================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        DrawGizmoCircle(transform.position, minRange);

        Gizmos.color = new Color(1f, 0.85f, 0f, 0.8f);
        DrawGizmoCircle(transform.position, maxRange);

        if (Application.isPlaying && playerTransform != null)
        {
            Gizmos.color = HasSightLine() ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, playerTransform.position);
        }

        // Head direction — cyan (+Y)
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.up * 0.7f);

        // Butt direction — orange (-Y)
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawRay(transform.position, -transform.up * 0.7f);

        if (nosePoint != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(nosePoint.position, 0.07f);
        }

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.9f,
            Application.isPlaying ? $"{state} | HP: {currentHealth}/{maxHealth}" : "TriangleEnemy");
    }

    private static void DrawGizmoCircle(Vector3 c, float r, int seg = 36)
    {
        float   step = 360f / seg;
        Vector3 prev = c + new Vector3(r, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float   rad  = i * step * Mathf.Deg2Rad;
            Vector3 next = c + new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r, 0f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}