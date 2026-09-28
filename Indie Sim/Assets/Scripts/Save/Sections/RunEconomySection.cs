using System;

[Serializable]
public class RunEconomyDto
{
    public int Coins;
    public int MaxCoins;
    public int CoinsCollected;
}

/// <summary>
/// run.economy — coins (which are also HP, so there's no health field).
/// MaxCoins 0 is CoinManager's "fresh run" signal, so the default keeps it 0
/// and the Inspector starting values apply.
/// </summary>
public class RunEconomySection : SaveSection<RunEconomyDto>
{
    private readonly Func<RunStats> _run;

    public RunEconomySection(Func<RunStats> run) => _run = run;

    public override string Key => "run.economy";
    public override SaveScope Scope => SaveScope.Run;

    protected override RunEconomyDto Capture()
    {
        RunStats run = _run();
        return new RunEconomyDto { Coins = run.CurrentCoins, MaxCoins = run.MaxCoins, CoinsCollected = run.CoinsCollectedThisRun };
    }

    protected override void Restore(RunEconomyDto dto)
    {
        RunStats run = _run();
        run.CurrentCoins = dto.Coins;
        run.MaxCoins = dto.MaxCoins;
        run.CoinsCollectedThisRun = dto.CoinsCollected;
    }

    protected override RunEconomyDto CreateDefault() => new RunEconomyDto();
    protected override void FillSummary(RunEconomyDto dto, SlotSummary summary) => summary.Coins = dto.Coins;
}
