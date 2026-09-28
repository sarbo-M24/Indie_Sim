using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the player's current weapon loadout and handles switching between weapons.
/// This is the "backpack" - it knows what guns you're carrying and which one is active.
/// Switch Weapon (mouse wheel / Y by default, rebindable) cycles to the next weapon.
/// </summary>
public class WeaponInventory : MonoBehaviour
{
    [Header("Weapon Loadout")]
    [SerializeField] private WeaponData[] availableWeapons; // The guns you're carrying this run
    [SerializeField] private int currentWeaponIndex = 0;

    private WeaponData currentWeapon;

    // ✅ EVENT: Other scripts can listen to this without needing a reference to this script
    public static event Action<WeaponData> OnWeaponChanged;

    private Dictionary<WeaponData, int> _currentAmmo = new Dictionary<WeaponData, int>();

    #region Unity Lifecycle

    // Shared instance (InputManager) — subscribe per enable so a destroyed player leaves no callbacks behind.
    private void OnEnable() => InputManager.Controls.Player.SwitchWeapon.performed += OnSwitchWeapon;
    private void OnDisable() => InputManager.Controls.Player.SwitchWeapon.performed -= OnSwitchWeapon;

    private void OnSwitchWeapon(InputAction.CallbackContext context) => SwitchToNextWeapon();

    private void Start()
    {
        // Find the first unlocked weapon and equip it
        InitializeStartingWeapon();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Equips the carried-over weapon if there is one, else the first weapon
    /// in the loadout.
    /// </summary>
    private void InitializeStartingWeapon()
    {
        if (availableWeapons == null || availableWeapons.Length == 0)
        {
            Debug.LogError("[WeaponInventory] No weapons assigned! Add weapons to the availableWeapons array.");
            return;
        }

        // The loadout is fixed on the prefab for now; mirrored so run.weapons
        // saves what the player owns (weapon unlocks will add to this).
        if (GameSession.Instance != null)
            GameSession.Instance.CurrentRun.OwnedWeapons = new List<WeaponData>(availableWeapons);

        // Scene-local (Phase 6) — resume the weapon equipped when this run's
        // previous scene instance was destroyed.
        WeaponData carriedOver = GameSession.Instance != null ? GameSession.Instance.CurrentRun.EquippedWeapon : null;
        if (carriedOver != null)
        {
            int carriedIndex = System.Array.IndexOf(availableWeapons, carriedOver);
            if (carriedIndex != -1)
            {
                SwitchToWeapon(carriedIndex);
                return;
            }
        }

        SwitchToWeapon(0);
    }

    #endregion

    #region Weapon Switching

    /// <summary>
    /// Switches to a specific weapon by index.
    /// </summary>
    /// <param name="weaponIndex">Index in the availableWeapons array</param>
    public void SwitchToWeapon(int weaponIndex)
    {
        // Validate index
        if (weaponIndex < 0 || weaponIndex >= availableWeapons.Length)
        {
            Debug.LogWarning($"[WeaponInventory] Invalid weapon index: {weaponIndex}");
            return;
        }

        WeaponData targetWeapon = availableWeapons[weaponIndex];

        // Switch to the weapon
        currentWeaponIndex = weaponIndex;
        currentWeapon = targetWeapon;

        if (GameSession.Instance != null)
            GameSession.Instance.CurrentRun.EquippedWeapon = currentWeapon;

        Debug.Log($"[WeaponInventory] ✅ Switched to: {currentWeapon.weaponName}");

        // ✅ Notify all listeners (UI, shooter, ammo manager, etc.)
        OnWeaponChanged?.Invoke(currentWeapon);
    }

    /// <summary>
    /// Switches to the next weapon in the loadout (cycles forward). The only
    /// switch direction — the Switch Weapon action calls this.
    /// </summary>
    public void SwitchToNextWeapon()
    {
        if (availableWeapons.Length == 0) return;

        int nextIndex = (currentWeaponIndex + 1) % availableWeapons.Length;
        SwitchToWeapon(nextIndex);
    }

    #endregion

    #region Public Getters

    /// <summary>
    /// Returns the currently equipped weapon.
    /// </summary>
    public WeaponData GetCurrentWeapon()
    {
        return currentWeapon;
    }

    /// <summary>
    /// Returns the name of the currently equipped weapon.
    /// </summary>
    public string GetCurrentWeaponName()
    {
        return currentWeapon?.weaponName ?? "None";
    }

    /// <summary>
    /// Returns the full array of weapons in this inventory.
    /// Useful for UI that shows all weapons (locked/unlocked).
    /// </summary>
    public WeaponData[] GetAllWeapons()
    {
        return availableWeapons;
    }

    /// <summary>
    /// Returns the current weapon's index in the array.
    /// </summary>
    public int GetCurrentWeaponIndex()
    {
        return currentWeaponIndex;
    }


    /// <summary>
    /// Returns current ammo for a weapon. Initialises to full mag on first access.
    /// </summary>
    public int GetCurrentAmmo(WeaponData weapon)
    {
        if (weapon == null) return 0;

        if (!_currentAmmo.ContainsKey(weapon))
            _currentAmmo[weapon] = weapon.magazineCapacity;

        return _currentAmmo[weapon];
    }

    /// <summary>
    /// Call this every time a bullet is fired.
    /// </summary>
    public void ConsumeAmmo(WeaponData weapon, int amount = 1)
    {
        if (weapon == null) return;
        _currentAmmo[weapon] = Mathf.Max(0, GetCurrentAmmo(weapon) - amount);
    }

    /// <summary>
    /// Call this when reload finishes.
    /// </summary>
    public void RefillAmmo(WeaponData weapon)
    {
        if (weapon == null) return;
        _currentAmmo[weapon] = weapon.magazineCapacity;
    }

    /// <summary>
    /// Call this at run start / on death to wipe all ammo state.
    /// </summary>
    public void ResetAllAmmo()
    {
        _currentAmmo.Clear();
    }

    #endregion
}
