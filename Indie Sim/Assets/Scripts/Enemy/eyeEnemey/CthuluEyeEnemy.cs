using System.Collections;
using System.Collections.Generic; // Added for HashSet compatibility with other scripts if needed
using UnityEngine;

public class CthulhuEyeEnemy : MonoBehaviour, IDamageable
{
    [Header("Activation Settings")]
    [SerializeField] private float activationRadius = 20f;
    [SerializeField] private float checkInterval = 0.5f;
    [SerializeField] private float activationDelay = 2f; 
    [SerializeField] private bool showDebugGizmos = true;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float preferredDistance = 8f;
    [SerializeField] private float distanceTolerance = 1f;

    [Header("Attack Settings")]
    [SerializeField] private GameObject escapePentagramPrefab;
    [SerializeField] private GameObject prisonPentagramPrefab;
    [SerializeField] private float attackCooldown = 4f;
    [SerializeField] [Range(0f, 1f)] private float escapePentagramChance = 0.5f;

    [Header("Juice & Damage Settings")]
    [SerializeField] private Color damageFlashColor = Color.white;
    [SerializeField] private float damageJuiceDuration = 0.15f;
    [SerializeField] private float damageSquashScale = 0.8f; // Uniform squeeze (0.8 = 20% smaller)

    [Header("Color Fade Settings")]
    [SerializeField] private Color attackColor = Color.red;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0, 1, 1, 0); 

    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;

    [Header("Loot Drop")]
    [SerializeField] private CoinDrop coinDrop = new CoinDrop();

    [Header("References")]
    private Transform player;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propBlock;
    private bool isActivated = false;
    private bool canAttack = false; 
    private float nextAttackTime = 0f;
    private Vector3 originalScale;
    private Coroutine juiceCoroutine;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        propBlock = new MaterialPropertyBlock();

        if (spriteRenderer != null) originalScale = spriteRenderer.transform.localScale;

        currentHealth = maxHealth;
        
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        StartCoroutine(ActivationCheck());
    }

    void FixedUpdate()
    {
        if (!isActivated || player == null) 
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        MaintainDistanceFromPlayer();

        if (canAttack && Time.time >= nextAttackTime)
        {
            SpawnPentagram();
            nextAttackTime = Time.time + attackCooldown;
            StartCoroutine(ColorFadeRoutine());
        }
    }

    // --- DAMAGE JUICE LOGIC ---
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        
        if (juiceCoroutine != null) StopCoroutine(juiceCoroutine);
        juiceCoroutine = StartCoroutine(DamageJuiceRoutine());

        if (currentHealth <= 0) Die();
    }

    private IEnumerator DamageJuiceRoutine()
    {
        if (spriteRenderer == null) yield break;
        Transform sTransform = spriteRenderer.transform;

        float elapsed = 0f;
        
        // 1. Squash and Flash
        while (elapsed < damageJuiceDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / damageJuiceDuration;
            float pingPong = Mathf.Sin(t * Mathf.PI); // Creates 0 -> 1 -> 0 curve

            // Squeeze from all directions
            float currentScale = Mathf.Lerp(1f, damageSquashScale, pingPong);
            sTransform.localScale = originalScale * currentScale;

            // Flash Color
            spriteRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", Color.Lerp(Color.white, damageFlashColor, pingPong));
            spriteRenderer.SetPropertyBlock(propBlock);

            yield return null;
        }

        // Reset
        sTransform.localScale = originalScale;
        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_Color", Color.white);
        spriteRenderer.SetPropertyBlock(propBlock);
    }
    // -------------------------

    private IEnumerator ColorFadeRoutine()
    {
        if (spriteRenderer == null) yield break;
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, attackCooldown - 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curveValue = fadeCurve.Evaluate(t);
            
            spriteRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", Color.Lerp(Color.white, attackColor, curveValue));
            spriteRenderer.SetPropertyBlock(propBlock);
            yield return null;
        }

        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_Color", Color.white);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    private IEnumerator ActivationCheck()
    {
        while (true)
        {
            if (player != null)
            {
                float distanceToPlayer = Vector2.Distance(transform.position, player.position);
                
                if (distanceToPlayer <= activationRadius && !isActivated)
                {
                    isActivated = true;
                    canAttack = false;
                    StartCoroutine(ActivationDelayCoroutine());
                }
                else if (distanceToPlayer > activationRadius && isActivated)
                {
                    isActivated = false;
                    canAttack = false;
                    rb.linearVelocity = Vector2.zero;
                }
            }
            yield return new WaitForSeconds(checkInterval);
        }
    }

    private IEnumerator ActivationDelayCoroutine()
    {
        yield return new WaitForSeconds(activationDelay);
        if (isActivated) canAttack = true;
    }

    private void MaintainDistanceFromPlayer()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        Vector2 directionToPlayer = (player.position - transform.position).normalized;

        if (distanceToPlayer < preferredDistance - distanceTolerance)
            rb.linearVelocity = -directionToPlayer * moveSpeed;
        else if (distanceToPlayer > preferredDistance + distanceTolerance)
            rb.linearVelocity = directionToPlayer * moveSpeed;
        else
            rb.linearVelocity = Vector2.zero;
    }

    private void SpawnPentagram()
    {
        if (player == null) return;
        bool isEscapePentagram = Random.value < escapePentagramChance;
        GameObject prefabToSpawn = isEscapePentagram ? escapePentagramPrefab : prisonPentagramPrefab;
        if (prefabToSpawn == null) return;

        Instantiate(prefabToSpawn, player.position, Quaternion.identity);
    }

    public bool IsDead() => currentHealth <= 0;
    public GameObject GetGameObject() => gameObject;

    private bool isDead;

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        BloodSplatterEffect.SpawnDeathSplatter(transform.position);
        coinDrop.Drop(transform.position);
        Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, activationRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredDistance);

        if (Application.isPlaying && isActivated && player != null)
        {
            Gizmos.color = canAttack ? Color.red : Color.yellow;
            Gizmos.DrawLine(transform.position, player.position);
        }
    }
}