using UnityEngine;

/// <summary>
/// Data describing one boss encounter: which boss to spawn, optional arena
/// variant, music, and arena bounds. BossArena is one scene, reused for every
/// boss (D1) — the boss itself is data, not baked into the scene.
/// </summary>
[CreateAssetMenu(fileName = "BossDefinition", menuName = "Boss/Boss Definition")]
public class BossDefinition : ScriptableObject
{
    [Header("Boss")]
    public GameObject bossPrefab;
    [Tooltip("Name shown on the boss health bar.")]
    public string displayName = "Boss";

    [Header("Arena (optional)")]
    [Tooltip("Optional variant of BossArena's geometry for this boss. Leave empty to use the default arena.")]
    public GameObject arenaVariantPrefab;
    public AudioClip music;
    public Bounds arenaBounds;
}
