using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drop on any GameObject in RoguelikeMode (or wherever Pack/CigPool live) and hit Play.
/// Press F1 to toggle. Shows live PackStats, held cigs, the equipped weapon's
/// current damage, and a running damage log split by source (gun / stomp /
/// dash AoE) and crit vs normal — plus one-click buttons to grant any
/// catalog upgrade at any tier, bypassing the shop, for isolated testing.
/// Debug-only tool: delete the GameObject (and this file) when done.
/// </summary>
public class UpgradeDebugHUD : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private bool startVisible = true;

    private bool visible;
    private Vector2 logScroll;
    private Vector2 spawnScroll;
    private PlayerConeShooter coneShooter;

    private struct HitRecord
    {
        public string label;
        public int damage;
    }

    private readonly List<HitRecord> log = new List<HitRecord>();
    private const int MaxLogEntries = 25;

    private int totalGunDamage;
    private int totalAoeDamage;
    private int critHits;
    private int normalHits;
    private readonly Dictionary<string, int> damageBySource = new Dictionary<string, int>();

    private GUIStyle richLabel;

    private void Awake()
    {
        // Editor and Development Builds only — a real release build disables this
        // component entirely (before OnEnable subscribes to anything) so it never
        // renders or costs anything for players.
        if (!Application.isEditor && !Debug.isDebugBuild)
        {
            enabled = false;
            return;
        }
        visible = startVisible;
    }

    private void OnEnable()
    {
        PlayerConeShooter.OnGunHit += HandleGunHit;
        PlayerStompController.OnAoeHit += HandleAoeHit;
    }

    private void OnDisable()
    {
        PlayerConeShooter.OnGunHit -= HandleGunHit;
        PlayerStompController.OnAoeHit -= HandleAoeHit;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
        if (coneShooter == null) coneShooter = FindFirstObjectByType<PlayerConeShooter>();
    }

    private void HandleGunHit(WeaponData weapon, int damage, bool isCrit)
    {
        totalGunDamage += damage;
        if (isCrit) critHits++; else normalHits++;

        string label = weapon != null ? weapon.weaponName : "Unknown Gun";
        AddToSource(label, damage);
        AddLog($"{label}{(isCrit ? " [CRIT]" : "")}", damage);
    }

    private void HandleAoeHit(int damage, string source)
    {
        totalAoeDamage += damage;
        AddToSource(source, damage);
        AddLog(source, damage);
    }

    private void AddToSource(string source, int damage)
    {
        damageBySource.TryGetValue(source, out int current);
        damageBySource[source] = current + damage;
    }

    private void AddLog(string label, int damage)
    {
        log.Add(new HitRecord { label = label, damage = damage });
        if (log.Count > MaxLogEntries) log.RemoveAt(0);
    }

    private void OnGUI()
    {
        richLabel ??= new GUIStyle(GUI.skin.label) { richText = true };

        if (!visible)
        {
            GUI.Label(new Rect(10, 10, 320, 20), $"[{toggleKey}] Upgrade Debug HUD");
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 440, Screen.height - 20), GUI.skin.box);

        GUILayout.Label($"<b>UPGRADE DEBUG HUD</b>  [{toggleKey} to close]", richLabel);

        DrawWeaponSection();
        GUILayout.Space(6);
        DrawPackStatsSection();
        GUILayout.Space(6);
        DrawHeldCigsSection();
        GUILayout.Space(6);
        DrawDamageSection();
        GUILayout.Space(6);
        DrawSpawnSection();

        GUILayout.EndArea();
    }

    private void DrawWeaponSection()
    {
        GUILayout.Label("<b>-- Weapon --</b>", richLabel);
        if (coneShooter == null)
        {
            GUILayout.Label("No PlayerConeShooter found in scene.");
            return;
        }

        WeaponData weapon = coneShooter.GetCurrentWeapon();
        if (weapon == null)
        {
            GUILayout.Label("No weapon equipped.");
            return;
        }

        string slot = coneShooter.IsCurrentWeaponSecondary ? "Secondary (Shotgun)" : "Primary";
        GUILayout.Label($"{weapon.weaponName}  [{slot}]");
        GUILayout.Label($"Base dmg: {weapon.baseDamagePerShot}   Live dmg/hit: {coneShooter.GetLiveDamagePerShot()}");
    }

    private void DrawPackStatsSection()
    {
        GUILayout.Label("<b>-- Live Pack Stats --</b>", richLabel);
        if (Pack.Instance == null)
        {
            GUILayout.Label("No Pack in scene.");
            return;
        }

        PackStats s = Pack.Instance.Stats;
        GUILayout.Label($"Primary   crit {s.PrimaryCritChance:P0} x{s.PrimaryCritMultiplier:0.00}   bonusDmg {s.PrimaryWeaponBonusDamage}   bounce {s.PrimaryBounceCount}");
        GUILayout.Label($"Secondary crit {s.SecondaryCritChance:P0} x{s.SecondaryCritMultiplier:0.00}   bonusDmg {s.SecondaryWeaponBonusDamage}   bounce {s.SecondaryBounceCount}");
        GUILayout.Label($"Stomp     +radius {s.StompBonusRadius:0.0}   +dmg {s.StompBonusDamage}   bulletRing {s.StompBulletCount}");
        GUILayout.Label($"Dash      aoeDmg {s.DashAoeDamage}   deflect {s.DashDeflectEnabled}   +charges {s.DashExtraCharges}");
        GUILayout.Label($"DashWindow dur {s.DashDamageWindowDuration:0.00}s   mult x{s.DashDamageWindowMultiplier:0.00}");
    }

    private void DrawHeldCigsSection()
    {
        GUILayout.Label("<b>-- Held Cigs --</b>", richLabel);
        if (Pack.Instance == null || Pack.Instance.HeldCigs.Count == 0)
        {
            GUILayout.Label("(none held)");
            return;
        }

        foreach (CigInstance c in Pack.Instance.HeldCigs)
        {
            if (c?.Data == null) continue;
            string burn = c.IsBurning ? "  BURNING" : "";
            GUILayout.Label($"- {c.Data.displayName}  T{c.RolledTier}({c.RolledRarity})  effT{c.EffectiveTier}{burn}");
        }
    }

    private void DrawDamageSection()
    {
        GUILayout.Label("<b>-- Damage --</b>", richLabel);
        int grandTotal = totalGunDamage + totalAoeDamage;
        GUILayout.Label($"Total {grandTotal}   Gun {totalGunDamage}   AoE(Stomp/DashAoE) {totalAoeDamage}");
        GUILayout.Label($"Crit hits {critHits}   Normal hits {normalHits}");

        foreach (KeyValuePair<string, int> kv in damageBySource)
            GUILayout.Label($"  {kv.Key}: {kv.Value}");

        GUILayout.Label("Recent hits (newest first):");
        logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.Height(110));
        for (int i = log.Count - 1; i >= 0; i--)
            GUILayout.Label($"{log[i].label}: {log[i].damage}");
        GUILayout.EndScrollView();

        if (GUILayout.Button("Reset damage counters"))
        {
            totalGunDamage = 0;
            totalAoeDamage = 0;
            critHits = 0;
            normalHits = 0;
            damageBySource.Clear();
            log.Clear();
        }
    }

    private void DrawSpawnSection()
    {
        GUILayout.Label("<b>-- Grant Upgrade (bypasses shop) --</b>", richLabel);
        if (CigPool.Instance == null || Pack.Instance == null)
        {
            GUILayout.Label("CigPool/Pack not in scene.");
            return;
        }

        spawnScroll = GUILayout.BeginScrollView(spawnScroll, GUILayout.Height(160));
        foreach (CigData data in CigPool.Instance.Catalog)
        {
            if (data == null) continue;

            GUILayout.BeginHorizontal();
            GUILayout.Label(data.displayName, GUILayout.Width(170));
            for (int tier = 1; tier <= 4; tier++)
            {
                if (GUILayout.Button($"T{tier}", GUILayout.Width(28)))
                    GrantForTesting(data, tier);
            }
            if (GUILayout.Button("Burn", GUILayout.Width(45)))
                BurnForTesting(data);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void GrantForTesting(CigData data, int tier)
    {
        var instance = new CigInstance { Data = data, RolledTier = tier, RolledRarity = Rarity.Common };
        bool granted = Pack.Instance.TryAdd(instance);
        Debug.Log(granted
            ? $"[UpgradeDebugHUD] Granted {data.displayName} at Tier {tier}."
            : $"[UpgradeDebugHUD] Could not grant {data.displayName} — pack full.");
    }

    private void BurnForTesting(CigData data)
    {
        CigInstance held = Pack.Instance.FindByLineage(data.id);
        if (held == null)
        {
            Debug.LogWarning($"[UpgradeDebugHUD] {data.displayName} is not currently held — grant it first.");
            return;
        }
        BurnResolver.Instance?.Burn(held);
    }
}
