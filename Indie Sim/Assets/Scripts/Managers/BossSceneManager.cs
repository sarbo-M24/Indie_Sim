using UnityEngine;

public class BossSceneManager : MonoBehaviour
{
    [Header("Boss (D1 — data-driven, not scene-baked)")]
    [SerializeField] private BossDefinition bossDefinition;
    [SerializeField] private Transform bossSpawnPoint;

    /// <summary>Raised once the boss is spawned (boss, display name). UI such as BossHealthBar listens.</summary>
    public static event System.Action<BossEnemy, string> OnBossSpawned;

    private void Start()
    {
        Debug.Log($"[BossSceneManager] Start() in scene '{gameObject.scene.name}'. GameManager.Instance:{GameManager.Instance != null}, GameSession.Instance:{GameSession.Instance != null}");
        SpawnBoss();
    }

    private void SpawnBoss()
    {
        if (bossDefinition == null || bossDefinition.bossPrefab == null)
        {
            Debug.LogError("[BossSceneManager] No BossDefinition/bossPrefab assigned!");
            return;
        }

        Vector3 spawnPosition = bossSpawnPoint != null ? bossSpawnPoint.position : Vector3.zero;
        Quaternion spawnRotation = bossSpawnPoint != null ? bossSpawnPoint.rotation : Quaternion.identity;
        GameObject bossInstance = Instantiate(bossDefinition.bossPrefab, spawnPosition, spawnRotation);
        Debug.Log($"[BossSceneManager] Boss spawned: {bossInstance.name} at {spawnPosition}");

        BossEnemy bossEnemy = bossInstance.GetComponent<BossEnemy>();
        if (bossEnemy != null)
        {
            bossEnemy.OnDeath += OnBossDefeated;
            OnBossSpawned?.Invoke(bossEnemy, bossDefinition.displayName);
        }
        else
            Debug.LogError("[BossSceneManager] Spawned boss has no BossEnemy component — OnBossDefeated will never fire!");
    }

    /// <summary>
    /// Boss defeated — terminal success path (D2). Replaces the old
    /// VictoryPanel/continue-button flow entirely; GameManager.CompleteRun()
    /// shows the Demo Complete screen, whose only exit is Main Menu.
    /// </summary>
    public void OnBossDefeated()
    {
        Debug.Log("[BossSceneManager] Boss defeated — completing run.");
        GameManager.Instance.CompleteRun();
    }

    [ContextMenu("DEBUG - Defeat Boss")]
    public void DEBUG_DefeatBoss() => OnBossDefeated();
}
