using System;

/// <summary>
/// Save composition root: builds the SaveService and registers every section,
/// once, at boot. Adding saved data later = one new SaveSection class + one
/// Register line here; SaveService itself never changes.
/// </summary>
public static class SaveBootstrap
{
    public static SaveService Create(GameSession session)
    {
        SaveService saves = new SaveService(SaveService.DefaultFolder);
        RegisterSections(saves, () => session.CurrentRun, () => session.Slot, () => session.Persistent, ContentCatalog.Load());
        return saves;
    }

    /// <summary>
    /// Accessors, not objects: GameSession swaps CurrentRun for a fresh
    /// instance on every new run, and sections must always see the live one.
    /// Public so SaveSelfTest can register the real sections against test data.
    /// </summary>
    public static void RegisterSections(SaveService saves, Func<RunStats> run, Func<SlotData> slot,
        Func<PersistentStats> persistent, ContentCatalog catalog)
    {
        // Global — profile.json
        saves.Register(new GlobalAchievementsSection(persistent));
        saves.Register(new GlobalStatsSection(persistent));
        saves.Register(new GlobalDemoSection(persistent));

        // Slot — survives death
        saves.Register(new SlotMetaSection(slot));

        // Run — wiped on death / victory
        saves.Register(new RunProgressSection(run));
        saves.Register(new RunEconomySection(run));
        saves.Register(new RunUpgradesSection(run, catalog));
        saves.Register(new RunRelicsSection(run));
        saves.Register(new RunWeaponsSection(run, catalog));
    }
}
