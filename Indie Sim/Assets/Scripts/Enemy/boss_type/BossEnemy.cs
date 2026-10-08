using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BossEnemy : MonoBehaviour, IDamageable
{
    [Header("Boss Spawning Requirements")]
    [SerializeField] private int minTeleporterUses = 2;
    [SerializeField] private int maxTeleporterUses = 5;
    [SerializeField] private float maxSpawnTimeSeconds = 180f;

    [Header("References")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private BulletPool bulletPool; 
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private LayerMask playerLayer;

    [Header("Boss Stats")]
    [SerializeField] private int maxHealth = 500;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Pinball Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float speedIncreasePerBounce = 0.5f;
    [SerializeField] private float directionChangeInterval = 2f;

    [Header("Shield System")]
    [SerializeField] private int shieldHealth = 100;
    [SerializeField] private Color shieldColor = new Color(0f, 0.8f, 1f, 0.5f);
    [SerializeField] private GameObject shieldVisualPrefab;

    [Header("Wall Stick Mechanic")]
    [SerializeField] private float wallStickDuration = 5f;
    [SerializeField] private float stickDistance = 0.5f;

    [Header("Circular Bullet Attack")]
    [SerializeField] private int bulletsPerWave = 20; 
    [SerializeField] private float angleBetweenBullets = 18f; 
    [SerializeField] private float timeBetweenWaves = 0.3f; 
    [SerializeField] private float timeBetweenAttacks = 1.5f; 
    [SerializeField] private float bulletSpeed = 8f;
    [SerializeField] private float bulletLifetime = 5f;
    [SerializeField] private int bulletDamage = 5;
    [SerializeField] private float wallCheckDistance = 1f;

    [Header("Visual Feedback")]
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private Color stuckColor = Color.yellow;
    [SerializeField] private SpriteRenderer whiteSpriteRenderer;

    [Header("Loot Drop")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int minCoins = 10;
    [SerializeField] private int maxCoins = 20;
    [SerializeField] private float coinDropForce = 5f;
    [SerializeField] private float coinSpreadRadius = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip bounceSound;
    [SerializeField] private AudioClip shieldBreakSound;
    [SerializeField] private AudioClip shootSound;

    // Components
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Transform playerTransform;
    private GameObject shieldVisual;
    
    // State
    private int currentHealth;
    private int currentShieldHealth;
    private bool hasShield = true;
    private bool isDead = false;
    private bool isStuckToWall = false;
    private float lastAttackTime = 0f;
    private Color originalColor;
    private Coroutine flashCoroutine;
    private Vector2 moveDirection;
    private float nextDirectionChangeTime;
    private float currentSpeed;
    private Vector2 stuckPosition;

    // Failsafe State
    private Vector2 lastStuckCheckPos;
    private float stuckCheckTimer;
    private const float STUCK_DISTANCE_THRESHOLD = 1.0f;

    // Events
    public System.Action OnDeath;
    public System.Action<int, int> OnHealthChanged;
    public System.Action OnShieldBroken;

    void Start()
    {
        InitializeBoss();
    }

    private void InitializeBoss()
    {
        currentHealth = maxHealth;
        currentShieldHealth = shieldHealth;
        currentSpeed = moveSpeed;

        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();

        rb.linearDamping = 0f;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerObject = player;
            }
        }

        UpdateMoveDirection();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        if (shieldVisualPrefab != null)
        {
            shieldVisual = Instantiate(shieldVisualPrefab, transform);
        }

        lastStuckCheckPos = transform.position;
        UpdateShieldVisual();
    }

    void Update()
    {
        if (isDead) return;

        // Failsafe Watchdog: If boss hasn't moved 1 unit in 1 second, force a launch.
        if (!isStuckToWall)
        {
            stuckCheckTimer += Time.deltaTime;
            if (stuckCheckTimer >= 1.0f)
            {
                float distanceMoved = Vector2.Distance(transform.position, lastStuckCheckPos);
                if (distanceMoved < STUCK_DISTANCE_THRESHOLD)
                {
                    Debug.LogWarning("Boss stuck detected by watchdog! Launching...");
                    RescueLaunch();
                }
                lastStuckCheckPos = transform.position;
                stuckCheckTimer = 0f;
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        if (isStuckToWall)
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = stuckPosition;
        }
        else
        {
            rb.linearVelocity = moveDirection * currentSpeed;
        }
    }

    private void RescueLaunch()
    {
        moveDirection = Random.insideUnitCircle.normalized;
        currentSpeed = moveSpeed; // Reset speed slightly to stabilize
        rb.linearVelocity = moveDirection * currentSpeed;
        stuckCheckTimer = 0f;
    }

    private void UpdateMoveDirection()
    {
        if (playerTransform != null)
        {
            moveDirection = (playerTransform.position - transform.position).normalized;
        }
        else
        {
            moveDirection = Random.insideUnitCircle.normalized;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.gameObject == null) return;

        int collisionLayer = collision.gameObject.layer;

        if (IsInLayerMask(collisionLayer, wallLayers))
        {
            if (!hasShield)
            {
                StickToWall(collision);
            }
            else
            {
                BounceOffWall(collision);
            }
        }

        if (IsInLayerMask(collisionLayer, playerLayer))
        {
            AttemptAttackPlayer(collision.gameObject);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision == null || collision.gameObject == null) return;

        if (IsInLayerMask(collision.gameObject.layer, playerLayer))
        {
            AttemptAttackPlayer(collision.gameObject);
        }
    }

    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return layerMask == (layerMask | (1 << layer));
    }

    private void BounceOffWall(Collision2D collision)
    {
        if (collision.contacts.Length == 0) return;

        Vector2 averagedNormal = Vector2.zero;
        foreach (var contact in collision.contacts) averagedNormal += contact.normal;
        averagedNormal = averagedNormal.normalized;

        rb.linearVelocity = Vector2.zero;

        // 1. Calculate the standard reflection
        Vector2 reflectDir = Vector2.Reflect(moveDirection, averagedNormal).normalized;

        // 2. THE GLIDE KILLER: Check the angle between the reflection and the wall
        // If the angle is too shallow (less than 20 degrees), we force it to 30 degrees
        float dot = Vector2.Dot(reflectDir, averagedNormal); 
        
        // A dot product of 0 means parallel to wall (gliding)
        // A dot product of 1 means perpendicular (straight back)
        if (dot < 0.35f) // Roughly 20-25 degrees
        {
            // Mix the reflection with the normal to "push" it away from the wall
            reflectDir = Vector2.Lerp(reflectDir, averagedNormal, 0.4f).normalized;
            Debug.Log("Shallow angle detected! Correcting glide.");
        }

        // 3. Teleport Nudge (Keep this at 0.25f or 0.15f)
        transform.position += (Vector3)averagedNormal * 0.2f;

        // 4. Re-Launch
        moveDirection = reflectDir;
        currentSpeed = Mathf.Min(currentSpeed + speedIncreasePerBounce, maxSpeed);
        rb.linearVelocity = moveDirection * currentSpeed;

        stuckCheckTimer = 0f; 
        if (audioSource != null && bounceSound != null) audioSource.PlayOneShot(bounceSound, 0.4f);
    }

    private void StickToWall(Collision2D collision)
    {
        if (collision.contacts.Length == 0) return;

        isStuckToWall = true;
        Vector2 averageNormal = Vector2.zero;
        Vector2 averagePoint = Vector2.zero;

        foreach (var contact in collision.contacts)
        {
            averageNormal += contact.normal;
            averagePoint += contact.point;
        }

        averageNormal = averageNormal.normalized;
        averagePoint /= collision.contacts.Length;

        stuckPosition = averagePoint + averageNormal * stickDistance;
        
        rb.linearVelocity = Vector2.zero;
        StartCoroutine(SetKinematicNextFrame());

        if (spriteRenderer != null)
        {
            spriteRenderer.color = stuckColor;
        }

        StartCoroutine(WallAttackSequence());
    }

    private IEnumerator SetKinematicNextFrame()
    {
        yield return new WaitForFixedUpdate();
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private IEnumerator WallAttackSequence()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < wallStickDuration && isStuckToWall)
        {
            ShootCircularBulletWave(0f);
            yield return new WaitForSeconds(timeBetweenWaves);
            
            ShootCircularBulletWave(angleBetweenBullets / 2f);
            yield return new WaitForSeconds(timeBetweenAttacks);
            
            elapsedTime += timeBetweenWaves + timeBetweenAttacks;
        }

        LeaveWall();
    }

    private void ShootCircularBulletWave(float angleOffset)
    {
        if (bulletPool == null) return;

        List<float> validAngles = GetValidShootingAngles(angleOffset);
        if (validAngles.Count == 0) return;

        foreach (float angle in validAngles)
        {
            Vector2 direction = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            ).normalized;

            bulletPool.SpawnBullet(transform.position, direction * bulletSpeed, bulletDamage, bulletLifetime);
        }

        if (audioSource != null && shootSound != null)
        {
            audioSource.PlayOneShot(shootSound, 0.5f);
        }
    }

    private List<float> GetValidShootingAngles(float angleOffset)
    {
        List<float> validAngles = new List<float>();
        int totalAngles = Mathf.CeilToInt(360f / angleBetweenBullets);

        for (int i = 0; i < totalAngles; i++)
        {
            float angle = (i * angleBetweenBullets) + angleOffset;
            Vector2 direction = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            ).normalized;

            if (!IsWallInDirection(direction))
            {
                validAngles.Add(angle);
            }
        }
        return validAngles;
    }

    private bool IsWallInDirection(Vector2 direction)
    {
        Vector2 rayOrigin = (Vector2)transform.position + (direction.normalized * 0.3f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, direction, wallCheckDistance, wallLayers);
        return hit.collider != null;
    }

    // --- GAP FINDING LOGIC PRESERVED ---
    private Vector2 GetBestFiringDirection()
    {
        int rayCount = 36;
        List<float> clearAngles = new List<float>();

        for (int i = 0; i < rayCount; i++)
        {
            float angle = (360f / rayCount) * i;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            if (!IsWallInDirection(direction)) clearAngles.Add(angle);
        }

        if (clearAngles.Count == 0) return Random.insideUnitCircle.normalized;

        float largestGapCenter = FindLargestGapCenter(clearAngles);
        return new Vector2(Mathf.Cos(largestGapCenter * Mathf.Deg2Rad), Mathf.Sin(largestGapCenter * Mathf.Deg2Rad)).normalized;
    }

    private float FindLargestGapCenter(List<float> clearAngles)
    {
        if (clearAngles.Count == 0) return 0f;
        if (clearAngles.Count == 1) return clearAngles[0];

        clearAngles.Sort();
        float maxGapSize = 0f;
        float maxGapStart = clearAngles[0];
        float maxGapEnd = clearAngles[0];
        float currentGapStart = clearAngles[0];
        
        for (int i = 1; i < clearAngles.Count; i++)
        {
            float angleDiff = clearAngles[i] - clearAngles[i - 1];
            if (angleDiff > angleBetweenBullets * 2)
            {
                float gapSize = clearAngles[i - 1] - currentGapStart;
                if (gapSize > maxGapSize)
                {
                    maxGapSize = gapSize;
                    maxGapStart = currentGapStart;
                    maxGapEnd = clearAngles[i - 1];
                }
                currentGapStart = clearAngles[i];
            }
        }

        float finalGapSize = clearAngles[clearAngles.Count - 1] - currentGapStart;
        if (finalGapSize > maxGapSize)
        {
            maxGapStart = currentGapStart;
            maxGapEnd = clearAngles[clearAngles.Count - 1];
        }

        return (maxGapStart + maxGapEnd) / 2f;
    }

    private void LeaveWall()
    {
        isStuckToWall = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
        currentSpeed = moveSpeed;
        UpdateMoveDirection();
        hasShield = true;
        currentShieldHealth = shieldHealth;
        UpdateShieldVisual();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    private void AttemptAttackPlayer(GameObject player)
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null && !playerHealth.IsDead() && !playerHealth.IsOnDamageCooldown())
            {
                playerHealth.TakeDamage(attackDamage, transform.position);
                lastAttackTime = Time.time;
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        if (hasShield)
        {
            currentShieldHealth -= damage;
            if (currentShieldHealth <= 0) BreakShield();
            else TriggerFlash();
        }
        else
        {
            currentHealth -= damage;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            TriggerFlash();
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            if (currentHealth <= 0) Die();
        }

        if (audioSource != null && damageSound != null) audioSource.PlayOneShot(damageSound);
    }

    private void TriggerFlash()
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashDamage());
    }

    private void BreakShield()
    {
        hasShield = false;
        currentShieldHealth = 0;
        UpdateShieldVisual();
        if (audioSource != null && shieldBreakSound != null) audioSource.PlayOneShot(shieldBreakSound);
        OnShieldBroken?.Invoke();
    }

    private void UpdateShieldVisual()
    {
        if (shieldVisual != null) shieldVisual.SetActive(hasShield);
        if (spriteRenderer != null)
        {
            if (hasShield) spriteRenderer.color = Color.Lerp(originalColor, shieldColor, 0.3f);
            else if (isStuckToWall) spriteRenderer.color = stuckColor;
            else spriteRenderer.color = originalColor;
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        StopAllCoroutines();
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        DropCoins();
        if (audioSource != null && deathSound != null) audioSource.PlayOneShot(deathSound);
        OnDeath?.Invoke();
        Destroy(gameObject, 2f);
    }

    private void DropCoins()
    {
        if (coinPrefab == null) return;
        int coinCount = Random.Range(minCoins, maxCoins + 1);
        for (int i = 0; i < coinCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * coinSpreadRadius;
            GameObject coin = Instantiate(coinPrefab, transform.position + (Vector3)randomOffset, Quaternion.identity);
            Rigidbody2D coinRb = coin.GetComponent<Rigidbody2D>();
            if (coinRb != null)
            {
                Vector2 randomForce = new Vector2(Random.Range(-coinDropForce, coinDropForce), Random.Range(coinDropForce * 0.5f, coinDropForce));
                coinRb.AddForce(randomForce, ForceMode2D.Impulse);
            }
        }
    }

    private IEnumerator FlashDamage()
    {
        for (int i = 0; i < 3; i++)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            if (whiteSpriteRenderer != null) whiteSpriteRenderer.enabled = true;
            yield return new WaitForSeconds(0.07f);
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            if (whiteSpriteRenderer != null) whiteSpriteRenderer.enabled = false;
            yield return new WaitForSeconds(0.07f);
        }
    }

    // --- DEBUG GIZMOS PRESERVED ---
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
        if (isStuckToWall)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)moveDirection * 2f);
        }
    }

    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public bool HasShield() => hasShield;
    public int GetCurrentShieldHealth() => currentShieldHealth;
    public int GetMaxShieldHealth() => shieldHealth;
    public bool IsDead() => isDead;
    public GameObject GetGameObject() => gameObject;
}