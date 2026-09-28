using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RunWeaponsDto
{
    public string Equipped;
    public List<string> Owned = new List<string>();
}

/// <summary>
/// run.weapons — equipped weapon and the owned loadout, by WeaponData.id.
/// Ammo is deliberately not saved (infinite reserve).
/// </summary>
public class RunWeaponsSection : SaveSection<RunWeaponsDto>
{
    private readonly Func<RunStats> _run;
    private readonly ContentCatalog _catalog;

    public RunWeaponsSection(Func<RunStats> run, ContentCatalog catalog)
    {
        _run = run;
        _catalog = catalog;
    }

    public override string Key => "run.weapons";
    public override SaveScope Scope => SaveScope.Run;

    protected override RunWeaponsDto Capture()
    {
        RunStats run = _run();
        RunWeaponsDto dto = new RunWeaponsDto { Equipped = run.EquippedWeapon != null ? run.EquippedWeapon.id : null };
        foreach (WeaponData weapon in run.OwnedWeapons)
            if (weapon != null) dto.Owned.Add(weapon.id);
        return dto;
    }

    protected override void Restore(RunWeaponsDto dto)
    {
        RunStats run = _run();

        run.EquippedWeapon = null;
        if (!string.IsNullOrEmpty(dto.Equipped))
        {
            if (_catalog.TryGetWeapon(dto.Equipped, out WeaponData equipped)) run.EquippedWeapon = equipped;
            else Debug.LogWarning($"[Save] run.weapons: unknown equipped weapon '{dto.Equipped}' — skipped.");
        }

        run.OwnedWeapons = new List<WeaponData>();
        foreach (string id in dto.Owned)
        {
            if (_catalog.TryGetWeapon(id, out WeaponData weapon)) run.OwnedWeapons.Add(weapon);
            else Debug.LogWarning($"[Save] run.weapons: unknown weapon '{id}' — skipped.");
        }
    }

    protected override RunWeaponsDto CreateDefault() => new RunWeaponsDto();
}
