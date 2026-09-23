/// <summary>
/// The aggregate every stat consumer queries. Rebuilt from scratch by
/// Pack.Recompute() each time the pack changes — never mutated piecemeal.
/// </summary>
[System.Serializable]
public struct PackStats
{
    // Weapon (crit + flat damage — both granted by CritChanceCigData per weapon slot)
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

    // Dash
    public float DashDamageWindowDuration;
    public float DashDamageWindowMultiplier;
    // Dash AoE ticks continuously every physics step of the dash (not once at
    // dash-end); both damage and radius scale with tier + rarity via DashAoECigData.
    public int DashAoeDamage;
    public float DashAoeRadius;
    public bool DashDeflectEnabled;
    public int DashExtraCharges;

    public static PackStats Baseline => new PackStats
    {
        PrimaryCritMultiplier = 1f,
        SecondaryCritMultiplier = 1f,
        DashDamageWindowMultiplier = 1f
    };
}
