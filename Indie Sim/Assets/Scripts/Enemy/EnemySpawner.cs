using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnableEnemy
{
    public GameObject prefab;
    public int budgetCost;
    public float spawnWeight;
    public int maxAllowed = 0; // 0 = infinite
    [HideInInspector] public int currentSpawned = 0;
    [Tooltip("How many to spawn at once. 1 = single. Use 3-4 for fodder enemies.")]
    public int spawnGroupSize = 1;
}

public class EnemySpawner : MonoBehaviour, IDamageable
{
    [Header("Spawner Economy (The Piñata)")]
    public int startingBudget = 300;
    private int currentBudget;
    public float spawnInterval = 1.5f;
    private float nextSpawnTime;

    [Header("Enemy Roster (Set Weights & Costs)")]
    public SpawnableEnemy fodderLevel1;
    public SpawnableEnemy fodderLevel2;
    public SpawnableEnemy fodderLevel3;
    public SpawnableEnemy rangedEnemy;

    [Header("Cthulhu Eye Settings")]
    public SpawnableEnemy cthulhuEye;
    public float cthulhuExclusionRadius = 25f;
    private static HashSet<Vector3> globalCthulhuLocations = new HashSet<Vector3>();

    [Header("Coin Rewards")]
    public GameObject coinPrefab;
    public float coinRewardMultiplier = 0.5f;
    public float coinDropForce = 2f;

    [Header("Health & Visuals")]
    public int maxHealth = 100;
    private int currentHealth;
    public float flashDuration = 0.15f;
    public Color flashColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isFlashing = false;
    private bool isDead = false;

    [Header("Spawning Logistics")]
    public float spawnRadius = 5f;
    [Tooltip("How far from center the zone starts")]
    public float zoneOffset = 1.5f; 
    public LayerMask wallLayer;
    public bool requiresActivation = true;
    private bool isActivated = false;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip activationSound;
    public AudioClip damageSound;
    public AudioClip deathSound;

    [Header("Explode Effect")]
    public BloodSplatterEffect explodeEffect; // Reused blood-splatter system, standing in for a spawner "explosion"
    private Transform playerTransform;

    public System.Action<int, int> OnHealthChanged;
    public System.Action OnDeath;
    public System.Action OnActivated;

    private void Start()
    {
        currentBudget = startingBudget;
        currentHealth = maxHealth;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) playerTransform = playerObject.transform;

        if (explodeEffect == null) explodeEffect = FindConfiguredExplodeEffect();

