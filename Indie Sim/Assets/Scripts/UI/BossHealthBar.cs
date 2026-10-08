using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Boss health bar for the BossArena canvas variant. Hidden until
/// BossSceneManager spawns the boss, then tracks its health (with a delayed
/// "chip" trail) and its shield, which the boss regains each time it leaves a wall.
/// Fills work by scaling each bar's RectTransform anchorMax.x, so no sprites
/// or Filled images are needed.
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Everything that shows/hides with the boss.")]
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform healthFill;
    [SerializeField] private RectTransform healthTrail;
    [SerializeField] private RectTransform shieldFill;
    [SerializeField] private TMP_Text nameText;

    [Header("Feel")]
    [Tooltip("Seconds the trail waits after a hit before catching up.")]
    [SerializeField] private float trailDelay = 0.4f;
    [Tooltip("Fraction of the bar per second the trail drains.")]
    [SerializeField] private float trailSpeed = 0.6f;
    [Tooltip("Seconds the empty bar stays up after the boss dies.")]
    [SerializeField] private float hideDelayAfterDeath = 1f;

    private BossEnemy boss;
    private float trailFraction = 1f;
    private float lastHealthFraction = 1f;
    private float trailHoldUntil;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
    }

    private void OnEnable() => BossSceneManager.OnBossSpawned += Bind;
    private void OnDisable() => BossSceneManager.OnBossSpawned -= Bind;

    private void Bind(BossEnemy spawnedBoss, string displayName)
    {
        boss = spawnedBoss;
        boss.OnDeath += HandleBossDeath;

        if (nameText != null) nameText.text = displayName;

        lastHealthFraction = trailFraction = HealthFraction();
        SetFill(healthFill, lastHealthFraction);
        SetFill(healthTrail, trailFraction);
        UpdateShield();

        if (root != null) root.SetActive(true);
    }

    private void Update()
    {
        if (boss == null) return;

        float health = HealthFraction();
        if (health < lastHealthFraction) trailHoldUntil = Time.time + trailDelay;
        lastHealthFraction = health;
        SetFill(healthFill, health);

        if (Time.time >= trailHoldUntil)
            trailFraction = Mathf.MoveTowards(trailFraction, health, trailSpeed * Time.deltaTime);
        trailFraction = Mathf.Max(trailFraction, health);
        SetFill(healthTrail, trailFraction);

        UpdateShield();
    }

    private float HealthFraction() =>
        boss.GetMaxHealth() > 0 ? (float)boss.GetCurrentHealth() / boss.GetMaxHealth() : 0f;

    private void UpdateShield()
    {
        if (shieldFill == null) return;

        bool shielded = boss.HasShield() && boss.GetMaxShieldHealth() > 0;
        shieldFill.gameObject.SetActive(shielded);
        if (shielded)
            SetFill(shieldFill, (float)boss.GetCurrentShieldHealth() / boss.GetMaxShieldHealth());
    }

    private void HandleBossDeath()
    {
        boss.OnDeath -= HandleBossDeath;
        SetFill(healthFill, 0f);
        if (shieldFill != null) shieldFill.gameObject.SetActive(false);
        boss = null;
        StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        // Realtime: the Demo Complete screen may freeze time right after the kill.
        yield return new WaitForSecondsRealtime(hideDelayAfterDeath);
        if (root != null) root.SetActive(false);
    }

    private static void SetFill(RectTransform bar, float fraction)
    {
        if (bar == null) return;
        Vector2 max = bar.anchorMax;
        max.x = Mathf.Clamp01(fraction);
        bar.anchorMax = max;
    }

    private void OnDestroy()
    {
        if (boss != null) boss.OnDeath -= HandleBossDeath;
    }
}
