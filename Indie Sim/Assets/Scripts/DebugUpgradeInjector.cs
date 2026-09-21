using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Standalone debug tool: grants CigData entries straight into Pack on scene
/// start, bypassing the shop, CigPool, and coin cost entirely. Not referenced
/// by anything else in the project — drop it on any GameObject, assign
/// entries, hit Play; delete the GameObject (and this file) when done and
/// nothing else needs to change.
/// </summary>
public class DebugUpgradeInjector : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public CigData data;
        [Range(1, 4)] public int tier = 1;
        public Rarity rarity = Rarity.Common;
        [Tooltip("Grants already burning (maxed effect, countdown running) instead of just sitting at the tier above.")]
        public bool startBurning;
    }

    [Tooltip("Granted directly into Pack on Start, in list order. Bypasses coins, CigPool, and the shop entirely.")]
    [SerializeField] private List<Entry> grantOnStart = new List<Entry>();

    private IEnumerator Start()
    {
        yield return null; // let Pack/CigPool/BurnResolver finish their own Start() first

        GrantAll();
    }

    private void GrantAll()
    {
        if (Pack.Instance == null)
        {
            Debug.LogWarning("[DebugUpgradeInjector] No Pack in scene — nothing to grant.");
            return;
        }

        foreach (Entry entry in grantOnStart)
            Grant(entry);
    }

    private void Grant(Entry entry)
    {
        if (entry?.data == null) return;

        var instance = new CigInstance
        {
            Data = entry.data,
            RolledTier = Mathf.Clamp(entry.tier, 1, 4),
            RolledRarity = entry.rarity
        };

        if (!Pack.Instance.TryAdd(instance))
        {
            Debug.LogWarning($"[DebugUpgradeInjector] Could not grant {entry.data.displayName} — pack full.");
            return;
        }

        if (entry.startBurning)
            BurnResolver.Instance?.Burn(instance);

        Debug.Log($"[DebugUpgradeInjector] Granted {entry.data.displayName} (Tier {instance.RolledTier}, {instance.RolledRarity}){(entry.startBurning ? " — burning" : "")}.");
    }

    [ContextMenu("DEBUG - Grant All Now")]
    private void GrantAllNow() => GrantAll();
}
