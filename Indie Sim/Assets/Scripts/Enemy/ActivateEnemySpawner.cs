using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawnerActivator : MonoBehaviour
{
    [Header("Activation Settings")]
    [SerializeField] private float activationRange = 10f; // Distance to activate spawners
    [SerializeField] private float scanInterval = 0.5f; // How often to scan for spawners (seconds)

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color activationRangeColor = new Color(1f, 1f, 0f, 0.3f); // Yellow

    // Tracking
    private List<EnemySpawner> nearbySpawners = new List<EnemySpawner>();
    private HashSet<EnemySpawner> activatedSpawners = new HashSet<EnemySpawner>();

    void Start()
    {
        // Start scanning for spawners periodically
        InvokeRepeating(nameof(ScanForSpawners), 0f, scanInterval);
    }

    /// <summary>
    /// Scans for nearby enemy spawners and activates them if in range
    /// This runs periodically instead of every frame for better performance
    /// </summary>
    private void ScanForSpawners()
    {
        // Find all spawners in range using OverlapCircle
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, activationRange);

        nearbySpawners.Clear();

        foreach (Collider2D col in colliders)
        {
            EnemySpawner spawner = col.GetComponent<EnemySpawner>();

            if (spawner != null)
            {
                nearbySpawners.Add(spawner);

                // Activate if not already activated and requires activation
                if (!activatedSpawners.Contains(spawner) &&
                    spawner.RequiresActivation() &&
                    spawner.ActivatesByProximity() &&
                    !spawner.IsActivated() &&
                    !spawner.IsDead())
                {
                    ActivateSpawner(spawner);
                }
            }
        }
    }

    /// <summary>
    /// Activates a specific spawner
    /// </summary>
    private void ActivateSpawner(EnemySpawner spawner)
    {
        if (spawner == null || activatedSpawners.Contains(spawner)) return;

        spawner.ActivateSpawner();
        activatedSpawners.Add(spawner);

        Debug.Log($"[PlayerSpawnerActivator] Activated spawner at {spawner.transform.position}");
    }

    /// <summary>
    /// Public method to manually adjust activation range
    /// </summary>
    public void SetActivationRange(float newRange)
    {
        activationRange = Mathf.Max(1f, newRange);
    }

    /// <summary>
    /// Get current activation range
    /// </summary>
    public float GetActivationRange()
    {
        return activationRange;
    }

    /// <summary>
    /// Get number of activated spawners
    /// </summary>
    public int GetActivatedSpawnersCount()
    {
        return activatedSpawners.Count;
    }

    #region Debug Visualization

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // Draw activation range
        Gizmos.color = activationRangeColor;
        Gizmos.DrawWireSphere(transform.position, activationRange);
    }

    private void OnDrawGizmosSelected()
    {
        // Draw larger activation range when selected
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f); // Brighter yellow
        Gizmos.DrawWireSphere(transform.position, activationRange);

        // Draw lines to nearby spawners (only in play mode)
        if (Application.isPlaying && nearbySpawners != null)
        {
            foreach (var spawner in nearbySpawners)
            {
                if (spawner != null)
                {
                    // Green line if activated, yellow if not
                    Gizmos.color = activatedSpawners.Contains(spawner) ? Color.green : Color.yellow;
                    Gizmos.DrawLine(transform.position, spawner.transform.position);
                }
            }
        }
    }

    #endregion
}
