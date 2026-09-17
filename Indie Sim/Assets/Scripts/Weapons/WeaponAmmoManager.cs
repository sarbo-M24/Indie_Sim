using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class WeaponAmmoManager : MonoBehaviour
{
    // ✅ Bug 1 Fix: Add singleton
    public static WeaponAmmoManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerConeShooter playerShooter;
    [SerializeField] private AudioSource audioSource;

    [Header("Ammo UI")]
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private Image reloadIcon;

    [Header("Debug Info")]
    [SerializeField] private int currentAmmoInMagazine;
    [SerializeField] private bool isReloading = false;

    private WeaponData currentWeapon;
    private Coroutine reloadCoroutine;

    // Tracks per-weapon ammo so switching weapons doesn't reset ammo
    private Dictionary<WeaponData, int> _currentAmmo = new Dictionary<WeaponData, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ReinitialiseUIReferences());
    }

    private IEnumerator ReinitialiseUIReferences()
    {
        yield return null; // wait one frame for scene to finish loading

        // Re-grab UI references from the HUD canvas specifically. Scenes can
        // now have more than one Canvas (e.g. BossArena's DemoCompleteCanvas
        // alongside the HUD), so FindFirstObjectByType<Canvas>() is no longer
        // reliable — it can return whichever canvas happens to exist first,
        // not necessarily the one with the ammo UI as a child.
        //
        // "AmmoText" was never the real object name (the ammo count TMP object
        // is actually named "Bullet number" inside Player Canvas HardcoreMode.prefab)
        // and it is not a direct child of the Canvas, so the previous single-level
        // Transform.Find() could never match — this silently broke ammo UI on any
        // runtime-instantiated player (BossArena) that has no scene-baked Inspector
        // override to fall back on. Search recursively by the real name instead.
        Transform ammoTextTransform = null;
        Transform reloadIconTransform = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (ammoTextTransform == null)
                ammoTextTransform = FindDeepChild(c.transform, "Bullet number");
            // TODO: "ReloadIcon" is an unconfirmed guess — no object with this name
            // exists anywhere in the project. Confirm the real name of the reload
            // fill-icon object in Player Canvas HardcoreMode.prefab and fix this string.
            if (reloadIconTransform == null)
                reloadIconTransform = FindDeepChild(c.transform, "ReloadIcon");
        }

        if (ammoTextTransform != null)
            ammoText = ammoTextTransform.GetComponent<TMP_Text>();
        if (reloadIconTransform != null)
            reloadIcon = reloadIconTransform.GetComponent<Image>();

        // Re-grab player shooter reference
        playerShooter = FindFirstObjectByType<PlayerConeShooter>();

        Debug.Log($"[AmmoManager] UI re-grabbed — ammoText: {ammoText}, reloadIcon: {reloadIcon}, playerShooter: {playerShooter}");

        UpdateAmmoUI();
    }

    // Recursive child search — the ammo UI is nested more than one level under
    // its Canvas, so Transform.Find() (direct children only) cannot locate it.
    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnEnable()
    {
        WeaponInventory.OnWeaponChanged += OnWeaponSwitched;
    }

    private void OnDisable()
    {
        WeaponInventory.OnWeaponChanged -= OnWeaponSwitched;
    }

    private void Start()
    {
        if (playerShooter == null)
            playerShooter = GetComponent<PlayerConeShooter>();

        // Scene-local (Phase 6) — resume ammo counts carried over from this
        // run's previous scene instance (RoguelikeMode -> BossArena) before
        // falling back to a full magazine for anything not seen yet.
        if (GameSession.Instance != null)
        {
            foreach (KeyValuePair<WeaponData, int> saved in GameSession.Instance.CurrentRun.WeaponAmmo)
                _currentAmmo[saved.Key] = saved.Value;
        }

        if (playerShooter != null)
        {
            currentWeapon = playerShooter.GetCurrentWeapon();
            if (currentWeapon != null)
            {
                // Only fill to max if we haven't saved ammo for this weapon yet
                if (!_currentAmmo.ContainsKey(currentWeapon))
                    _currentAmmo[currentWeapon] = GetCurrentMaxAmmo();

                currentAmmoInMagazine = _currentAmmo[currentWeapon];
                SyncAmmoToRunStats(currentWeapon);
                UpdateAmmoUI();
            }
        }
    }

    // Mirrors one weapon's ammo count into GameSession.CurrentRun so it
    // survives this object being destroyed on the next scene load.
    private void SyncAmmoToRunStats(WeaponData weapon)
    {
        if (GameSession.Instance == null || weapon == null) return;
        GameSession.Instance.CurrentRun.WeaponAmmo[weapon] = _currentAmmo[weapon];
    }

    private void Update()
    {
        if (currentWeapon == null && playerShooter != null)
        {
            currentWeapon = playerShooter.GetCurrentWeapon();
            if (currentWeapon != null)
            {
                if (!_currentAmmo.ContainsKey(currentWeapon))
                    _currentAmmo[currentWeapon] = GetCurrentMaxAmmo();

                currentAmmoInMagazine = _currentAmmo[currentWeapon];
                Debug.Log($"[AmmoManager] Late-initialized weapon: {currentWeapon.weaponName} - Ammo: {currentAmmoInMagazine}");
                UpdateAmmoUI();
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.R) && !isReloading)
            TryReload();

        if (currentAmmoInMagazine <= 0 && !isReloading)
            TryReload();
    }

    public bool CanShoot()
    {
        return currentAmmoInMagazine > 0 && !isReloading;
    }

    public void ConsumeBullet()
    {
        if (currentAmmoInMagazine > 0)
        {
            currentAmmoInMagazine--;

            // ✅ Keep dictionary in sync so weapon switch restores correct count
            if (currentWeapon != null)
            {
                _currentAmmo[currentWeapon] = currentAmmoInMagazine;
                SyncAmmoToRunStats(currentWeapon);
            }

            Debug.Log($"[AmmoManager] Ammo: {currentAmmoInMagazine}/{GetCurrentMaxAmmo()}");
            UpdateAmmoUI();
        }
    }

    public void TryReload()
    {
        if (currentWeapon == null)
        {
            Debug.LogWarning("[AmmoManager] TryReload called but currentWeapon is null!");
            return;
        }

        if (isReloading)
        {
            Debug.Log("[AmmoManager] Already reloading!");
            return;
        }

        if (currentAmmoInMagazine >= GetCurrentMaxAmmo())
        {
            Debug.Log("[AmmoManager] Magazine already full!");
            return;
        }

        if (reloadCoroutine != null) StopCoroutine(reloadCoroutine);
        reloadCoroutine = StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        Debug.Log($"[AmmoManager] Reloading {currentWeapon.weaponName}...");

        if (ammoText != null) ammoText.text = "...";

        if (audioSource != null && currentWeapon.reloadSound != null)
            audioSource.PlayOneShot(currentWeapon.reloadSound);

        SetReloadFill(0f);

        float elapsed = 0f;
        while (elapsed < currentWeapon.reloadTime)
        {
            elapsed += Time.deltaTime;
            SetReloadFill(Mathf.Clamp01(elapsed / currentWeapon.reloadTime));
            yield return null;
        }

        SetReloadFill(1f);

        int maxAmmo = GetCurrentMaxAmmo();
        currentAmmoInMagazine = maxAmmo;

        // ✅ Keep dictionary in sync
        if (currentWeapon != null)
        {
            _currentAmmo[currentWeapon] = currentAmmoInMagazine;
            SyncAmmoToRunStats(currentWeapon);
        }

        isReloading = false;
        Debug.Log($"[AmmoManager] Reload complete! Ammo: {currentAmmoInMagazine}/{maxAmmo}");
        UpdateAmmoUI();
        reloadCoroutine = null;
    }

    private void SetReloadFill(float amount)
    {
        if (reloadIcon != null) reloadIcon.fillAmount = amount;
    }

    public void OnWeaponSwitched(WeaponData newWeapon)
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        if (currentWeapon != null)
        {
            _currentAmmo[currentWeapon] = currentAmmoInMagazine;
            SyncAmmoToRunStats(currentWeapon);
        }

        isReloading = false;
        currentWeapon = newWeapon;

        // ✅ Instantly refill ALL weapons to max on switch
        foreach (WeaponData weapon in new List<WeaponData>(_currentAmmo.Keys))
        {
            int max = weapon.magazineCapacity;
            _currentAmmo[weapon] = max;
            SyncAmmoToRunStats(weapon);
        }

        if (newWeapon != null)
        {
            if (!_currentAmmo.ContainsKey(newWeapon))
                _currentAmmo[newWeapon] = newWeapon.magazineCapacity;
            currentAmmoInMagazine = _currentAmmo[newWeapon];
        }

        Debug.Log($"[AmmoManager] Switched to {currentWeapon?.weaponName} - All weapons refilled. Ammo: {currentAmmoInMagazine}/{GetCurrentMaxAmmo()}");
        UpdateAmmoUI();
    }

    // ✅ Bug 2 Fix: Add RefillAmmo — called by UpgradeManager after an ammo upgrade
    /// <summary>
    /// Called by UpgradeManager when an ammo upgrade is applied.
    /// Tops up the upgraded weapon to its new max capacity.
    /// </summary>
    public void RefillAmmo(WeaponData weapon)
    {
        if (weapon == null) return;

        int newMax = weapon.magazineCapacity;

        _currentAmmo[weapon] = newMax;
        SyncAmmoToRunStats(weapon);

        // If the upgraded weapon is currently equipped, update the live counter too
        if (currentWeapon == weapon)
        {
            currentAmmoInMagazine = newMax;
            UpdateAmmoUI();
        }

        Debug.Log($"[AmmoManager] Refilled {weapon.weaponName} to {newMax} after upgrade.");
    }

    public void CancelReload()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }
        isReloading = false;
        SetReloadFill(1f);
        UpdateAmmoUI();
        Debug.Log("[AmmoManager] Reload cancelled!");
    }

    private void UpdateAmmoUI()
    {
        if (ammoText == null) return;
        ammoText.text = $"{currentAmmoInMagazine} / {GetCurrentMaxAmmo()} rounds";
    }

    public void InitialiseAmmo(WeaponData[] weapons)
    {
        _currentAmmo.Clear();
        foreach (WeaponData weapon in weapons)
        {
            if (weapon == null) continue;
            _currentAmmo[weapon] = weapon.magazineCapacity;
        }
    }

    private int GetCurrentMaxAmmo()
    {
        if (currentWeapon == null) return 0;
        return currentWeapon.magazineCapacity;
    }

    public int GetCurrentAmmo() => currentAmmoInMagazine;
    public int GetMagazineCapacity() => GetCurrentMaxAmmo();
    public bool IsReloading() => isReloading;
    public float GetReloadProgress() => 0f;
}