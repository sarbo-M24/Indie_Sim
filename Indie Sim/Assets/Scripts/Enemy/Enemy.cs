using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Enemy Settings")]
    public int maxHealth = 100;
    public float knockbackStrength = 10f; // Adjust this value to control knockback strength
    public GameObject deathEffect;
    public GameObject damageNumberPrefab; // Optional floating damage numbers

    [Header("Visual Feedback")]
    public float flashDuration = 0.1f;
    public Color damageColor = Color.white;

    [Header("Blood Splatter Effect")]
    public BloodSplatterEffect bloodEffect; // Reference to blood effect manager
    private Transform playerTransform; // Reference to player for blood direction

    [Header("Loot Drop")]
    [SerializeField] private CoinDrop coinDrop = new CoinDrop { dropChance = 0.3f, maxCoins = 2 };

    [Header("Attack Settings")]
    public int attackDamage = 10;
    public float attackCooldown = 1.5f; // Time between attacks
    private float lastAttackTime = 0f;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip deathSound;

    [Header("Damage Flash Settings")]
    [SerializeField] private Sprite damageSprite; // The sprite to show when damaged
    private Sprite originalSprite; // Stores the normal sprite


    // Private variables
    private int currentHealth;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Coroutine flashCoroutine;
    private bool isDead = false;
    private EnemyMovement enemyMovement;

    // Events
    public System.Action OnDeath;
    public System.Action<int, int> OnHealthChanged; // current, max

    void Start()
    {
        InitializeEnemy();
    }

    private void InitializeEnemy()
    {
        // Initialize health
        currentHealth = maxHealth;

        // Get components
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        enemyMovement = GetComponent<EnemyMovement>();

        // Store original color for flash effect
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        // Find the player reference for blood splatter direction
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning($"Enemy '{gameObject.name}': No GameObject with 'Player' tag found. Blood splatter direction will not work.");
        }

        // If bloodEffect not assigned, try to find it in the scene
        if (bloodEffect == null)
        {
            bloodEffect = FindObjectOfType<BloodSplatterEffect>();
            if (bloodEffect == null)
            {
                Debug.LogWarning($"Enemy '{gameObject.name}': No BloodSplatterEffect found in scene. Create a GameObject with BloodSplatterEffect script.");
            }
        }

        // Ensure collider is set up correctly for cone detection
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = false; // Set to false for solid collision
        }

        Debug.Log($"Enemy '{gameObject.name}' initialized with {maxHealth} health");
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Check if touching player
        if (collision.gameObject.CompareTag("Player"))
        {
            // Check if cooldown has passed
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                // Attack the player
                PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(attackDamage, transform.position);
                    lastAttackTime = Time.time;
                    Debug.Log($"Enemy attacked player for {attackDamage} damage");
                }
            }
        }
    }

    #region IDamageable Interface Implementation

    // INTERFACE METHOD: Must match exactly - public void TakeDamage(int damage)
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        // Reduce health
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Show damage number (optional)
        if (damageNumberPrefab != null)
        {
            GameObject damageNumber = Instantiate(damageNumberPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);

            // If the damage number has a Text component, set the damage value
            UnityEngine.UI.Text damageText = damageNumber.GetComponentInChildren<UnityEngine.UI.Text>();
            if (damageText != null)
            {
                damageText.text = damage.ToString();
            }

            // If using TextMeshPro instead
            TMPro.TextMeshProUGUI tmpText = damageNumber.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.text = damage.ToString();
            }
        }

       

        // Flash red when hit
        if (spriteRenderer != null)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            flashCoroutine = StartCoroutine(FlashDamage());


            if (squishCoroutine != null)
            {
                StopCoroutine(squishCoroutine);
            }
            squishCoroutine = StartCoroutine(SquishEffect());
        }


        // Play damage sound
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }

        // Notify health change
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        //Apply knockback when enemy takes damage
        // Calculate knockback direction (away from player)
        Vector2 knockbackDirection = (transform.position - playerTransform.position).normalized;
        // Multiply direction by knockback strength to create the force

        Vector2 knockbackForce = knockbackDirection * knockbackStrength;
        enemyMovement.ApplyKnockback(knockbackForce);

        Debug.Log($"Enemy '{gameObject.name}' took {damage} damage. Health: {currentHealth}/{maxHealth}");

        // Check if dead
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // INTERFACE METHOD: Must match exactly - public bool IsDead()
    public bool IsDead()
    {
        return isDead;
    }

    // INTERFACE METHOD: Must match exactly - public GameObject GetGameObject()
    public GameObject GetGameObject()
    {
        return gameObject;
    }

    #endregion

    #region Health System

    public void Heal(int healAmount)
    {
        if (isDead) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Debug.Log($"Enemy '{gameObject.name}' healed {healAmount}. Health: {currentHealth}/{maxHealth}");
    }

    public void SetMaxHealth(int newMaxHealth)
    {
        maxHealth = newMaxHealth;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;

        // ** KILL TRACKING INTEGRATION **
        // Register this enemy death with the kill tracker
        if (EnemyKillTracker.Instance != null)
        {
            EnemyKillTracker.Instance.RegisterEnemyKill();
        }
        else
        {
            Debug.LogWarning($"Enemy '{gameObject.name}': EnemyKillTracker not found. Kill not recorded.");
        }
        // ** END KILL TRACKING **

        Sfx.Play(SfxId.EnemyDeath);

        // PLAY BLOOD SPLATTER EFFECT ON DEATH
        SpawnBloodSplatterOnDeath();

        // Fodder only drops coins on a low chance (coinDrop.dropChance).
        coinDrop.Drop(transform.position);

        // Notify death (important for spawner tracking)
        OnDeath?.Invoke();

        Debug.Log($"Enemy '{gameObject.name}' died!");

        // Add score, drop items, etc. here
        // Example: GameManager.Instance.AddScore(100);

        // Let EnemyDeath script handle everything
        EnemyDeath deathHandler = GetComponent<EnemyDeath>();
        if (deathHandler != null)
        {
            deathHandler.HandleDeath();
        }
        else
        {
            // Fallback if no EnemyDeath script
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Spawns blood splatter effect when enemy dies
    /// Blood sprays away from the player (direction from player to enemy)
    /// </summary>
    private void SpawnBloodSplatterOnDeath()
    {
        // Check if blood effect system is set up
        if (bloodEffect == null)
        {
            Debug.LogWarning($"Enemy '{gameObject.name}': Blood effect not assigned. Skipping blood splatter.");
            return;
        }

        // Check if player reference exists
        if (playerTransform != null)
        {
            // Spawn blood at enemy position, spraying away from player
            bloodEffect.SpawnBloodSplatter(transform.position, playerTransform.position);
            Debug.Log($"Enemy '{gameObject.name}': Blood splatter spawned at {transform.position}");
        }
        else
        {
            Debug.LogWarning($"Enemy '{gameObject.name}': Player reference not found. Blood splatter will not have correct direction.");

            // Fallback: spawn blood with random direction if no player found
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            bloodEffect.SpawnBloodSplatter(transform.position, randomDirection);
        }
    }

    #endregion

    #region Visual Effects

    #region Visual Effects

    IEnumerator FlashDamage()
    {
        if (spriteRenderer != null)
        {
            // Get the Animator component from the same GameObject as the sprite renderer
            Animator animator = spriteRenderer.GetComponent<Animator>();

            // Disable the animator to stop animation
            if (animator != null)
            {
                animator.enabled = false;
            }

            // Store the original sprite
            Sprite tempOriginalSprite = spriteRenderer.sprite;

            // Change to damage sprite
            spriteRenderer.sprite = damageSprite;
            yield return new WaitForSeconds(flashDuration);

            // Only restore sprite and re-enable animator if enemy is still alive
            if (!isDead)
            {
                spriteRenderer.sprite = tempOriginalSprite;

                // Re-enable the animator
                if (animator != null)
                {
                    animator.enabled = true;
                }
            }
        }
        flashCoroutine = null;
    }



    // NEW: Add this squish effect coroutine
    // Add this variable at the top of your class (with other variables)
    private Coroutine squishCoroutine = null;

    IEnumerator SquishEffect()
    {
        // Store the TRUE original scale at the START of this coroutine
        Vector3 originalScale = transform.localScale;

        // Squish parameters
        float squishAmount = 0.4f;
        float squishDuration = 0.2f;
        float bounceBackDuration = 0.1f;

        // Phase 1: SQUISH
        Vector3 squishScale = new Vector3(
            originalScale.x * (1 + squishAmount * 0.5f),
            originalScale.y * (1 - squishAmount),
            originalScale.z
        );

        float elapsed = 0f;

        while (elapsed < squishDuration)
        {
            float t = elapsed / squishDuration;
            transform.localScale = Vector3.Lerp(originalScale, squishScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = squishScale;

        // Phase 2: BOUNCE BACK with overshoot
        elapsed = 0f;
        Vector3 overshootScale = new Vector3(
            originalScale.x * 0.95f,
            originalScale.y * 1.05f,
            originalScale.z
        );

        while (elapsed < bounceBackDuration * 0.6f)
        {
            float t = elapsed / (bounceBackDuration * 0.6f);
            transform.localScale = Vector3.Lerp(squishScale, overshootScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Phase 3: Settle back to original
        elapsed = 0f;
        while (elapsed < bounceBackDuration * 0.4f)
        {
            float t = elapsed / (bounceBackDuration * 0.4f);
            transform.localScale = Vector3.Lerp(overshootScale, originalScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure we end at exactly original scale
        transform.localScale = originalScale;

        // Clear the coroutine reference
        squishCoroutine = null;
    }


    #endregion

    #endregion

    #region Public API

    // Public getters for other systems
    public int GetCurrentHealth() { return currentHealth; }
    public int GetMaxHealth() { return maxHealth; }
    public float GetHealthPercentage() { return (float)currentHealth / maxHealth; }
    public bool IsAtFullHealth() { return currentHealth >= maxHealth; }

    // Methods for gameplay systems
    public void SetHealth(int health)
    {
        currentHealth = Mathf.Clamp(health, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void InstantKill()
    {
        currentHealth = 0;
        Die();
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Draw health bar above enemy
        Vector3 healthBarPos = transform.position + Vector3.up * 1.5f;
        float healthPercentage = GetHealthPercentage();

        // Background (red)
        Gizmos.color = Color.red;
        Gizmos.DrawLine(healthBarPos - Vector3.right * 0.5f, healthBarPos + Vector3.right * 0.5f);

        // Health (green)
        Gizmos.color = Color.green;
        Vector3 healthEnd = healthBarPos + Vector3.right * (healthPercentage * 1f - 0.5f);
        Gizmos.DrawLine(healthBarPos - Vector3.right * 0.5f, healthEnd);

        // Dead indicator
        if (isDead)
        {
            Gizmos.color = Color.black;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }

    #endregion
}
