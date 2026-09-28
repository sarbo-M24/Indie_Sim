using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HeldCigDto
{
    public string Id;
    public int Tier;
    public Rarity Rarity;
    public bool Burning;
}

[Serializable]
public class RunUpgradesDto
{
    public List<HeldCigDto> Held = new List<HeldCigDto>();
    public List<string> Purchased = new List<string>();
}

/// <summary>
/// run.upgrades — the held pack (per-instance tier/rarity/burning) and the
/// lineages bought this run. Saved by CigData.id; unknown IDs are skipped.
/// </summary>
public class RunUpgradesSection : SaveSection<RunUpgradesDto>
{
    private readonly Func<RunStats> _run;
    private readonly ContentCatalog _catalog;

    public RunUpgradesSection(Func<RunStats> run, ContentCatalog catalog)
    {
        _run = run;
        _catalog = catalog;
    }

    public override string Key => "run.upgrades";
    public override SaveScope Scope => SaveScope.Run;

    protected override RunUpgradesDto Capture()
    {
        RunStats run = _run();
        RunUpgradesDto dto = new RunUpgradesDto { Purchased = new List<string>(run.PurchasedCigIds) };
        foreach (CigInstance cig in run.HeldCigs)
        {
            if (cig?.Data == null) continue;
            dto.Held.Add(new HeldCigDto { Id = cig.Data.id, Tier = cig.RolledTier, Rarity = cig.RolledRarity, Burning = cig.IsBurning });
        }
        return dto;
    }

    protected override void Restore(RunUpgradesDto dto)
    {
        RunStats run = _run();

        run.HeldCigs = new List<CigInstance>();
        foreach (HeldCigDto held in dto.Held)
        {
            if (held == null) continue;
            if (!_catalog.TryGetCig(held.Id, out CigData data))
            {
                Debug.LogWarning($"[Save] run.upgrades: unknown upgrade '{held.Id}' — skipped.");
                continue;
            }
            run.HeldCigs.Add(new CigInstance { Data = data, RolledTier = held.Tier, RolledRarity = held.Rarity, IsBurning = held.Burning });
        }

        run.PurchasedCigIds = new List<string>();
        foreach (string id in dto.Purchased)
        {
            if (_catalog.TryGetCig(id, out _)) run.PurchasedCigIds.Add(id);
            else Debug.LogWarning($"[Save] run.upgrades: unknown purchased upgrade '{id}' — skipped.");
        }
    }

    protected override RunUpgradesDto CreateDefault() => new RunUpgradesDto();
}
