using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Weapon Info")]
    [Tooltip("Stable save ID (lowercase, never renamed once shipped). Checked by Tools/Save/Validate Content IDs.")]
    public string id;
    public string weaponName;
    public Sprite weaponIcon;
    public WeaponType weaponType = WeaponType.Standard;

    [Header("Shooting Parameters")]
    public float coneAngle = 45f;
    public float coneRange = 8f;
    public float fireRate = 10f;
    public int baseDamagePerShot = 10;

    [Header("Trapezium Shape Settings")]
    [Range(0.1f, 1f)] public float baseWidthMultiplier = 0.3f;
    [Range(0.1f, 1f)] public float topWidthMultiplier = 0.7f;
    [Range(0f, 0.5f)] public float baseDistanceRatio = 0.2f;

    [Header("Shape Profile")]
    public TrapeziumProfile shapeProfile = TrapeziumProfile.Linear;
    [Range(0.5f, 3f)] public float expansionCurve = 1f;

    [Header("Audio")]
    public AudioClip shootSound;
    public AudioClip reloadSound;

    [Header("Visual Effects")]
    public GameObject muzzleFlashEffect;
    public GameObject hitEffect;

    [Header("Camera Recoil")]
    [Tooltip("Camera kick strength for this weapon, passed to CinemachineCursorLead.ApplyRecoil().")]
    public float recoilStrength = 0.5f;

    [Tooltip("Physical push-back applied to the player's Rigidbody2D on fire (0 = no push). Try 3-5 for shotgun.")]
    public float playerKnockback = 0f;

    [Header("Ammo Settings")]
    public int magazineCapacity = 30;
    public float reloadTime = 2f;
    public bool hasInfiniteReserve = true;

    [Header("Piercing Settings (Piercer only)")]
    [Tooltip("How many enemies the bullet can pierce through (0 = standard behavior)")]
    public int maxPierceCount = 3;

    // ─────────────────────────────────────────
    //  ENUMS
    // ─────────────────────────────────────────
    public enum WeaponType
    {
        Standard,   // Pistol, AK – damages closest enemy only
        Shotgun,    // Fires in a cone
        Piercer     // Shots pass through enemies
    }

    public enum TrapeziumProfile
    {
        Linear,
        EaseIn,
        EaseOut,
        Curved
    }

    // ─────────────────────────────────────────
    //  READ-ONLY HELPER METHODS
    //  These are safe – they never modify the SO.
    //  PlayerConeShooter uses these to read values.
    //  Upgrades are applied on top of these by
    //  PlayerUpgradeState (built in Phase 2).
    // ─────────────────────────────────────────
    public float GetAngleAtDistance(float distance)
    {
        if (distance >= coneRange) return GetMaxAngle();
        if (distance <= GetBaseDistance()) return GetBaseAngle();

        float normalizedDistance = (distance - GetBaseDistance()) / (coneRange - GetBaseDistance());
        float curveValue = ApplyExpansionProfile(normalizedDistance);
        return Mathf.Lerp(GetBaseAngle(), GetMaxAngle(), curveValue);
    }

    public float GetBaseAngle() => coneAngle * baseWidthMultiplier;
    public float GetMaxAngle() => coneAngle * topWidthMultiplier;
    public float GetBaseDistance() => coneRange * baseDistanceRatio;

    private float ApplyExpansionProfile(float t)
    {
        switch (shapeProfile)
        {
            case TrapeziumProfile.EaseIn: return t * t;
            case TrapeziumProfile.EaseOut: return 1f - (1f - t) * (1f - t);
            case TrapeziumProfile.Curved: return Mathf.Pow(t, expansionCurve);
            default: return t; // Linear
        }
    }

    public Vector2[] GetTrapeziumPoints(Vector2 origin, Vector2 direction)
    {
        Vector2[] points = new Vector2[4];
        float baseDistance = GetBaseDistance();
        float baseAngle = GetBaseAngle() * Mathf.Deg2Rad;
        float maxAngle = GetMaxAngle() * Mathf.Deg2Rad;

        Vector2 baseCenter = origin + direction * baseDistance;
        Vector2 topCenter = origin + direction * coneRange;
        Vector2 perp = new Vector2(-direction.y, direction.x);

        float baseWidth = baseDistance * Mathf.Tan(baseAngle);
        points[0] = baseCenter - perp * baseWidth;
        points[1] = baseCenter + perp * baseWidth;

        float topWidth = coneRange * Mathf.Tan(maxAngle);
        points[2] = topCenter + perp * topWidth;
        points[3] = topCenter - perp * topWidth;

        return points;
    }
}