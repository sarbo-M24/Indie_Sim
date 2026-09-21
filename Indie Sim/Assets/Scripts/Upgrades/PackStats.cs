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
    // No DashAoeRadius: per UpgradeSystemSpec.md, the AoE's radius is never
    // authored — it's derived at runtime from the dash's own travel distance.
    public int DashAoeDamage;
    public bool DashDeflectEnabled;
    public int DashExtraCharges;

    public static PackStats Baseline => new PackStats
    {
        PrimaryCritMultiplier = 1f,
        SecondaryCritMultiplier = 1f,
        DashDamageWindowMultiplier = 1f
    };
}