        if (!requiresActivation) ActivateSpawner();
    }

    private void Update()
    {
        if (isDead || !isActivated) return;

        if (Time.time >= nextSpawnTime && currentBudget > 0)
        {
            AttemptSpawn();
        }

        if (currentBudget <= 0)
        {
            Die();
        }
    }

    public void ActivateSpawner()
    {
        if (isActivated || isDead) return;

        isActivated = true;
        if (audioSource && activationSound) audioSource.PlayOneShot(activationSound);
        if (spriteRenderer) StartCoroutine(ActivationFlash());

        OnActivated?.Invoke();
    }

    private void AttemptSpawn()
    {
        SpawnableEnemy chosenEnemy = PickEnemyByWeight();

        if (chosenEnemy != null)
        {
            // Determine how many to spawn this wave.
            // Group size only applies if budget can cover the full group;
            // otherwise clamp to however many we can afford.
            int groupSize = chosenEnemy.spawnGroupSize;
            int affordable = (chosenEnemy.budgetCost > 0)
                ? Mathf.Min(groupSize, currentBudget / chosenEnemy.budgetCost)
                : groupSize;
            groupSize = Mathf.Max(1, affordable);

            // Also respect maxAllowed cap
            if (chosenEnemy.maxAllowed > 0)
                groupSize = Mathf.Min(groupSize, chosenEnemy.maxAllowed - chosenEnemy.currentSpawned);

            if (groupSize <= 0) return;

            for (int i = 0; i < groupSize; i++)
            {
                Vector2 safePosition = GetValidSpawnPosition();
                if (safePosition == (Vector2)transform.position) continue;

                currentBudget -= chosenEnemy.budgetCost;
                chosenEnemy.currentSpawned++;

                if (chosenEnemy == cthulhuEye)
                    globalCthulhuLocations.Add(transform.position);

                Instantiate(chosenEnemy.prefab, safePosition, Quaternion.identity);
            }

            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    private SpawnableEnemy PickEnemyByWeight()
    {
        List<SpawnableEnemy> validEnemies = new List<SpawnableEnemy>();
        float totalWeight = 0f;

        SpawnableEnemy[] allEnemies = { fodderLevel1, fodderLevel2, fodderLevel3, rangedEnemy, cthulhuEye };

        foreach (var enemy in allEnemies)
        {
            if (enemy.prefab == null) continue;

            bool canAfford = enemy.budgetCost <= currentBudget;
            bool underCap = enemy.maxAllowed == 0 || enemy.currentSpawned < enemy.maxAllowed;

            bool isCthulhuSafe = true;
            if (enemy == cthulhuEye) isCthulhuSafe = !IsCthulhuTooClose();

            if (canAfford && underCap && isCthulhuSafe)
            {
                validEnemies.Add(enemy);
                totalWeight += enemy.spawnWeight;
            }
        }

        if (validEnemies.Count == 0) return null;

        float randomVal = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var enemy in validEnemies)
        {
            cumulative += enemy.spawnWeight;
            if (randomVal <= cumulative) return enemy;
        }

        return null;
    }

    #region Physics & Wall Detection
    private Vector2 GetValidSpawnPosition()
    {
        Vector2[] zoneCenters = {
            (Vector2)transform.position + Vector2.up * zoneOffset,
            (Vector2)transform.position + Vector2.down * zoneOffset,
            (Vector2)transform.position + Vector2.left * zoneOffset,
            (Vector2)transform.position + Vector2.right * zoneOffset
        };

        List<Vector2> validZones = new List<Vector2>();
        Vector2 zoneSize = transform.localScale * 0.95f;

        foreach (Vector2 center in zoneCenters)
        {
            Collider2D hit = Physics2D.OverlapBox(center, zoneSize, 0f, wallLayer);
            if (hit == null)
            {
                validZones.Add(center);
            }
        }

        if (validZones.Count == 0) return transform.position;

        return validZones[Random.Range(0, validZones.Count)];
    }

    private bool IsCthulhuTooClose()
    {
        foreach (Vector3 pos in globalCthulhuLocations)
        {
            if (Vector3.Distance(transform.position, pos) < cthulhuExclusionRadius)
                return true;
        }
        return false;
    }
    public static void ResetCthulhuEyeTracking() => globalCthulhuLocations.Clear();
    #endregion

    #region Piñata & Health System
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (!isFlashing) StartCoroutine(FlashWhite());
        if (audioSource && damageSound) audioSource.PlayOneShot(damageSound);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0) Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (audioSource && deathSound) audioSource.PlayOneShot(deathSound);
        OnDeath?.Invoke();

        DropCoins();
        StartCoroutine(DeathSequence());

        // Last, so a broken cosmetic effect can't stop the coins/death sequence above.
        SpawnExplodeEffect();
    }

    private void DropCoins()
    {
        if (coinPrefab == null || currentBudget <= 0) return;

        int coinsToDrop = Mathf.RoundToInt(currentBudget * coinRewardMultiplier);

        for (int i = 0; i < coinsToDrop; i++)
        {
            GameObject coin = Instantiate(coinPrefab, transform.position, Quaternion.identity);

            Rigidbody2D rb = coin.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 gentlePush = Random.insideUnitCircle * coinDropForce;
                rb.AddForce(gentlePush, ForceMode2D.Impulse);
            }
        }
    }

    /// <summary>
    /// Fallback when explodeEffect isn't assigned: any BloodSplatterEffect in the scene that
    /// actually has splatter prefabs, so an unconfigured one can't be picked by accident.
    /// </summary>
    private static BloodSplatterEffect FindConfiguredExplodeEffect()
    {
        foreach (BloodSplatterEffect effect in FindObjectsByType<BloodSplatterEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (HasSplatterPrefabs(effect)) return effect;
        return null;
    }

    private static bool HasSplatterPrefabs(BloodSplatterEffect effect) =>
        effect.bloodSplatterPrefabs != null && effect.bloodSplatterPrefabs.Length > 0;

    /// <summary>
    /// Reuses BloodSplatterEffect to stand in for a spawner "explode" animation on death.
    /// </summary>
    private void SpawnExplodeEffect()
    {
        // The borrowed effect usually lives on an enemy, which may have died since Start.
        if (explodeEffect == null || !HasSplatterPrefabs(explodeEffect)) explodeEffect = FindConfiguredExplodeEffect();

        if (explodeEffect == null)
        {
            Debug.LogWarning($"[EnemySpawner] '{gameObject.name}': No BloodSplatterEffect assigned/found. Skipping explode effect.");
            return;
        }

        Debug.Log($"[EnemySpawner] '{gameObject.name}': Firing explode effect at {transform.position}.");

        if (playerTransform != null)
        {
            explodeEffect.SpawnBloodSplatter(transform.position, playerTransform.position);
        }
        else
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            explodeEffect.SpawnBloodSplatter(transform.position, randomDirection);
        }
    }

    private IEnumerator DeathSequence()
    {
        if (!isFlashing) StartCoroutine(FlashWhite());
        yield return new WaitForSeconds(flashDuration);
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = disabledColor;
            originalColor = disabledColor;

            // The blood/explode effect renders at sortingOrder -10 on this same sorting
            // layer. Normal enemies reveal it because their corpse sprite goes fully
            // transparent on death; this spawner instead stays opaque and grey, so without
            // dropping below the effect's order here it just paints over the VFX forever.
            spriteRenderer.sortingOrder = -20;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Debug.Log("[EnemySpawner] Spawner Disabled and turned Grey.");
    }
    #endregion

    #region Visual Effects
    private IEnumerator ActivationFlash()
    {
        spriteRenderer.color = Color.yellow;
        yield return new WaitForSeconds(0.3f);
        spriteRenderer.color = originalColor;
    }

    private IEnumerator FlashWhite()
    {
        if (spriteRenderer == null) yield break;
        isFlashing = true;
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
        isFlashing = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 zoneSize = transform.localScale * 0.95f;
        Gizmos.DrawWireCube((Vector2)transform.position + Vector2.up * zoneOffset, zoneSize);
        Gizmos.DrawWireCube((Vector2)transform.position + Vector2.down * zoneOffset, zoneSize);
        Gizmos.DrawWireCube((Vector2)transform.position + Vector2.left * zoneOffset, zoneSize);
        Gizmos.DrawWireCube((Vector2)transform.position + Vector2.right * zoneOffset, zoneSize);

        if (cthulhuEye != null && cthulhuEye.prefab != null)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, cthulhuExclusionRadius);
        }
    }
    #endregion

    public bool IsDead() => isDead;
    public GameObject GetGameObject() => gameObject;

    #region Legacy API Bridge
    public bool RequiresActivation() { return requiresActivation; }
    public bool IsActivated() { return isActivated; }

    public int maxEnemies = 8;
    public void SetDungeonGenerator(MonoBehaviour generator) { }

    public void SetDungeonLevel(int level) { UpdateDifficultyForLevel(level); }

    public void UpdateDifficultyForLevel(int currentLevel)
    {
        if (currentLevel <= 1) return;
        startingBudget += (currentLevel * 50);
        if (currentBudget > 0 && currentHealth == maxHealth) currentBudget = startingBudget;
        spawnInterval = Mathf.Max(0.5f, spawnInterval - (currentLevel * 0.15f));
    }
    #endregion
}