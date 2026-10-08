using System;
using System.Collections.Generic;

// Global-scope sections — profile.json, shared by every slot.

[Serializable]
public class AchievementsDto
{
    public List<string> Unlocked = new List<string>();
}

/// <summary>global.achievements — unlocked achievement IDs.</summary>
public class GlobalAchievementsSection : SaveSection<AchievementsDto>
{
    private readonly Func<PersistentStats> _stats;

    public GlobalAchievementsSection(Func<PersistentStats> stats) => _stats = stats;

    public override string Key => "global.achievements";
    public override SaveScope Scope => SaveScope.Global;

    protected override AchievementsDto Capture() =>
        new AchievementsDto { Unlocked = new List<string>(_stats().UnlockedAchievementIds) };

    protected override void Restore(AchievementsDto dto) =>
        _stats().UnlockedAchievementIds = new List<string>(dto.Unlocked);

    protected override AchievementsDto CreateDefault() => new AchievementsDto();
}

[Serializable]
public class LifetimeStatsDto
{
    public int TotalCoinsEverCollected;
    public int TotalEnemiesKilled;
    public int TotalRuns;
    public int BestRunDungeonsCleared;
}

/// <summary>global.stats — lifetime counters (also achievement progress).</summary>
public class GlobalStatsSection : SaveSection<LifetimeStatsDto>
{
    private readonly Func<PersistentStats> _stats;

    public GlobalStatsSection(Func<PersistentStats> stats) => _stats = stats;

    public override string Key => "global.stats";
    public override SaveScope Scope => SaveScope.Global;

    protected override LifetimeStatsDto Capture()
    {
        PersistentStats stats = _stats();
        return new LifetimeStatsDto
        {
            TotalCoinsEverCollected = stats.TotalCoinsEverCollected,
            TotalEnemiesKilled = stats.TotalEnemiesKilled,
            TotalRuns = stats.TotalRuns,
            BestRunDungeonsCleared = stats.BestRunDungeonsCleared
        };
    }

    protected override void Restore(LifetimeStatsDto dto)
    {
        PersistentStats stats = _stats();
        stats.TotalCoinsEverCollected = dto.TotalCoinsEverCollected;
        stats.TotalEnemiesKilled = dto.TotalEnemiesKilled;
        stats.TotalRuns = dto.TotalRuns;
        stats.BestRunDungeonsCleared = dto.BestRunDungeonsCleared;
    }

    protected override LifetimeStatsDto CreateDefault() => new LifetimeStatsDto();
}

[Serializable]
public class DemoDto
{
    public bool DevPanelUnlocked;
}

/// <summary>
/// global.demo — set when the final boss is defeated (PersistentStats.DemoCompleted).
/// Registered in every build so saves stay compatible; the dev-panel gating
/// that reads it is deferred (spec 4E).
/// </summary>
public class GlobalDemoSection : SaveSection<DemoDto>
{
    private readonly Func<PersistentStats> _stats;

    public GlobalDemoSection(Func<PersistentStats> stats) => _stats = stats;

    public override string Key => "global.demo";
    public override SaveScope Scope => SaveScope.Global;

    protected override DemoDto Capture() => new DemoDto { DevPanelUnlocked = _stats().DemoCompleted };
    protected override void Restore(DemoDto dto) => _stats().DemoCompleted = dto.DevPanelUnlocked;
    protected override DemoDto CreateDefault() => new DemoDto();
}

[Serializable]
public class TutorialDto
{
    public bool Completed;
}

/// <summary>
/// global.tutorial — set when the player leaves Tutorial.unity
/// (PersistentStats.TutorialCompleted). Until then every new run starts there.
/// </summary>
public class GlobalTutorialSection : SaveSection<TutorialDto>
{
    private readonly Func<PersistentStats> _stats;

    public GlobalTutorialSection(Func<PersistentStats> stats) => _stats = stats;

    public override string Key => "global.tutorial";
    public override SaveScope Scope => SaveScope.Global;

    protected override TutorialDto Capture() => new TutorialDto { Completed = _stats().TutorialCompleted };
    protected override void Restore(TutorialDto dto) => _stats().TutorialCompleted = dto.Completed;
    protected override TutorialDto CreateDefault() => new TutorialDto();
}
