using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RunRelicsDto
{
    public List<string> Held = new List<string>();
}

/// <summary>
/// run.relics — relics collected this run, by RelicIds. Run scope like coins
/// (wiped on death); its own section so relics can become a secondary
/// currency later without touching the others.
/// </summary>
public class RunRelicsSection : SaveSection<RunRelicsDto>
{
    private readonly Func<RunStats> _run;

    public RunRelicsSection(Func<RunStats> run) => _run = run;

    public override string Key => "run.relics";
    public override SaveScope Scope => SaveScope.Run;

    protected override RunRelicsDto Capture()
    {
        RunRelicsDto dto = new RunRelicsDto();
        bool[] held = _run().RelicsHeld;
        for (int i = 0; i < held.Length; i++)
            if (held[i]) dto.Held.Add(RelicIds.ToId(i));
        return dto;
    }

    protected override void Restore(RunRelicsDto dto)
    {
        RunStats run = _run();
        run.RelicsHeld = new bool[RelicIds.Count];
        run.UniqueRelicsCollectedThisRun = 0;

        foreach (string id in dto.Held)
        {
            if (!RelicIds.TryParse(id, out int index))
            {
                Debug.LogWarning($"[Save] run.relics: unknown relic '{id}' — skipped.");
                continue;
            }
            if (run.RelicsHeld[index]) continue;
            run.RelicsHeld[index] = true;
            run.UniqueRelicsCollectedThisRun++;
        }
    }

    protected override RunRelicsDto CreateDefault() => new RunRelicsDto();
}
