using System.Collections;
using UnityEngine;

public class Teleporter : MonoBehaviour
{
    [Header("Teleporter Settings")]
    [SerializeField] private float activationRadius = 2f;
    [SerializeField] private float teleportDelay = 1f;

    [Header("Effects (Assign in Inspector)")]
    [SerializeField] private GameObject teleportEffect;
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip teleportSound;

    [Header("Visual Settings")]
    [SerializeField] private float glowIntensity = 1f;
    [SerializeField] private Color teleporterColor = Color.cyan;

    [Header("Cleanup Settings")]
    [SerializeField] private string[] enemyTags = { "Enemy", "EnemySpawner", "Coin", "Relic" };

    [Header("Teleporter Settings")]
    [Tooltip("Does the player need a key to use this teleporter?")]
    public bool requiresKey = true;

    // ✅ NEW — toggle between next dungeon or boss level
    [Header("Destination Settings")]
    [Tooltip("If true, loads the Boss Level scene instead of generating a new dungeon.")]
    [SerializeField] private bool leadsToBossLevel = false;

    [Tooltip("Exact name of the Boss Level scene (must match Build Settings).")]
    [SerializeField] private string bossSceneName = "BossLevel";

    [Tooltip("Tutorial.unity's teleporter: ends the tutorial (never plays again) and loads the first dungeon. Overrides Leads To Boss Level.")]
    [SerializeField] private bool finishesTutorial = false;

    // Core dependencies
    private DungeonMapGenerator mapGenerator;
    private RoguelikeManager roguelikeManager;
    private GameObject player;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    // State tracking
    private bool playerInRange = false;
    private bool isTeleporting = false;

    // Events for external systems
    public System.Action OnTeleportStarted;
    public System.Action OnTeleportCompleted;
    public System.Action OnNewLevelGenerated;

    #region Unity Lifecycle
    void Start()
    {
        InitializeTeleporter();
    }

    void Update()
    {
        UpdateVisualEffects();
    }
    #endregion

    #region Initialization
    private void InitializeTeleporter()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        // The tutorial has no dungeon — it needs neither.
        if (!finishesTutorial)
        {
            mapGenerator = FindFirstObjectByType<DungeonMapGenerator>();
            if (mapGenerator == null)
                Debug.LogError($"Teleporter '{gameObject.name}': DungeonMapGenerator not found!");

            roguelikeManager = FindFirstObjectByType<RoguelikeManager>();
            if (roguelikeManager == null)
                Debug.LogError($"Teleporter '{gameObject.name}': RoguelikeManager not found!");
        }

        SetupTriggerCollider();

        if (spriteRenderer != null)
            spriteRenderer.color = teleporterColor;

