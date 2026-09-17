/// <summary>
/// The aggregate every stat consumer queries. Rebuilt from scratch by
/// Pack.Recompute() each time the pack changes — never mutated piecemeal.
/// </summary>
[System.Serializable]
public struct PackStats
{
    // Crit
    public float PrimaryCritChance;
    public float PrimaryCritMultiplier;
    public float SecondaryCritChance;
    public float SecondaryCritMultiplier;

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
    public float DashAoeRadius;
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
