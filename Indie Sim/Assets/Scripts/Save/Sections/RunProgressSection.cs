using System;

[Serializable]
public class RunProgressDto
{
    public int DungeonNumber = 1;
    public int DungeonsCleared;
    public int DungeonSizeIncrement;
    public RunResumePoint ResumePoint = RunResumePoint.LevelStart;
    public int Kills;
}

/// <summary>
/// run.progress — where the run is (dungeon number + resume point, spec §4)
/// and its kill count. Writing it is what marks a slot "active run".
/// </summary>
public class RunProgressSection : SaveSection<RunProgressDto>
{
    private readonly Func<RunStats> _run;

    public RunProgressSection(Func<RunStats> run) => _run = run;

    public override string Key => "run.progress";
    public override SaveScope Scope => SaveScope.Run;

    protected override RunProgressDto Capture()
    {
        RunStats run = _run();
        return new RunProgressDto
        {
            DungeonNumber = run.CurrentDungeonLevel,
            DungeonsCleared = run.DungeonsClearedThisRun,
            DungeonSizeIncrement = run.DungeonSizeIncrement,
            ResumePoint = run.ResumePoint,
            Kills = run.KillsThisRun
        };
    }

    protected override void Restore(RunProgressDto dto)
    {
        RunStats run = _run();
        run.CurrentDungeonLevel = dto.DungeonNumber;
        run.DungeonsClearedThisRun = dto.DungeonsCleared;
        run.DungeonSizeIncrement = dto.DungeonSizeIncrement;
        run.ResumePoint = dto.ResumePoint;
        run.KillsThisRun = dto.Kills;
    }

    protected override RunProgressDto CreateDefault() => new RunProgressDto();

    protected override void FillSummary(RunProgressDto dto, SlotSummary summary)
    {
        summary.DungeonNumber = dto.DungeonNumber;
        summary.HasActiveRun = true;
    }
}
