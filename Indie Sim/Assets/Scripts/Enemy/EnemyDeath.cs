using System.Collections;
using UnityEngine;

public class EnemyDeath : MonoBehaviour
{
    [Header("Corpse Settings")]
    [SerializeField] private Sprite corpseSprite;
    [SerializeField] private bool leaveCorpse = true;
    [SerializeField] private float corpseLifetime = 30f; // 0 = stays forever
    [SerializeField] private float corpseAlpha = 0.8f;
    
    [Header("Corpse Sorting Layer")]
    [SerializeField] private string corpseSortingLayer = "Floor";
    [SerializeField] private int corpseSortingOrder = 2;

    [Header("Death Effects")]
    [SerializeField] private GameObject deathEffect;
    [SerializeField] private float deathEffectDuration = 2f;

    [Header("Component Management")]
    [SerializeField] private bool disableAllScripts = true;
    [SerializeField] private MonoBehaviour[] scriptsToDisable; // Drag specific scripts here

    // Components we'll cache
    private SpriteRenderer spriteRenderer;
    private Collider2D enemyCollider;

    private void Awake()
    {
        // Cache components
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// Call this method when the enemy dies
    /// </summary>
    public void HandleDeath()
    {
        // The death sound is the shared Sfx EnemyDeath, played by Enemy / TriangleEnemy.

        // Spawn death effect
        SpawnDeathEffect();

        // Create corpse or destroy enemy
        if (leaveCorpse && corpseSprite != null)
        {
            StartCoroutine(CreateCorpseSequence());
        }
        else
        {
            // No corpse - just destroy
            Destroy(gameObject, 0.1f);
        }
    }

    private void SpawnDeathEffect()
    {
        if (deathEffect != null)
        {
            GameObject effect = Instantiate(deathEffect, transform.position, Quaternion.identity);

            // Auto-destroy death effect after duration
            if (deathEffectDuration > 0)
            {
                Destroy(effect, deathEffectDuration);
            }
        }
    }

    private IEnumerator CreateCorpseSequence()
    {
        // Create the corpse GameObject
        GameObject corpse = CreateCorpse();

        // Disable enemy functionality immediately
        DisableEnemyComponents();

        // Wait a tiny bit for audio to play
        yield return new WaitForSeconds(0.1f);

        // Destroy the original enemy
        Destroy(gameObject);

        // Handle corpse cleanup
        if (corpse != null && corpseLifetime > 0)
        {
            Destroy(corpse, corpseLifetime);
        }
    }

    private GameObject CreateCorpse()
    {
        // Create corpse GameObject
        GameObject corpse = new GameObject($"{gameObject.name}_Corpse");
        corpse.transform.position = transform.position;
        corpse.transform.rotation = transform.rotation;
        corpse.transform.localScale = transform.localScale;

        // Add sprite renderer for corpse
        SpriteRenderer corpseRenderer = corpse.AddComponent<SpriteRenderer>();
        corpseRenderer.sprite = corpseSprite;

        // Set sorting layer from inspector
        corpseRenderer.sortingLayerName = corpseSortingLayer;
        corpseRenderer.sortingOrder = corpseSortingOrder;

        // Make corpse semi-transparent
        Color corpseColor = new Color(130 / 255f, 212 / 255f, 140 / 255f);
        corpseColor.a = corpseAlpha;
        corpseRenderer.color = corpseColor;

        return corpse;
    }

    private void DisableEnemyComponents()
    {
        // Disable collider so nothing can interact with dead enemy
        if (enemyCollider != null)
        {
            enemyCollider.enabled = false;
        }

        // Hide original sprite
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.clear;
        }

        // Disable specific scripts or all scripts
        if (disableAllScripts)
        {
            DisableAllScripts();
        }
        else if (scriptsToDisable != null && scriptsToDisable.Length > 0)
        {
            DisableSpecificScripts();
        }
    }

    private void DisableAllScripts()
    {
        MonoBehaviour[] allScripts = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in allScripts)
        {
            // Don't disable this EnemyDeath script until we're done
            if (script != this)
            {
                script.enabled = false;
            }
        }
    }

    private void DisableSpecificScripts()
    {
        foreach (MonoBehaviour script in scriptsToDisable)
        {
            if (script != null)
            {
                script.enabled = false;
            }
        }
    }
}