        // ✅ NEW — log destination type on start so you can confirm in Console
        Debug.Log($"Teleporter '{gameObject.name}' initialized. Destination: " +
                  (finishesTutorial ? "First Dungeon (finishes tutorial)" : leadsToBossLevel ? $"Boss Level ({bossSceneName})" : "Next Dungeon"));
    }

    private void SetupTriggerCollider()
    {
        CircleCollider2D triggerCollider = GetComponent<CircleCollider2D>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<CircleCollider2D>();
            Debug.Log($"Added CircleCollider2D to teleporter '{gameObject.name}'");
        }

        triggerCollider.isTrigger = true;
        triggerCollider.radius = activationRadius;
    }
    #endregion

    #region Visual Effects
    private void UpdateVisualEffects()
    {
        if (spriteRenderer == null) return;

        float pulse = (Mathf.Sin(Time.time * 2f) + 1f) * 0.5f;
        float intensity = glowIntensity * (0.5f + pulse * 0.5f);

        Color currentColor = teleporterColor;
        currentColor.a = intensity;
        spriteRenderer.color = currentColor;

        float scale = 1f + pulse * 0.1f;
        transform.localScale = Vector3.one * scale;
    }
    #endregion

    #region Teleportation Logic
    public void ActivateTeleporter()
    {
        if (isTeleporting)
        {
            Debug.LogWarning("Cannot activate teleporter: already teleporting");
            return;
        }

        // Idle while no dungeon is running (store, resumed store). The scene
        // copy that caused a teleport on resume is gone (each dungeon spawns
        // its own), but this stays as a safety net. Checked here, not in
        // OnTriggerEnter2D, because PlayerKeyManagement calls this directly.
        RoguelikeManager manager = roguelikeManager != null ? roguelikeManager : RoguelikeManager.Instance;
        if (!finishesTutorial && manager != null && !manager.IsDungeonActive) return;

        StartCoroutine(ExecuteTeleportSequence());
    }

    private IEnumerator ExecuteTeleportSequence()
    {
        isTeleporting = true;
        OnTeleportStarted?.Invoke();

        PlayTeleportEffects();
        StartCoroutine(AnimateTeleporter());

        yield return new WaitForSeconds(teleportDelay);

        ClearEnemiesAndSpawners();

        // ✅ NEW — branch based on the toggle
        if (finishesTutorial)
        {
            FinishTutorial();
        }
        else if (leadsToBossLevel)
        {
            LoadBossLevel();
        }
        else
        {
            LoadNextDungeon();
        }

        OnTeleportCompleted?.Invoke();
        isTeleporting = false;

        Debug.Log("Teleportation completed successfully");
    }

    // Funnels through AdvanceToBoss() so CurrentRun carries over intact (D2).
    private void LoadBossLevel()
    {
        Debug.Log("<color=red>Loading Boss Level</color>");
        GameManager.Instance.AdvanceToBoss();
    }

    // Through TutorialDirector so the hint banner clears and a double trigger
    // can't finish twice; it calls GameManager.FinishTutorial (flag + first dungeon).
    private void FinishTutorial()
    {
        Debug.Log("<color=lime>Tutorial complete! Loading the first dungeon...</color>");
        if (TutorialDirector.Instance != null)
            TutorialDirector.Instance.Finish();
        else
            GameManager.Instance.FinishTutorial();
    }

    // ✅ NEW — existing dungeon completion logic, extracted into its own method
    private void LoadNextDungeon()
    {
        if (roguelikeManager != null)
        {
            Debug.Log("<color=lime>Dungeon completed! Proceeding to next dungeon...</color>");
            roguelikeManager.CompleteDungeon();
        }
        else
        {
            Debug.LogError("[Teleporter] RoguelikeManager not found - cannot complete dungeon!");
        }
    }

    private void PlayTeleportEffects()
    {
        if (audioSource != null && teleportSound != null)
            audioSource.PlayOneShot(teleportSound);

        if (teleportEffect != null)
            Instantiate(teleportEffect, transform.position, transform.rotation);

        if (particles != null)
            particles.Emit(50);
    }

    private IEnumerator AnimateTeleporter()
    {
        float duration = teleportDelay;
        float elapsed = 0f;
        Vector3 originalScale = transform.localScale;
        Color originalColor = spriteRenderer != null ? spriteRenderer.color : teleporterColor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            transform.Rotate(0, 0, 360 * Time.deltaTime);

            float scaleMultiplier = 1f + progress * 2f;
            transform.localScale = originalScale * scaleMultiplier;

            if (spriteRenderer != null)
            {
                Color animColor = teleporterColor;
                animColor.a = 1f + progress;
                spriteRenderer.color = animColor;
            }

            yield return null;
        }

        transform.localScale = originalScale;
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
    }
    #endregion

    #region Enemy Cleanup
    private void ClearEnemiesAndSpawners()
    {
        ClearGameObjectsByTags(enemyTags);
        Debug.Log("Cleared all enemies, spawners, and projectiles from current level");
    }

    private void ClearGameObjectsByTags(string[] tags)
    {
        foreach (string tag in tags)
            ClearGameObjectsByTag(tag);
    }

    private void ClearGameObjectsByTag(string tag)
    {
        GameObject[] objectsToDestroy = GameObject.FindGameObjectsWithTag(tag);

        foreach (GameObject obj in objectsToDestroy)
        {
            obj.SetActive(false);
            Destroy(obj);
        }

        Debug.Log($"Destroyed {objectsToDestroy.Length} objects with tag: {tag}");
    }
    #endregion

    #region Trigger Events
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            player = other.gameObject;
            playerInRange = true;

            if (requiresKey)
            {
                PlayerKeyManagement keyManager = other.GetComponent<PlayerKeyManagement>();
                if (keyManager != null && keyManager.HasKey)
                {
                    ActivateTeleporter();
                    Debug.Log("Player has key - Teleporting!");
                }
                else
                {
                    Debug.Log("Player needs a key to use this teleporter!");
                }
            }
            else
            {
                ActivateTeleporter();
                Debug.Log("No key required - Teleporting!");
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            Debug.Log("Player exited teleporter range");
        }
    }
    #endregion

    #region Public API
    public void SetMapGenerator(DungeonMapGenerator generator) => mapGenerator = generator;
    public void SetRoguelikeManager(RoguelikeManager manager) => roguelikeManager = manager;
    public bool IsPlayerInRange() => playerInRange;
    public bool IsTeleporting() => isTeleporting;

    public void SpawnInRoom(Room room)
    {
        if (room == null) return;

        Vector3 spawnPosition = new Vector3(
            Mathf.Round(room.worldPosition.x),
            Mathf.Round(room.worldPosition.y),
            transform.position.z
        );

        transform.position = spawnPosition;
        Debug.Log($"Teleporter spawned in room {room.uniqueId} at {spawnPosition}");
    }
    #endregion

    #region Debug Visualization
    private void OnDrawGizmos()
    {
        // ✅ NEW — color changes based on destination type too
        Gizmos.color = leadsToBossLevel ? Color.red : (playerInRange ? Color.green : Color.cyan);
        Gizmos.DrawWireSphere(transform.position, activationRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationRadius);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
    #endregion
}
