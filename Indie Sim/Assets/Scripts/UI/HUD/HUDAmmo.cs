using TMPro;
using UnityEngine;

/// <summary>
/// Shows the current weapon's magazine from WeaponAmmoManager ("..." while
/// reloading, same as the manager's own text). Only rewrites on change.
/// </summary>
public class HUDAmmo : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string format = "{0} / {1}";
    [SerializeField] private string reloadingText = "...";

    private int _shownAmmo = -1;
    private int _shownCapacity = -1;
    private bool _shownReloading;

    private void Update()
    {
        WeaponAmmoManager ammo = WeaponAmmoManager.Instance;
        if (ammo == null || label == null) return;

        int current = ammo.GetCurrentAmmo();
        int capacity = ammo.GetMagazineCapacity();
        bool reloading = ammo.IsReloading();
        if (current == _shownAmmo && capacity == _shownCapacity && reloading == _shownReloading) return;

        _shownAmmo = current;
        _shownCapacity = capacity;
        _shownReloading = reloading;
        label.text = reloading ? reloadingText : string.Format(format, current, capacity);
    }
}
