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

    // Bounce
    public int PrimaryBounceCount;
    public int SecondaryBounceCount;

    // Stomp
    public float StompBonusRadius;
    public int StompBonusDamage;
    public int StompBulletCount;

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
