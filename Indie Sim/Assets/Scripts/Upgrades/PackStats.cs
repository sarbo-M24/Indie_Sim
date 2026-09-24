/// <summary>
/// The aggregate every stat consumer queries. Rebuilt from scratch by
/// Pack.Recompute() each time the pack changes — never mutated piecemeal.
/// </summary>
[System.Serializable]
public struct PackStats
{
    // Weapon (crit from CritChanceCigData, flat damage from BulletBounceCigData, per weapon slot)
    public float PrimaryCritChance;
    public float PrimaryCritMultiplier;
    public int PrimaryWeaponBonusDamage;
    public float SecondaryCritChance;
    public float SecondaryCritMultiplier;
    public int SecondaryWeaponBonusDamage;

    // Fire rate (% increase, granted by FireRateCigData per weapon slot)
    public float PrimaryFireRateBonus;
    public float SecondaryFireRateBonus;

    // Bounce
    public int PrimaryBounceCount;
    public int SecondaryBounceCount;

    // Shotgun-only (secondary weapon's pellet count, granted by ShotgunPelletCountCigData)
    public int ShotgunBonusPellets;

    // Stomp
    public float StompBonusRadius;
    public int StompBonusDamage;
    public int StompBulletCount;
    public int StompExtraCharges;
    public float StompCooldownPenalty; // seconds added to the stomp cooldown (Chain Stomp)

    // Dash
    public float DashDamageWindowDuration;
    public float DashDamageWindowMultiplier;
    // Dash AoE ticks continuously every physics step of the dash (not once at
    // dash-end); damage and knockback scale with tier + rarity via DashAoECigData,
    // radius is fixed per asset.
    public int DashAoeDamage;
    public float DashAoeRadius;
    public float DashAoeKnockback;
    public bool DashDeflectEnabled;
    public int DashExtraCharges;
    public float DashCooldownPenalty; // seconds added to the dash cooldown (Chain Dash)

    public static PackStats Baseline => new PackStats
    {
        PrimaryCritMultiplier = 1f,
        SecondaryCritMultiplier = 1f,
        DashDamageWindowMultiplier = 1f
    };
}
