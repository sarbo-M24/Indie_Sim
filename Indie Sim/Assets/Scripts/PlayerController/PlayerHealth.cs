using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Coin-Based Health Settings")]
    [SerializeField] private int coinsLostPerHit = 100;
    [SerializeField] private bool useCoinsAsHealth = true;

    [Header("Coin Drop Visual Feedback")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int coinsToSpawnOnDamage = 10;
    [SerializeField] private float coinSpawnRadius = 2f;
    [SerializeField] private float coinSpawnForce = 5f;
    [SerializeField] private float coinLifetime = 2f;

    [Header("Damage Cooldown Settings")]
    public float damageCooldown = 1f;
    private float nextDamageTime = 0f;

    [Header("Visual Feedback Settings")]
    public float flashSpeed = 0.1f;
    public float flashDuration = 1f;

    [Header("Knockback Settings")]
    public float knockbackForce = 5f;
    public float knockbackDuration = 0.2f;

    [Header("Invincibility (Debug)")]
    [Tooltip("While true the player ignores all damage and cannot be killed (incl. the dungeon timer).")]
    [SerializeField] private bool invincible = false;
    [Tooltip("Press to toggle invincibility. Works in the build too. Set to None to disable.")]
    [SerializeField] private KeyCode invincibilityToggleKey = KeyCode.I;
    [Tooltip("Allow the toggle key even in a non-development build.")]
    [SerializeField] private bool enableToggleInBuild = true;

    public bool IsInvincible => invincible;
    public void SetInvincible(bool value) => invincible = value;

    // Separate from `invincible` (the manual debug toggle) so dashing never fights the debug key.
    private bool isDashInvulnerable;
    public void SetDashInvulnerable(bool value) => isDashInvulnerable = value;

    [Header("Death Settings")]
    public Sprite deathSprite;
    public float deathDelay = 3f;
    public GameObject deathUIPanel;
    public Sprite aliveSprite;

    [Header("UI Reference")]
    public HealthHeartBar healthHeartBar;

    [Header("Damage Indicator")]
    [SerializeField] private DamageIndicator damageIndicator;

    [Header("Hit Flash")]
    [SerializeField] private Color hitFlashColor = Color.white; // Change to any color in Inspector
    [SerializeField] private float hitFlashDuration = 0.08f;    // How long the color stays on

    [Header("Hit Particle")]
    [SerializeField] private ParticleSystem hitParticleEffect;  // Drag a child ParticleSystem here

    [Header("References")]
    private SpriteRenderer spriteRenderer;
    private Collider2D playerCollider;
    private Rigidbody2D rb;
    private Color originalColor;
    private bool isDead = false;
    private bool isFlashing = false;
    private PlayerController playerController;
    private SimplePlayerRotation playerRotation;
    private PlayerAutoAimShooter playerAutoAimShooter;
    private PlayerConeShooter playerConeShooter;
    private PlayerAnimationController playerAnimationController;

    private Coroutine hitFlashCoroutine;

    void Start()
    {
        // Scene-local (Phase 6) — this object is now destroyed and recreated
        // fresh every scene load (RoguelikeMode baked instance recreated on
        // reload, BossArena instance runtime-spawned by PlayerSpawner), so
        // resolving deathUIPanel once here in Start() is sufficient; the old
        // per-scene-load re-acquisition subscription is no longer needed —
        // there's no surviving instance left to go stale.
        RetryButton retryButton = FindFirstObjectByType<RetryButton>(FindObjectsInactive.Include);
        if (retryButton != null && retryButton.DeathPanel != null)
        {
            deathUIPanel = retryButton.DeathPanel;
            Debug.Log($"[PlayerHealth] Acquired deathUIPanel in scene '{gameObject.scene.name}': {deathUIPanel.name}");
        }
        else if (retryButton != null)
        {
            Debug.LogWarning($"[PlayerHealth] RetryButton found in scene '{gameObject.scene.name}' but its DeathPanel is unassigned in the Inspector — deathUIPanel may be stale!");
        }
        else
        {
            Debug.LogWarning($"[PlayerHealth] No RetryButton found in scene '{gameObject.scene.name}' — deathUIPanel may be stale!");
        }

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        playerController = GetComponent<PlayerController>();
        playerCollider = GetComponent<Collider2D>();
        playerRotation = GetComponent<SimplePlayerRotation>();
        playerAutoAimShooter = GetComponent<PlayerAutoAimShooter>();
        playerConeShooter = GetComponent<PlayerConeShooter>();
        playerAnimationController = GetComponent<PlayerAnimationController>();
        rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            Debug.Log("SpriteRenderer found on child object");
        }
        else
        {
            Debug.LogError("SpriteRenderer not found! Make sure the player sprite child has a SpriteRenderer component.");
        }

        if (deathUIPanel != null)
            deathUIPanel.SetActive(false);

        if (damageIndicator == null)
            damageIndicator = GetComponent<DamageIndicator>();

        // Make sure the particle doesn't auto-play on start
        if (hitParticleEffect != null && hitParticleEffect.main.playOnAwake)
            hitParticleEffect.Stop();

        Debug.Log($"Player initialized with coin-based health system. Coins per hit: {coinsLostPerHit}");
    }

    private void Update()
    {
        if (invincibilityToggleKey == KeyCode.None) return;
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        if (!enableToggleInBuild) return;
#endif
        if (Input.GetKeyDown(invincibilityToggleKey))
        {
            invincible = !invincible;
            Debug.Log($"[PlayerHealth] Invincibility {(invincible ? "ON" : "OFF")} (key '{invincibilityToggleKey}').");
        }
    }

    public void TakeDamage(int damage, Vector3 enemyPosition)
    {
        if (invincible || isDashInvulnerable)
        {
            Debug.Log("[PlayerHealth] Damage ignored - invincible.");
            return;
        }

        if (Time.time < nextDamageTime || isDead)
        {
            Debug.Log("Player is on damage cooldown - cannot take damage yet");
            return;
        }

        if (useCoinsAsHealth)
        {
            if (CoinManager.Instance == null)
            {
                Debug.LogError("CoinManager not found! Cannot process coin-based damage.");
                return;
            }

            int currentCoins = CoinManager.Instance.GetCurrentCoins();

            if (currentCoins <= 0)
            {
                Die();
                return;
            }

            int coinsToLose = Mathf.Min(coinsLostPerHit, currentCoins);
            CoinManager.Instance.SpendCoins(coinsToLose);

            Debug.Log($"Player hit! Lost {coinsToLose} coins. Remaining: {CoinManager.Instance.GetCurrentCoins()}");

            SpawnCoinDropVisual(enemyPosition);

            // Screen flash (DamageIndicator)
            if (damageIndicator != null)
                damageIndicator.TriggerDamageFlash();

            // Sprite color hit flash
            if (hitFlashCoroutine != null)
                StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitColorFlash());

            // Particle burst
            if (hitParticleEffect != null)
                hitParticleEffect.Play();

            nextDamageTime = Time.time + damageCooldown;

            ApplyKnockback(enemyPosition);

            if (CoinManager.Instance.GetCurrentCoins() <= 0)
            {
                Die();
            }
            else
            {
                if (!isFlashing)
                    StartCoroutine(FlashEffect());
            }
        }
        else
        {
            Debug.LogWarning("Coin-based health is disabled. Enable it in Inspector.");
        }
    }

    /// <summary>
    /// Briefly tints the player sprite to hitFlashColor, then restores it.
    /// Runs independently of the blink loop so both can coexist.
    /// </summary>
    private IEnumerator HitColorFlash()
    {
        if (spriteRenderer == null) yield break;
        spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        // Only restore if the blink loop hasn't taken over (it manages color itself)
        if (!isFlashing && spriteRenderer != null)
            spriteRenderer.color = originalColor;
        hitFlashCoroutine = null;
    }

    private void SpawnCoinDropVisual(Vector3 damageSourcePosition)
    {
        if (coinPrefab == null)
        {
            Debug.LogWarning("Coin prefab not assigned! Cannot spawn visual feedback.");
            return;
        }

        for (int i = 0; i < coinsToSpawnOnDamage; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 spawnOffset = new Vector2(
                Mathf.Cos(angle) * coinSpawnRadius,
                Mathf.Sin(angle) * coinSpawnRadius
            );
            Vector3 spawnPosition = transform.position + (Vector3)spawnOffset;

            GameObject coin = Instantiate(coinPrefab, spawnPosition, Quaternion.identity);

            Coin coinScript = coin.GetComponent<Coin>();
            if (coinScript != null) coinScript.enabled = false;

            Rigidbody2D coinRb = coin.GetComponent<Rigidbody2D>();
            if (coinRb != null)
            {
                Vector2 forceDirection = ((Vector2)spawnPosition - (Vector2)damageSourcePosition).normalized;
                coinRb.AddForce(forceDirection * coinSpawnForce, ForceMode2D.Impulse);
            }

            Destroy(coin, coinLifetime);
        }

        Debug.Log($"Spawned {coinsToSpawnOnDamage} visual coins");
    }

    private void ApplyKnockback(Vector3 enemyPosition)
    {
        if (rb == null) return;
        Vector2 knockbackDirection = (transform.position - enemyPosition).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);
        Debug.Log($"Knockback applied in direction: {knockbackDirection}");
    }

    private IEnumerator FlashEffect()
    {
        isFlashing = true;
        Debug.Log("Player flashing effect started (damage cooldown active)");

        float elapsedTime = 0f;
        while (elapsedTime < flashDuration)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.3f);
            yield return new WaitForSeconds(flashSpeed);

            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(flashSpeed);

            elapsedTime += flashSpeed * 2;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;

        isFlashing = false;
        Debug.Log("Flashing effect ended");
    }

    /// <summary>
    /// Kills the player outright, bypassing coin-health. Used by the dungeon
    /// timer in RoguelikeManager when time runs out.
    /// </summary>
    public void KillPlayer()
    {
        if (invincible)
        {
            Debug.Log("[PlayerHealth] KillPlayer ignored - invincible.");
            return;
        }
        Die();
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log($"[PlayerHealth] Die() called on {gameObject.name} (instance {GetInstanceID()}) in scene '{gameObject.scene.name}'. deathUIPanel currently: {(deathUIPanel != null ? deathUIPanel.name : "NULL")}");

        StopAllCoroutines();

        if (playerController != null) playerController.enabled = false;
        if (playerRotation != null) playerRotation.enabled = false;
        if (playerAutoAimShooter != null) playerAutoAimShooter.enabled = false;
        if (playerConeShooter != null) playerConeShooter.enabled = false;
        // Disabling the controller alone only stops it from updating the
        // IsMoving parameter — the Animator component itself keeps evaluating
        // its current state and re-writing sprite keyframes every frame
        // regardless, which was overwriting deathSprite/aliveSprite the
        // moment the player moved again after retry. Disable both.
        if (playerAnimationController != null)
        {
            playerAnimationController.enabled = false;
            if (playerAnimationController.Animator != null)
                playerAnimationController.Animator.enabled = false;
        }

        if (spriteRenderer != null) spriteRenderer.color = originalColor;
        if (deathSprite != null && spriteRenderer != null) spriteRenderer.sprite = deathSprite;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (playerCollider != null) playerCollider.enabled = false;

        StatTracker statTracker = FindObjectOfType<StatTracker>();
        if (statTracker != null) statTracker.ShowDeathStats();

        StartCoroutine(ShowDeathUI());
    }

    private IEnumerator ShowDeathUI()
    {
        yield return new WaitForSeconds(deathDelay);

        if (deathUIPanel != null)
        {
            deathUIPanel.SetActive(true);
            Time.timeScale = 0f;
            Debug.Log($"[PlayerHealth] Death UI shown: {deathUIPanel.name}");
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] ShowDeathUI() ran but deathUIPanel is NULL — no Retry UI will appear!");
        }
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        GameManager.Instance.ReturnToMainMenu();
        Debug.Log("Going to main menu");
    }

    public int GetCurrentHealth()
    {
        if (useCoinsAsHealth && CoinManager.Instance != null)
            return CoinManager.Instance.GetCurrentCoins();
        return 0;
    }

    public int GetMaxHealth()
    {
        if (useCoinsAsHealth && CoinManager.Instance != null)
            return CoinManager.Instance.GetTotalCoinsEverCollected();
        return 100;
    }

    public bool IsDead() { return isDead; }
    public bool IsOnDamageCooldown() { return Time.time < nextDamageTime; }
    public int GetCoinsLostPerHit() { return coinsLostPerHit; }
}