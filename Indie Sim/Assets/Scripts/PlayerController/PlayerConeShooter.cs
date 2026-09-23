using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The refined "Hands" script - purely handles the physics and math of shooting.
/// This script asks "What am I holding?" and then executes the shooting logic.
/// All VFX, UI, and progression are handled by other scripts now.
/// </summary>
public class PlayerConeShooter : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private Transform firePoint; // Where bullets originate
    [SerializeField] private Camera mainCamera; // For mouse-to-world conversion

    [Header("Detection Settings")]
    [SerializeField] private LayerMask enemyLayers = -1; // What counts as an enemy
    [SerializeField] private LayerMask obstacleLayers; // Walls that block shots
    [SerializeField] private LayerMask bulletTrailWallLayers; // Walls that stop bullet trails
    [SerializeField] private LayerMask bulletTrailEnemyLayers; // Enemies that bullets can hit

    [Header("Bullet Visuals")]
    [SerializeField] private GameObject bulletProjectilePrefab; // Visual bullet trail prefab
    [SerializeField] private float bulletSpeed = 50f; // How fast the visual bullet travels

    [Header("Cone Visualization (Debug)")]
    [SerializeField] private bool showConeGizmo = true;
    [SerializeField] private Color coneGizmoColor = new Color(1f, 0f, 0f, 0.3f);

    [Header("Cone Edge Visualizer (Runtime)")]
    [SerializeField] private bool enableConeEdgeVisual = true;
    [SerializeField] private Color edgeLineColor = Color.yellow;
    [SerializeField] private float edgeLineWidth = 0.05f;
    [SerializeField] private float coneFlashDuration = 0.1f;

    [Header("Aiming Settings")]
    [SerializeField] private float aimDeadZone = 0.01f; // Minimum distance to register aim

    [Header("Bullet Bounce Upgrade")]
    [Tooltip("Max distance a bounce chain can travel from its first hit — keeps a multi-bounce chain contained to one area instead of walking hop-by-hop across the whole map.")]
    [SerializeField] private float maxBounceChainRange = 15f;

    [Header("External Component References")]
    [SerializeField] private WeaponAmmoManager ammoManager; // Handles ammo/reloading
    [SerializeField] private WeaponVFXHandler vfxHandler; // Handles all visual/audio effects
    [SerializeField] private WeaponInventory weaponInventory; // Knows what weapon we're holding

    private PlayerController playerController; // Read for the Dash Damage Window upgrade's multiplier

    // Gates firing while a menu (e.g. the upgrade store) is open.
    // Set via SetInputEnabled(), routed through RoguelikeManager.SetGameplayInputEnabled().
    private bool gameplayInputEnabled = true;
    public void SetInputEnabled(bool enabled) => gameplayInputEnabled = enabled;

    // Runtime state
    private WeaponData currentWeapon;
    private float nextFireTime = 0f;
    private bool isFiring = false;
    private bool wasShooting = false;
    private Vector2 currentShootingDirection = Vector2.zero;
    private List<IDamageable> damageableTargets = new List<IDamageable>();

    // Cone edge visual lines
    private LineRenderer leftEdgeLine;
    private LineRenderer rightEdgeLine;
    private bool isShowingConeFlash = false;

    // Input system
    private PlayerControls inputActions;

    #region Unity Lifecycle

    private void Awake()
    {
        // Set up input system
        inputActions = new PlayerControls();

        inputActions.Player.Fire.performed += ctx => isFiring = true;
        inputActions.Player.Fire.canceled += ctx => isFiring = false;

        if (mainCamera == null) mainCamera = Camera.main;
        playerController = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        inputActions.Enable();

        // Subscribe to weapon changes
        WeaponInventory.OnWeaponChanged += HandleWeaponChanged;
    }

    private void OnDisable()
    {
        inputActions.Disable();

        // Unsubscribe to prevent memory leaks
        WeaponInventory.OnWeaponChanged -= HandleWeaponChanged;
    }

    private void Start()
    {
        // Get initial weapon from inventory
        if (weaponInventory != null)
        {
            currentWeapon = weaponInventory.GetCurrentWeapon();
        }

        // Initialize cone edge visualization
        if (enableConeEdgeVisual)
        {
            InitializeConeEdgeLines();
        }
    }

    private void Update()
    {
        // Fallback for mouse input (works with both old and new input systems)
        if (Input.GetMouseButton(0))
        {
            isFiring = true;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isFiring = false;
        }
    }

    private void FixedUpdate()
    {
        if (currentWeapon == null) return;

        // Calculate shooting direction from player to mouse
        Vector2 shootDirection = GetMouseAimDirection();

        // Check if we can shoot (has ammo, aiming in valid direction)
        bool hasAmmo = ammoManager != null ? ammoManager.CanShoot() : true;
        bool isAiming = shootDirection.magnitude > aimDeadZone;
        bool shouldShoot = gameplayInputEnabled && isFiring && isAiming && hasAmmo;

        if (shouldShoot)
        {
            currentShootingDirection = shootDirection;

            // Check fire rate cooldown
            if (Time.time >= nextFireTime)
            {
                FireCone(shootDirection);
                nextFireTime = Time.time + (1f / GetDynamicFireRate());

                // Consume ammo
                if (ammoManager != null)
                {
                    ammoManager.ConsumeBullet();
                }
            }

            wasShooting = true;
        }
        else
        {
            currentShootingDirection = Vector2.zero;

            // Hide cone visuals when not shooting
            if (!isShowingConeFlash && enableConeEdgeVisual)
            {
                HideConeEdgeVisual();
            }

            // Notify VFX handler when stopping
            if (wasShooting)
            {
                OnStopShooting();
            }

            wasShooting = false;
        }
    }

    #endregion

    #region Weapon Change Handling

    /// <summary>
    /// Called when the weapon inventory changes weapons.
    /// Updates our local reference to the current weapon.
    /// </summary>
    private void HandleWeaponChanged(WeaponData newWeapon)
    {
        currentWeapon = newWeapon;
        Debug.Log($"[PlayerConeShooter] Now shooting with: {newWeapon.weaponName}");
    }

    #endregion

    #region Core Shooting Logic

    /// <summary>
    /// Master firing method - detects targets, creates bullet trails, plays effects.
    /// </summary>
    private void FireCone(Vector2 direction)
    {
        // 1. Detect all enemies in the cone
        damageableTargets.Clear();
        DetectDamageableTargetsInCone(direction);

        // 2. Create bullet trails and apply damage
        CreateBulletTrailsAndDamage(direction);

        // 3. Play all visual/audio effects (delegated to VFX handler)
        if (vfxHandler != null)
        {
            vfxHandler.PlayShootEffects(direction);
        }

        // 4. Show cone edge flash (if enabled)
        if (enableConeEdgeVisual)
        {
            ShowConeFlash(direction);
        }
    }

    /// <summary>
    /// Detects all damageable targets within the weapon's cone area.
    /// Filters by angle and distance based on weapon's trapezium shape.
    /// </summary>
    private void DetectDamageableTargetsInCone(Vector2 direction)
    {
        damageableTargets.Clear();

        // Detect all colliders in range
        Collider2D[] colliders = Physics2D.OverlapCircleAll(firePoint.position, currentWeapon.coneRange, enemyLayers);

        foreach (Collider2D collider in colliders)
        {
            IDamageable damageable = collider.GetComponent<IDamageable>();
            if (damageable == null) continue;
            if (damageable.IsDead()) continue;

            Vector2 closestPoint = collider.ClosestPoint(firePoint.position);
            Vector2 directionToTarget = (closestPoint - (Vector2)firePoint.position).normalized;

            if (directionToTarget.sqrMagnitude < 0.001f)
            {
                damageableTargets.Add(damageable); // treat as always hit if overlapping
                continue;
            }

            float distanceToTarget = Vector2.Distance(firePoint.position, closestPoint);

            // Check if target is within the trapezium cone
            if (IsTargetInTrapezium(firePoint.position, direction, distanceToTarget, directionToTarget))
            {
                damageableTargets.Add(damageable);
            }
        }
    }

    /// <summary>
    /// Checks if a target is within the weapon's trapezium-shaped cone.
    /// </summary>
    private bool IsTargetInTrapezium(Vector2 origin, Vector2 direction, float distance, Vector2 directionToTarget)
    {
        float allowedAngle = currentWeapon.GetAngleAtDistance(distance);
        float angleToTarget = Vector2.Angle(direction, directionToTarget);
        return angleToTarget <= allowedAngle;
    }

    #endregion

    #region Bullet Trail & Damage Logic

    /// <summary>
    /// Returns the weapon's final damage = base (from SO) + any bonus earned this run.
    /// Uses the new Phase 2 API — one method handles ALL weapon types automatically.
    /// </summary>
    /// <summary>
    /// FinalDmg = WeaponBaseDmg + UpgradeDmg + (RarityBonus x UpgradeDmg), per
    /// CritChanceCigData — WeaponBaseDmg is currentWeapon.baseDamagePerShot,
    /// the UpgradeDmg/RarityBonus portion is already resolved into
    /// Primary/SecondaryWeaponBonusDamage by Pack.Recompute(). The dash damage
    /// window's multiplier (separate upgrade) applies on top of that total.
    /// </summary>
    private int GetDynamicWeaponDamage()
    {
        int baseDamage = currentWeapon.baseDamagePerShot;
        int bonusDamage = 0;
        if (Pack.Instance != null)
        {
            PackStats stats = Pack.Instance.Stats;
            bonusDamage = IsSecondaryWeapon ? stats.SecondaryWeaponBonusDamage : stats.PrimaryWeaponBonusDamage;
        }

        float dashMultiplier = playerController != null ? playerController.GetDashDamageMultiplier() : 1f;
        return Mathf.RoundToInt((baseDamage + bonusDamage) * dashMultiplier);
    }

    /// <summary>Shotgun Pellet Count upgrade — flat extra pellets added to the shotgun's base 6-pellet spread.</summary>
    private int GetDynamicPelletCount()
    {
        int basePellets = 6;
        int bonus = Pack.Instance != null ? Pack.Instance.Stats.ShotgunBonusPellets : 0;
        return basePellets + bonus;
    }

    /// <summary>Fire Rate upgrade — % bonus per weapon slot, applied multiplicatively to the weapon's own base fire rate.</summary>
    private float GetDynamicFireRate()
    {
        float baseFireRate = currentWeapon.fireRate;
        float bonus = 0f;
        if (Pack.Instance != null)
        {
            PackStats stats = Pack.Instance.Stats;
            bonus = IsSecondaryWeapon ? stats.SecondaryFireRateBonus : stats.PrimaryFireRateBonus;
        }
        return baseFireRate * (1f + bonus);
    }

    /// <summary>
    /// Returns the weapon's final pierce count = base (from SO) + any bonus earned this run.
    /// </summary>
    private int GetDynamicPierceCount()
    {
        return currentWeapon.maxPierceCount;
    }

    /// <summary>Only two weapons exist (MachineGun=Standard, Shotgun=Shotgun) — Shotgun is the "secondary" slot.</summary>
    private bool IsSecondaryWeapon => currentWeapon != null && currentWeapon.weaponType == WeaponData.WeaponType.Shotgun;

    /// <summary>Crit upgrade — rolled per hit, not per shot, so a shotgun's 6 pellets can crit independently.</summary>
    private int ApplyCrit(int damage, out bool isCrit)
    {
        isCrit = false;
        if (Pack.Instance == null) return damage;

        PackStats stats = Pack.Instance.Stats;
        float critChance = IsSecondaryWeapon ? stats.SecondaryCritChance : stats.PrimaryCritChance;
        if (critChance <= 0f) return damage;

        isCrit = Random.value < critChance;
        if (!isCrit) return damage;

        float critMultiplier = IsSecondaryWeapon ? stats.SecondaryCritMultiplier : stats.PrimaryCritMultiplier;
        return Mathf.RoundToInt(damage * critMultiplier);
    }

    /// <summary>
    /// Applies crit-adjusted damage to one target and reports it (hit effect,
    /// crosshair feedback, score, floating number). Shared by the direct hit
    /// and every bounce hop so each one resolves identically.
    /// </summary>
    private void ApplyHitDamage(IDamageable target, int damage, Vector3 hitPosition)
    {
        int finalDamage = ApplyCrit(damage, out bool isCrit);
        target.TakeDamage(finalDamage);

        if (hitPosition != Vector3.zero && currentWeapon.hitEffect != null)
            Instantiate(currentWeapon.hitEffect, hitPosition, Quaternion.identity);

        CustomCrosshair crosshair = FindObjectOfType<CustomCrosshair>();
        if (crosshair != null) crosshair.ShowHitFeedback();

        Vector3 reportPosition = hitPosition != Vector3.zero ? hitPosition : target.GetGameObject().transform.position;
        ReportDamage(reportPosition, finalDamage, isCrit);
    }

    /// <summary>
    /// Bounce upgrade — chains the hit to the nearest not-yet-hit enemy in
    /// line of sight, up to the pack's bounce count for this weapon slot.
    /// Appends each hop's position to waypoints so the visual bullet follows
    /// the chain instead of stopping at the first target.
    /// </summary>
    private void ChainBounces(IDamageable firstTarget, Vector3 firstHitPosition, int damage, List<Vector3> waypoints)
    {
        if (Pack.Instance == null) return;
        int bounceCount = IsSecondaryWeapon ? Pack.Instance.Stats.SecondaryBounceCount : Pack.Instance.Stats.PrimaryBounceCount;
        if (bounceCount <= 0) return;

        List<IDamageable> hitSoFar = new List<IDamageable> { firstTarget };
        Vector3 currentPos = firstHitPosition;

        for (int i = 0; i < bounceCount; i++)
        {
            IDamageable nextTarget = FindNearestBounceTarget(currentPos, firstHitPosition, hitSoFar, out Vector3 nextPos);
            if (nextTarget == null) break;

            ApplyHitDamage(nextTarget, damage, nextPos);
            waypoints.Add(nextPos);
            hitSoFar.Add(nextTarget);
            currentPos = nextPos;
        }
    }

    /// <summary>
    /// Finds the closest not-yet-hit, line-of-sight-clear enemy within
    /// coneRange of the current hop AND within maxBounceChainRange of the
    /// chain's original hit — the second check keeps a multi-bounce chain
    /// contained to one area instead of letting it walk hop-by-hop across
    /// the whole map.
    /// </summary>
    private IDamageable FindNearestBounceTarget(Vector3 fromPosition, Vector3 chainOrigin, List<IDamageable> exclude, out Vector3 hitPosition)
    {
        hitPosition = Vector3.zero;
        float bounceRange = currentWeapon != null ? currentWeapon.coneRange : 8f;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(fromPosition, bounceRange, enemyLayers);

        IDamageable closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider2D collider in colliders)
        {
            IDamageable candidate = collider.GetComponent<IDamageable>();
            if (candidate == null || candidate.IsDead() || exclude.Contains(candidate)) continue;

            Vector3 candidatePos = candidate.GetGameObject().transform.position;
            if (Vector3.Distance(chainOrigin, candidatePos) > maxBounceChainRange) continue;

            float distance = Vector3.Distance(fromPosition, candidatePos);
            if (distance >= closestDistance) continue;

            Vector2 direction = ((Vector2)candidatePos - (Vector2)fromPosition).normalized;
            RaycastHit2D wallCheck = Physics2D.Raycast(fromPosition, direction, distance, bulletTrailWallLayers);
            if (wallCheck.collider != null) continue;

            closest = candidate;
            closestDistance = distance;
            hitPosition = candidatePos;
        }

        return closest;
    }

    /// <summary>Animates the visual bullet through each waypoint segment in turn (multi-hop for a bounce chain).</summary>
    private IEnumerator AnimateBulletAlongPath(GameObject bullet, List<Vector3> waypoints)
    {
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            Vector3 segmentStart = waypoints[i];
            Vector3 segmentEnd = waypoints[i + 1];
            float distance = Vector3.Distance(segmentStart, segmentEnd);
            float travelTime = distance / bulletSpeed;
            float elapsed = 0f;

            while (elapsed < travelTime)
            {
                if (bullet == null) yield break;
                elapsed += Time.deltaTime;
                bullet.transform.position = Vector3.Lerp(segmentStart, segmentEnd, elapsed / travelTime);
                yield return null;
            }

            if (bullet == null) yield break;
            bullet.transform.position = segmentEnd;
        }

        Destroy(bullet, 0.2f); // Let trail fade
    }
    /// <summary>
    /// Creates visual bullet trails and applies damage based on weapon type.
    /// Standard weapons: 1 bullet to closest enemy
    /// Shotguns: Multiple pellets with spread
    /// </summary>
    private void CreateBulletTrailsAndDamage(Vector2 direction)
    {
        // Fetch the buffed stats right before we fire
        int actualDamage = GetDynamicWeaponDamage();

        if (currentWeapon.weaponType == WeaponData.WeaponType.Standard)
        {
            FireStandardWeapon(direction, actualDamage);
        }
        else if (currentWeapon.weaponType == WeaponData.WeaponType.Shotgun)
        {
            FireShotgun(direction, actualDamage);
        }
        else if (currentWeapon.weaponType == WeaponData.WeaponType.Piercer)
        {
            int actualPierceCount = GetDynamicPierceCount();
            FirePiercer(direction, actualDamage, actualPierceCount);
        }
    }

    /// <summary>
    /// Fires a single bullet at the closest target.
    /// </summary>
    private void FireStandardWeapon(Vector2 direction, int damage) // ✅ Added damage parameter
    {
        IDamageable closestTarget = GetClosestTarget(out Vector3 hitPosition);

        if (closestTarget != null)
        {
            Vector3 directionToTarget = (hitPosition - firePoint.position).normalized;
            float distanceToTarget = Vector3.Distance(firePoint.position, hitPosition);

            RaycastHit2D wallCheck = Physics2D.Raycast(firePoint.position, directionToTarget, distanceToTarget, bulletTrailWallLayers);

            if (wallCheck.collider != null)
            {
                Vector2 randomDirection = AddRandomSpread(direction, 5f);
                Vector3 randomEndPos = GetTrailEndPosition(firePoint.position, randomDirection, currentWeapon.coneRange);
                StartCoroutine(BulletTrailCoroutine(firePoint.position, randomEndPos, null, 0, Vector3.zero));
            }
            else
            {
                // ✅ Pass the dynamic damage here
                StartCoroutine(BulletTrailCoroutine(firePoint.position, hitPosition, closestTarget, damage, hitPosition));
            }
        }
        else
        {
            Vector2 randomDirection = AddRandomSpread(direction, 5f);
            Vector3 maxRangePosition = GetTrailEndPosition(firePoint.position, randomDirection, currentWeapon.coneRange);
            StartCoroutine(BulletTrailCoroutine(firePoint.position, maxRangePosition, null, 0, Vector3.zero));
        }
    }

    /// <summary>
    /// Fires multiple pellets in a spread pattern (shotgun behavior).
    /// </summary>
    private void FireShotgun(Vector2 direction, int damage) // ✅ Added damage parameter
    {
        int pelletsPerShot = GetDynamicPelletCount();
        float spreadAngle = currentWeapon.GetAngleAtDistance(currentWeapon.coneRange);

        List<IDamageable> hitTargets = new List<IDamageable>();

        for (int i = 0; i < pelletsPerShot; i++)
        {
            float angleOffset = Mathf.Lerp(-spreadAngle, spreadAngle, i / (float)(pelletsPerShot - 1));
            Vector2 pelletDirection = RotateVector(direction, angleOffset);

            IDamageable hitEnemy = GetTargetInDirection(pelletDirection, hitTargets);

            if (hitEnemy != null)
            {
                GameObject targetGO = hitEnemy.GetGameObject();
                Vector3 hitPos = targetGO.transform.position;
                float distanceToEnemy = Vector3.Distance(firePoint.position, hitPos);

                RaycastHit2D wallCheck = Physics2D.Raycast(firePoint.position, pelletDirection, distanceToEnemy, bulletTrailWallLayers);

                if (wallCheck.collider != null)
                {
                    Vector2 randomDirection = AddRandomSpread(pelletDirection, 5f);
                    Vector3 randomEndPos = GetTrailEndPosition(firePoint.position, randomDirection, currentWeapon.coneRange);
                    StartCoroutine(BulletTrailCoroutine(firePoint.position, randomEndPos, null, 0, Vector3.zero));
                }
                else
                {
                    // ✅ Pass the dynamic damage here
                    StartCoroutine(BulletTrailCoroutine(firePoint.position, hitPos, hitEnemy, damage, hitPos));
                    hitTargets.Add(hitEnemy);
                }
            }
            else
            {
                Vector2 randomDirection = AddRandomSpread(pelletDirection, 5f);
                Vector3 maxRangePosition = GetTrailEndPosition(firePoint.position, randomDirection, currentWeapon.coneRange);
                StartCoroutine(BulletTrailCoroutine(firePoint.position, maxRangePosition, null, 0, Vector3.zero));
            }
        }
    }

    /// <summary>
    /// Fires a piercing bullet that penetrates multiple enemies.
    /// </summary>
    private void FirePiercer(Vector2 direction, int damage, int pierceCount) // ✅ Added parameters
    {
        List<IDamageable> piercedEnemies = new List<IDamageable>();
        Vector3 currentPosition = firePoint.position;
        Vector2 bulletDirection = direction;
        int enemiesHit = 0;

        // ✅ Use the dynamic pierce count
        int maxPierces = pierceCount;

        while (enemiesHit < maxPierces)
        {
            IDamageable nextTarget = GetNextPiercingTarget(currentPosition, bulletDirection, piercedEnemies, out Vector3 hitPosition, out float distanceToTarget);

            if (nextTarget != null)
            {
                RaycastHit2D wallCheck = Physics2D.Raycast(currentPosition, bulletDirection, distanceToTarget, bulletTrailWallLayers);

                if (wallCheck.collider != null)
                {
                    Vector3 wallHitPos = wallCheck.point;
                    StartCoroutine(PiercingBulletTrailCoroutine(currentPosition, wallHitPos, piercedEnemies, damage)); // ✅ Dynamic damage
                    return;
                }

                piercedEnemies.Add(nextTarget);
                enemiesHit++;
                currentPosition = hitPosition;

                if (enemiesHit >= maxPierces)
                {
                    StartCoroutine(PiercingBulletTrailCoroutine(firePoint.position, hitPosition, piercedEnemies, damage)); // ✅ Dynamic damage
                    return;
                }
            }
            else
            {
                Vector3 maxRangePos = GetTrailEndPosition(currentPosition, bulletDirection, currentWeapon.coneRange);
                StartCoroutine(PiercingBulletTrailCoroutine(firePoint.position, maxRangePos, piercedEnemies, damage)); // ✅ Dynamic damage
                return;
            }
        }

        Vector3 fallbackEndPos = currentPosition + (Vector3)bulletDirection * currentWeapon.coneRange;
        StartCoroutine(PiercingBulletTrailCoroutine(firePoint.position, fallbackEndPos, piercedEnemies, damage)); // ✅ Dynamic damage
    }

    /// <summary>
    /// Coroutine for piercing bullets - damages all enemies in the list when fired.
    /// </summary>
    private IEnumerator PiercingBulletTrailCoroutine(Vector3 startPos, Vector3 endPos, List<IDamageable> targetsHit, int damagePerTarget)
    {
        // 1. APPLY DAMAGE INSTANTLY to all pierced enemies (crit rolled per target)
        foreach (IDamageable target in targetsHit)
        {
            if (target != null && !target.IsDead())
            {
                int finalDamage = ApplyCrit(damagePerTarget, out bool isCrit);
                target.TakeDamage(finalDamage);

                // Show hit effects at each enemy
                GameObject targetGO = target.GetGameObject();
                if (targetGO != null && currentWeapon.hitEffect != null)
                {
                    Instantiate(currentWeapon.hitEffect, targetGO.transform.position, Quaternion.identity);
                }

                ReportDamage(targetGO != null ? targetGO.transform.position : transform.position, finalDamage, isCrit);
            }
        }

        // Crosshair feedback if we hit anything
        if (targetsHit.Count > 0)
        {
            CustomCrosshair crosshair = FindObjectOfType<CustomCrosshair>();
            if (crosshair != null)
            {
                crosshair.ShowHitFeedback();
            }
        }

        // 2. SPAWN VISUAL PROJECTILE
        if (bulletProjectilePrefab == null) yield break;

        GameObject bullet = Instantiate(bulletProjectilePrefab, startPos, Quaternion.identity);

        // Calculate travel time
        float distance = Vector3.Distance(startPos, endPos);
        float travelTime = distance / bulletSpeed;
        float elapsed = 0f;

        // 3. ANIMATE PROJECTILE TO END (visual only)
        while (elapsed < travelTime)
        {
            if (bullet == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / travelTime;
            bullet.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        // Ensure bullet reaches end
        if (bullet != null)
        {
            bullet.transform.position = endPos;
            Destroy(bullet, 0.2f);
        }
    }

    /// <summary>
    /// Finds the next enemy in the piercing bullet's path.
    /// Excludes enemies already hit by this bullet.
    /// </summary>
    private IDamageable GetNextPiercingTarget(Vector3 startPos, Vector2 direction, List<IDamageable> excludeTargets, out Vector3 hitPosition, out float distance)
    {
        hitPosition = Vector3.zero;
        distance = 0f;

        if (damageableTargets.Count == 0) return null;

        IDamageable closestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (IDamageable target in damageableTargets)
        {
            if (target == null || target.IsDead()) continue;

            // ✅ Skip enemies already hit by this bullet
            if (excludeTargets.Contains(target)) continue;

            GameObject targetGO = target.GetGameObject();
            Vector3 targetPos = targetGO.transform.position;
            Vector2 directionToTarget = (targetPos - startPos).normalized;
            float distanceToTarget = Vector3.Distance(startPos, targetPos);

            // Check if target is in the bullet's direction (within 5° tolerance)
            float angleToTarget = Vector2.Angle(direction, directionToTarget);
            if (angleToTarget > 5f) continue;

            // Check if wall blocks
            RaycastHit2D hit = Physics2D.Raycast(startPos, directionToTarget, distanceToTarget, obstacleLayers);
            if (hit.collider != null) continue;

            // Found valid target
            if (distanceToTarget < closestDistance)
            {
                closestDistance = distanceToTarget;
                closestTarget = target;
                hitPosition = targetPos;
                distance = distanceToTarget;
            }
        }

        return closestTarget;
    }

    /// <summary>
    /// Coroutine that handles bullet trail animation.
    /// Applies damage INSTANTLY when fired, then animates the visual projectile.
    /// </summary>
    private IEnumerator BulletTrailCoroutine(Vector3 startPos, Vector3 endPos, IDamageable target, int damage, Vector3 hitPosition)
    {
        List<Vector3> waypoints = new List<Vector3> { startPos };

        // 1. APPLY DAMAGE INSTANTLY (hitscan behavior), then chain any bounces.
        if (target != null && damage > 0)
        {
            ApplyHitDamage(target, damage, hitPosition);
            waypoints.Add(hitPosition);
            ChainBounces(target, hitPosition, damage, waypoints);
        }
        else
        {
            waypoints.Add(endPos);
        }

        // 2. SPAWN VISUAL PROJECTILE (just for show) and animate through every hop
        if (bulletProjectilePrefab == null) yield break;

        GameObject bullet = Instantiate(bulletProjectilePrefab, startPos, Quaternion.identity);
        yield return AnimateBulletAlongPath(bullet, waypoints);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the closest damageable target from the detected targets list.
    /// Returns the target and outputs its hit position.
    /// </summary>
    private IDamageable GetClosestTarget(out Vector3 hitPosition)
    {
        hitPosition = Vector3.zero;

        if (damageableTargets.Count == 0) return null;

        IDamageable closestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (IDamageable target in damageableTargets)
        {
            if (target == null || target.IsDead()) continue;

            GameObject targetGO = target.GetGameObject();
            float distanceToTarget = Vector3.Distance(firePoint.position, targetGO.transform.position);

            if (distanceToTarget < closestDistance)
            {
                Vector3 directionToTarget = (targetGO.transform.position - firePoint.position).normalized;
                RaycastHit2D hit = Physics2D.Raycast(firePoint.position, directionToTarget, distanceToTarget, obstacleLayers);

                if (hit.collider != null) continue; // Wall blocks

                closestDistance = distanceToTarget;
                closestTarget = target;
                hitPosition = targetGO.transform.position;
            }
        }

        return closestTarget;
    }

    /// <summary>
    /// Finds a target in a specific direction (used for shotgun pellets).
    /// </summary>
    private IDamageable GetTargetInDirection(Vector2 direction, List<IDamageable> excludeTargets)
    {
        IDamageable closestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (IDamageable target in damageableTargets)
        {
            if (target == null || target.IsDead()) continue;

            GameObject targetGO = target.GetGameObject();
            Vector2 directionToTarget = (targetGO.transform.position - firePoint.position).normalized;
            float distanceToTarget = Vector3.Distance(firePoint.position, targetGO.transform.position);

            float angleToTarget = Vector2.Angle(direction, directionToTarget);
            if (angleToTarget > 5f) continue; // Not in pellet direction

            RaycastHit2D hit = Physics2D.Raycast(firePoint.position, directionToTarget, distanceToTarget, obstacleLayers);
            if (hit.collider != null) continue; // Wall blocks

            if (distanceToTarget < closestDistance)
            {
                closestDistance = distanceToTarget;
                closestTarget = target;
            }
        }

        return closestTarget;
    }

    /// <summary>
    /// Calculates where the bullet trail should end (checks for walls).
    /// </summary>
    private Vector3 GetTrailEndPosition(Vector3 startPos, Vector2 direction, float maxRange)
    {
        RaycastHit2D wallHit = Physics2D.Raycast(startPos, direction, maxRange, bulletTrailWallLayers);

        if (wallHit.collider != null)
        {
            return wallHit.point; // Hit wall
        }

        // No wall - full range
        return startPos + new Vector3(direction.x, direction.y, 0) * maxRange;
    }

    /// <summary>
    /// Adds random spread to a direction (for missed shots).
    /// </summary>
    private Vector2 AddRandomSpread(Vector2 direction, float maxSpreadDegrees)
    {
        float randomAngle = Random.Range(-maxSpreadDegrees, maxSpreadDegrees);
        return RotateVector(direction, randomAngle);
    }

    /// <summary>
    /// Rotates a 2D vector by an angle in degrees.
    /// </summary>
    private Vector2 RotateVector(Vector2 vector, float angleDegrees)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleRadians);
        float sin = Mathf.Sin(angleRadians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        );
    }

    /// <summary>
    /// Calculates direction from player to mouse cursor.
    /// </summary>
    private Vector2 GetMouseAimDirection()
    {
        // Resolve lazily, not once in Awake: the persistent camera lives in Boot
        // and is not guaranteed to exist yet on the first frames when this scene
        // is entered directly (additive scene loads complete at end of frame).
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null || firePoint == null) return Vector2.zero;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Mathf.Abs(mainCamera.transform.position.z - firePoint.position.z);

        Vector3 worldMousePos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2 direction = ((Vector2)worldMousePos - (Vector2)firePoint.position).normalized;

        return direction;
    }

   
    public void SetCamera(Camera newCamera)
    {
        mainCamera = newCamera;
        Debug.Log($"[PlayerConeShooter] Camera reassigned to: {newCamera.name}");
    }


    /// <summary>
    /// Called when player stops shooting.
    /// </summary>
    private void OnStopShooting()
    {
        if (vfxHandler != null)
        {
            vfxHandler.OnStopShooting();
        }
    }

    /// <summary>Fired on every landed gun hit — (weapon, damage, isCrit). Debug/telemetry hook only, no gameplay effect.</summary>
    public static event System.Action<WeaponData, int, bool> OnGunHit;

    /// <summary>
    /// Reports a landed hit to the score system and spawns a floating damage number.
    /// Called from every damage-application point (standard/shotgun/piercer).
    /// </summary>
    private void ReportDamage(Vector3 worldPosition, int damage, bool isCrit = false)
    {
        if (ScoreManager.Instance != null) ScoreManager.Instance.AddDamage(damage);
        if (DamageNumberManager.Instance != null) DamageNumberManager.Instance.Spawn(worldPosition, damage, isCrit);
        OnGunHit?.Invoke(currentWeapon, damage, isCrit);
    }

    #endregion

    #region Cone Edge Visualization

    /// <summary>
    /// Initializes line renderers for cone edge visualization.
    /// </summary>
    private void InitializeConeEdgeLines()
    {
        // Left edge
        GameObject leftEdgeObj = new GameObject("LeftEdgeLine");
        leftEdgeObj.transform.SetParent(transform);
        leftEdgeLine = leftEdgeObj.AddComponent<LineRenderer>();
        ConfigureEdgeLine(leftEdgeLine);

        // Right edge
        GameObject rightEdgeObj = new GameObject("RightEdgeLine");
        rightEdgeObj.transform.SetParent(transform);
        rightEdgeLine = rightEdgeObj.AddComponent<LineRenderer>();
        ConfigureEdgeLine(rightEdgeLine);
    }

    private void ConfigureEdgeLine(LineRenderer line)
    {
        line.positionCount = 2;
        line.startWidth = edgeLineWidth;
        line.endWidth = edgeLineWidth;
        line.startColor = edgeLineColor;
        line.endColor = edgeLineColor;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.sortingLayerName = "character";
        line.sortingOrder = 1;
        line.useWorldSpace = true;
        line.enabled = false;
    }

    private void ShowConeFlash(Vector2 direction)
    {
        if (isShowingConeFlash) return;
        StartCoroutine(ConeFlashCoroutine(direction));
    }

    private IEnumerator ConeFlashCoroutine(Vector2 direction)
    {
        isShowingConeFlash = true;
        UpdateConeEdgeVisual(direction);
        yield return new WaitForSeconds(coneFlashDuration);
        HideConeEdgeVisual();
        isShowingConeFlash = false;
    }

    private void UpdateConeEdgeVisual(Vector2 direction)
    {
        if (leftEdgeLine == null || rightEdgeLine == null || currentWeapon == null) return;

        Vector2[] trapeziumPoints = currentWeapon.GetTrapeziumPoints(firePoint.position, direction);

        leftEdgeLine.enabled = true;
        leftEdgeLine.SetPosition(0, trapeziumPoints[0]);
        leftEdgeLine.SetPosition(1, trapeziumPoints[3]);

        rightEdgeLine.enabled = true;
        rightEdgeLine.SetPosition(0, trapeziumPoints[1]);
        rightEdgeLine.SetPosition(1, trapeziumPoints[2]);
    }

    private void HideConeEdgeVisual()
    {
        if (leftEdgeLine != null) leftEdgeLine.enabled = false;
        if (rightEdgeLine != null) rightEdgeLine.enabled = false;
    }

    #endregion

    #region Gizmos (Debug Visualization)

    private void OnDrawGizmos()
    {
        if (!showConeGizmo || currentWeapon == null || firePoint == null) return;
        if (currentShootingDirection.magnitude < aimDeadZone) return;

        Vector2[] trapeziumPoints = currentWeapon.GetTrapeziumPoints(firePoint.position, currentShootingDirection);

        Gizmos.color = coneGizmoColor;

        Vector3[] points3D = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            points3D[i] = new Vector3(trapeziumPoints[i].x, trapeziumPoints[i].y, 0);
        }

        DrawGizmoTriangle(points3D[0], points3D[1], points3D[2]);
        DrawGizmoTriangle(points3D[0], points3D[2], points3D[3]);

        Gizmos.color = new Color(coneGizmoColor.r, coneGizmoColor.g, coneGizmoColor.b, 1f);
        Gizmos.DrawLine(points3D[0], points3D[1]);
        Gizmos.DrawLine(points3D[1], points3D[2]);
        Gizmos.DrawLine(points3D[2], points3D[3]);
        Gizmos.DrawLine(points3D[3], points3D[0]);
    }

    private void DrawGizmoTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        int steps = 10;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 start = Vector3.Lerp(p1, p2, t);
            Vector3 end = Vector3.Lerp(p1, p3, t);
            Gizmos.DrawLine(start, end);
        }
    }

    #endregion

    #region Public Getters

    public WeaponData GetCurrentWeapon() => currentWeapon;
    public bool IsShooting() => wasShooting;
    public int GetTargetsInCone() => damageableTargets.Count;

    /// <summary>Live per-shot damage (base + upgrade bonus + dash window multiplier), for debug display.</summary>
    public int GetLiveDamagePerShot() => currentWeapon != null ? GetDynamicWeaponDamage() : 0;
    public bool IsCurrentWeaponSecondary => IsSecondaryWeapon;

    #endregion
}
