using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a before/after pair of PackStats into human-readable
/// "Stat Name: current → upgraded" lines for the shop tooltip. Pure display
/// formatting — no upgrade math lives here; the "after" side comes from
/// Pack.PreviewBuy, which shares Recompute's resolution path. Only stats that
/// actually change are listed, so a non-stat upgrade yields no lines.
/// </summary>
public static class PackStatsPreview
{
    private readonly struct Entry
    {
        public readonly string Label;
        public readonly Func<PackStats, float> Read;
        public readonly Func<float, string> Format;

        public Entry(string label, Func<PackStats, float> read, Func<float, string> format)
        {
            Label = label;
            Read = read;
            Format = format;
        }
    }

    private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";
    private static string BonusPercent(float v) => $"+{Mathf.RoundToInt(v * 100f)}%";
    private static string Multiplier(float v) => $"x{v:0.##}";
    private static string Whole(float v) => Mathf.RoundToInt(v).ToString();
    private static string Decimal(float v) => v.ToString("0.##");
    private static string Seconds(float v) => $"{v:0.##}s";
    private static string PlusSeconds(float v) => $"+{v:0.##}s";
    private static string OnOff(float v) => v > 0.5f ? "On" : "Off";

    private static readonly Entry[] Entries =
    {
        new Entry("Primary Crit Chance",       s => s.PrimaryCritChance,          Percent),
        new Entry("Primary Crit Damage",       s => s.PrimaryCritMultiplier,      Multiplier),
        new Entry("Primary Bonus Damage",      s => s.PrimaryWeaponBonusDamage,   Whole),
        new Entry("Primary Fire Rate",         s => s.PrimaryFireRateBonus,       BonusPercent),
        new Entry("Primary Bounces",           s => s.PrimaryBounceCount,         Whole),
        new Entry("Secondary Crit Chance",     s => s.SecondaryCritChance,        Percent),
        new Entry("Secondary Crit Damage",     s => s.SecondaryCritMultiplier,    Multiplier),
        new Entry("Secondary Bonus Damage",    s => s.SecondaryWeaponBonusDamage, Whole),
        new Entry("Secondary Fire Rate",       s => s.SecondaryFireRateBonus,     BonusPercent),
        new Entry("Secondary Bounces",         s => s.SecondaryBounceCount,       Whole),
        new Entry("Shotgun Pellets",           s => s.ShotgunBonusPellets,        Whole),
        new Entry("Stomp Radius",              s => s.StompBonusRadius,           Decimal),
        new Entry("Stomp Damage",              s => s.StompBonusDamage,           Whole),
        new Entry("Stomp Bullets",             s => s.StompBulletCount,           Whole),
        new Entry("Stomp Charges",             s => s.StompExtraCharges,          Whole),
        new Entry("Stomp Cooldown",            s => s.StompCooldownPenalty,       PlusSeconds),
        new Entry("Post-Dash Damage",          s => s.DashDamageWindowMultiplier, Multiplier),
        new Entry("Post-Dash Window",          s => s.DashDamageWindowDuration,   Seconds),
        new Entry("Dash AoE Damage",           s => s.DashAoeDamage,              Whole),
        new Entry("Dash AoE Radius",           s => s.DashAoeRadius,              Decimal),
        new Entry("Dash AoE Knockback",        s => s.DashAoeKnockback,           Decimal),
        new Entry("Dash Deflect",              s => s.DashDeflectEnabled ? 1f : 0f, OnOff),
        new Entry("Dash Charges",              s => s.DashExtraCharges,           Whole),
        new Entry("Dash Cooldown",             s => s.DashCooldownPenalty,        PlusSeconds),
    };

    /// <summary>One "Label: before{arrow}after" line per stat that differs between the two.</summary>
    public static List<string> BuildLines(PackStats before, PackStats after, string arrow)
    {
        List<string> lines = new List<string>();
        foreach (Entry entry in Entries)
        {
            float from = entry.Read(before);
            float to = entry.Read(after);
            if (Mathf.Approximately(from, to)) continue;

            lines.Add($"{entry.Label}: {entry.Format(from)} {arrow} {entry.Format(to)}");
        }
        return lines;
    }
}
