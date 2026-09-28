using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4A–4C acceptance checks for SaveService, the real sections and Continue targeting, runnable from Tools/Save/Run Self-Test
/// (no Play mode needed). Works in a throwaway temp folder with its own test
/// sections — never touches real saves or GameSession.
/// </summary>
public static class SaveSelfTest
{
    private class RunDto { public int Value; public List<string> Items = new List<string>(); }
    private class SlotDto { public string Name = ""; }

    private class TestRunSection : SaveSection<RunDto>
    {
        private readonly int _version;
        public int Value;
        public List<string> Items = new List<string>();

        public TestRunSection(int version = 1) => _version = version;

        public override string Key => "test.run";
        public override SaveScope Scope => SaveScope.Run;
        public override int Version => _version;

        protected override RunDto Capture() => new RunDto { Value = Value, Items = new List<string>(Items) };
        protected override void Restore(RunDto dto) { Value = dto.Value; Items = new List<string>(dto.Items); }
        protected override RunDto CreateDefault() => new RunDto { Value = -1 };

        // v1 -> v2 adds 100, so the test can see the migration ran.
        protected override JToken Migrate(JToken payload, int fromVersion)
        {
            payload["Value"] = payload.Value<int>("Value") + 100;
            return payload;
        }

        protected override void FillSummary(RunDto dto, SlotSummary summary)
        {
            summary.Coins = dto.Value;
            summary.HasActiveRun = true;
        }
    }

    private class TestSlotSection : SaveSection<SlotDto>
    {
        public string Name = "";

        public override string Key => "test.slot";
        public override SaveScope Scope => SaveScope.Slot;

        protected override SlotDto Capture() => new SlotDto { Name = Name };
        protected override void Restore(SlotDto dto) => Name = dto.Name;
        protected override SlotDto CreateDefault() => new SlotDto();
        protected override void FillSummary(SlotDto dto, SlotSummary summary) => summary.SlotName = dto.Name;
    }

    private static int _failures;
    private static string _folder;

    [MenuItem("Tools/Save/Run Self-Test", priority = 60)]
    public static void Run()
    {
        _failures = 0;
        _folder = Path.Combine(Path.GetTempPath(), "1bk_save_selftest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);

        try
        {
            RoundTrip();
            BackupRecovery();
            GarbageIsCorrupted();
            UnknownSectionPreserved();
            MissingSectionGetsDefault();
            NewerSectionIsCorrupted();
            MigrationRuns();
            RunWipe();
            DebugSlotNeverTouchesDisk();

            // 4B — the real sections, against test data (never GameSession).
            RealSectionsRoundTrip();
            UnknownContentIdSkipped();
            RemovedRealSectionLoadsDefault();

            // 4C — Continue targeting.
            ContinueTargeting();
        }
        catch (Exception e)
        {
            _failures++;
            Debug.LogError($"[SaveSelfTest] Threw: {e}");
        }
        finally
        {
            Directory.Delete(_folder, recursive: true);
        }

        if (_failures == 0) Debug.Log("[SaveSelfTest] All checks passed.");
        else Debug.LogError($"[SaveSelfTest] {_failures} check(s) FAILED — see errors above. (Corruption tests also log expected errors.)");
    }

    // ─────────────────────────────────────────────────────────────────

    private static SaveService NewService(out TestRunSection run, out TestSlotSection slot, int runVersion = 1)
    {
        SaveService service = new SaveService(_folder);
        run = new TestRunSection(runVersion);
        slot = new TestSlotSection();
        service.Register(run);
        service.Register(slot);
        return service;
    }

    private static string SlotFile(int index) => Path.Combine(_folder, $"slot_{index}.json");

    private static void EditFile(string path, Action<JObject> edit)
    {
        JObject root = JObject.Parse(File.ReadAllText(path));
        edit(root);
        File.WriteAllText(path, root.ToString());
    }

    private static void Check(bool condition, string what)
    {
        if (condition) Debug.Log($"[SaveSelfTest] PASS  {what}");
        else { _failures++; Debug.LogError($"[SaveSelfTest] FAIL  {what}"); }
    }

    // ─────────────────────────────────────────────────────────────────

    private static void RoundTrip()
    {
        SaveService service = NewService(out TestRunSection run, out TestSlotSection slot);
        service.SetActiveSlot(0);
        run.Value = 42;
        run.Items = new List<string> { "a", "b" };
        slot.Name = "Alpha";
        Check(service.WriteActiveSlot(), "round trip: write succeeds");

        SaveService fresh = NewService(out TestRunSection run2, out TestSlotSection slot2);
        fresh.SetActiveSlot(0);
        Check(fresh.LoadActiveSlot() == LoadResult.Loaded, "round trip: load succeeds");
        Check(run2.Value == 42 && run2.Items.Count == 2 && run2.Items[1] == "b" && slot2.Name == "Alpha", "round trip: state identical");

        SlotInfo info = fresh.GetSlotInfo(0);
        Check(info.State == SlotState.ActiveRun && info.Header.Summary.SlotName == "Alpha" && info.Header.Summary.Coins == 42,
            "round trip: header summary filled by sections");
        Check(fresh.GetSlotInfo(1).State == SlotState.Empty, "missing file reads as Empty");
    }

    private static void BackupRecovery()
    {
        SaveService service = NewService(out TestRunSection run, out _);
        service.SetActiveSlot(1);
        run.Value = 1;
        service.WriteActiveSlot();
        run.Value = 2;
        service.WriteActiveSlot();
        Check(File.Exists(SlotFile(1) + ".bak") && !File.Exists(SlotFile(1) + ".tmp"), "atomic write leaves .bak and no .tmp");

        File.Delete(SlotFile(1));
        SaveService fresh = NewService(out TestRunSection run2, out _);
        fresh.SetActiveSlot(1);
        Check(fresh.LoadActiveSlot() == LoadResult.Loaded && run2.Value == 1, "deleted main file recovers from .bak");
        Check(fresh.GetSlotInfo(1).State == SlotState.ActiveRun, "deleted main file: tile reads from .bak");
    }

    private static void GarbageIsCorrupted()
    {
        File.WriteAllText(SlotFile(2), "this is not json {{{");
        File.WriteAllText(SlotFile(2) + ".bak", "neither is this");

        SaveService service = NewService(out TestRunSection run, out _);
        service.SetActiveSlot(2);
        run.Value = 7;
        Check(service.GetSlotInfo(2).State == SlotState.Corrupted, "garbage main + backup marks the slot Corrupted");
        Check(service.LoadActiveSlot() == LoadResult.Corrupted && run.Value == 7, "corrupted load leaves runtime state untouched");
        Check(!service.WriteActiveSlot() && File.ReadAllText(SlotFile(2)).StartsWith("this is not"), "corrupted slot is never silently overwritten");

        service.DeleteSlot(2);
        Check(service.GetSlotInfo(2).State == SlotState.Empty, "Delete clears a corrupted slot");
    }

    private static void UnknownSectionPreserved()
    {
        SaveService service = NewService(out _, out TestSlotSection slot);
        service.SetActiveSlot(2);
        slot.Name = "Keep";
        service.WriteActiveSlot();

        EditFile(SlotFile(2), root =>
            root["Sections"]["future.feature"] = JObject.FromObject(new { Version = 3, Scope = "Slot", Payload = new { x = 1 } }));

        SaveService fresh = NewService(out _, out _);
        fresh.SetActiveSlot(2);
        fresh.LoadActiveSlot();
        fresh.WriteActiveSlot();

        JToken kept = JObject.Parse(File.ReadAllText(SlotFile(2)))["Sections"]["future.feature"];
        Check(kept != null && kept["Payload"].Value<int>("x") == 1 && kept.Value<int>("Version") == 3,
            "unknown section survives load + write");
    }

    private static void MissingSectionGetsDefault()
    {
        EditFile(SlotFile(0), root => ((JObject)root["Sections"]).Remove("test.run"));

        SaveService service = NewService(out TestRunSection run, out TestSlotSection slot);
        service.SetActiveSlot(0);
        run.Value = 99;
        Check(service.LoadActiveSlot() == LoadResult.Loaded && run.Value == -1 && slot.Name == "Alpha",
            "missing section loads its default, others still restore");
    }

    private static void NewerSectionIsCorrupted()
    {
        DeleteAllFiles(0);
        SaveService service = NewService(out _, out _);
        service.SetActiveSlot(0);
        service.WriteActiveSlot(); // single write — no .bak to fall back to

        EditFile(SlotFile(0), root => root["Sections"]["test.run"]["Version"] = 99);
        Check(NewService(out _, out _).GetSlotInfo(0).State == SlotState.Corrupted, "section newer than code marks the slot Corrupted");
    }

    private static void MigrationRuns()
    {
        DeleteAllFiles(0);
        SaveService v1 = NewService(out TestRunSection run, out _);
        v1.SetActiveSlot(0);
        run.Value = 5;
        v1.WriteActiveSlot();

        SaveService v2 = NewService(out TestRunSection run2, out _, runVersion: 2);
        v2.SetActiveSlot(0);
        Check(v2.LoadActiveSlot() == LoadResult.Loaded && run2.Value == 105, "older section version runs its migrate step");
    }

    private static void RunWipe()
    {
        DeleteAllFiles(0);
        SaveService service = NewService(out TestRunSection run, out TestSlotSection slot);
        service.SetActiveSlot(0);
        slot.Name = "Survivor";
        run.Value = 50;
        service.WriteActiveSlot();

        EditFile(SlotFile(0), root =>
            root["Sections"]["run.future"] = JObject.FromObject(new { Version = 1, Scope = "Run", Payload = new { y = 2 } }));

        SaveService fresh = NewService(out _, out _);
        fresh.SetActiveSlot(0);
        fresh.LoadActiveSlot();
        fresh.WriteActiveSlotWipingRun();

        SlotInfo info = fresh.GetSlotInfo(0);
        JObject sections = (JObject)JObject.Parse(File.ReadAllText(SlotFile(0)))["Sections"];
        Check(info.State == SlotState.NoRun && info.Header.Summary.SlotName == "Survivor" && info.Header.Summary.Coins == 0,
            "run wipe: slot keeps its name, shows no active run");
        Check(sections["test.run"] == null && sections["run.future"] == null && sections["test.slot"] != null,
            "run wipe: every Run-scope section dropped, including unknown ones");
    }

    private static void DebugSlotNeverTouchesDisk()
    {
        string[] before = Directory.GetFiles(_folder);
        SaveService service = NewService(out TestRunSection run, out _);
        run.Value = 11;
        Check(service.WriteActiveSlot(), "debug slot: write succeeds with no active slot");
        run.Value = 0;
        Check(service.LoadActiveSlot() == LoadResult.Loaded && run.Value == 11, "debug slot: round trips in memory");
        Check(Directory.GetFiles(_folder).Length == before.Length, "debug slot: nothing written to disk");
    }

    // ─────────────────────────────────────────────────────────────────
    //  4B — real sections
    // ─────────────────────────────────────────────────────────────────

    private class Session
    {
        public RunStats Run = new RunStats();
        public SlotData Slot = new SlotData();
        public PersistentStats Persistent = new PersistentStats();
        public SaveService Saves;

        public Session(ContentCatalog catalog, int slot)
        {
            Saves = new SaveService(_folder);
            SaveBootstrap.RegisterSections(Saves, () => Run, () => Slot, () => Persistent, catalog);
            Saves.SetActiveSlot(slot);
        }
    }

    private static bool TryCatalog(out ContentCatalog catalog)
    {
        catalog = ContentCatalog.Load();
        bool ok = catalog.Cigs.Count >= 3 && catalog.Weapons.Count >= 2;
        Check(ok, "ContentCatalog has at least 3 upgrades and 2 weapons to test with");
        return ok;
    }

    private static void Populate(Session s, ContentCatalog catalog)
    {
        RunStats run = s.Run;
        run.CurrentCoins = 37;
        run.MaxCoins = 120;
        run.CoinsCollectedThisRun = 210;
        run.KillsThisRun = 44;
        run.CurrentDungeonLevel = 4;
        run.DungeonsClearedThisRun = 3;
        run.DungeonSizeIncrement = 2;
        run.ResumePoint = RunResumePoint.Store;
        run.RelicsHeld[1] = true;
        run.RelicsHeld[5] = true;
        run.UniqueRelicsCollectedThisRun = 2;
        run.HeldCigs.Add(new CigInstance { Data = catalog.Cigs[0], RolledTier = 3, RolledRarity = Rarity.Rare, IsBurning = true });
        run.HeldCigs.Add(new CigInstance { Data = catalog.Cigs[1], RolledTier = 1, RolledRarity = Rarity.Common });
        run.PurchasedCigIds.AddRange(new[] { catalog.Cigs[0].id, catalog.Cigs[1].id, catalog.Cigs[2].id });
        run.OwnedWeapons.AddRange(new[] { catalog.Weapons[0], catalog.Weapons[1] });
        run.EquippedWeapon = catalog.Weapons[1];

        s.Slot.Name = "Tester";

        s.Persistent.TotalCoinsEverCollected = 9001;
        s.Persistent.TotalEnemiesKilled = 321;
        s.Persistent.TotalRuns = 12;
        s.Persistent.BestRunDungeonsCleared = 5;
        s.Persistent.DemoCompleted = true;
        s.Persistent.UnlockedAchievementIds.AddRange(new[] { "kills_100", "coins_1000" });
    }

    // Everything the save covers, as one comparable string.
    private static string Describe(RunStats run)
    {
        string cigs = string.Join(",", run.HeldCigs.ConvertAll(c => $"{(c.Data != null ? c.Data.id : "null")}:{c.RolledTier}:{c.RolledRarity}:{c.IsBurning}"));
        string relics = string.Join("", Array.ConvertAll(run.RelicsHeld, held => held ? "1" : "0"));
        string owned = string.Join(",", run.OwnedWeapons.ConvertAll(w => w != null ? w.id : "null"));
        return $"coins {run.CurrentCoins}/{run.MaxCoins} collected {run.CoinsCollectedThisRun} kills {run.KillsThisRun} | " +
               $"dungeon {run.CurrentDungeonLevel} cleared {run.DungeonsClearedThisRun} size+{run.DungeonSizeIncrement} resume {run.ResumePoint} | " +
               $"relics {relics} ({run.UniqueRelicsCollectedThisRun}) | cigs [{cigs}] bought [{string.Join(",", run.PurchasedCigIds)}] | " +
               $"weapon {(run.EquippedWeapon != null ? run.EquippedWeapon.id : "null")} owned [{owned}]";
    }

    private static string Describe(PersistentStats p) =>
        $"coins {p.TotalCoinsEverCollected} kills {p.TotalEnemiesKilled} runs {p.TotalRuns} best {p.BestRunDungeonsCleared} " +
        $"demo {p.DemoCompleted} achievements [{string.Join(",", p.UnlockedAchievementIds)}]";

    private static void CheckSame(string expected, string actual, string what)
    {
        Check(expected == actual, what);
        if (expected != actual) Debug.LogError($"[SaveSelfTest]   expected: {expected}\n   actual:   {actual}");
    }

    private static void RealSectionsRoundTrip()
    {
        if (!TryCatalog(out ContentCatalog catalog)) return;
        DeleteAllFiles(0);

        Session before = new Session(catalog, 0);
        Populate(before, catalog);
        Check(before.Saves.WriteActiveSlot() && before.Saves.WriteProfile(), "4B: slot + profile write");

        Session after = new Session(catalog, 0);
        Check(after.Saves.LoadActiveSlot() == LoadResult.Loaded && after.Saves.LoadProfile() == LoadResult.Loaded, "4B: slot + profile load");
        CheckSame(Describe(before.Run), Describe(after.Run), "4B: capture → restore gives identical run state");
        CheckSame(Describe(before.Persistent), Describe(after.Persistent), "4B: capture → restore gives identical profile");
        Check(after.Slot.Name == "Tester", "4B: slot name restored");

        SlotSummary summary = after.Saves.GetSlotInfo(0).Header.Summary;
        Check(summary.SlotName == "Tester" && summary.Coins == 37 && summary.DungeonNumber == 4 && summary.HasActiveRun,
            "4B: header summary has name, coins, dungeon, active run");
    }

    private static void UnknownContentIdSkipped()
    {
        if (!TryCatalog(out ContentCatalog catalog)) return;

        EditFile(SlotFile(0), root =>
        {
            JToken sections = root["Sections"];
            sections["run.upgrades"]["Payload"]["Held"][0]["Id"] = "Removed-99";
            ((JArray)sections["run.upgrades"]["Payload"]["Purchased"]).Add("Removed-99");
            sections["run.weapons"]["Payload"]["Equipped"] = "laser";
            ((JArray)sections["run.relics"]["Payload"]["Held"]).Add("relic_42");
        });

        Debug.Log("[SaveSelfTest] (the next 4 'unknown ... skipped' warnings are expected)");
        Session s = new Session(catalog, 0);
        Check(s.Saves.LoadActiveSlot() == LoadResult.Loaded, "4B: unknown content IDs don't fail the load");
        Check(s.Run.HeldCigs.Count == 1 && s.Run.HeldCigs[0].Data == catalog.Cigs[1], "4B: unknown upgrade skipped, known one kept");
        Check(s.Run.PurchasedCigIds.Count == 3, "4B: unknown purchased ID skipped");
        Check(s.Run.EquippedWeapon == null && s.Run.OwnedWeapons.Count == 2, "4B: unknown weapon skipped, owned kept");
        Check(s.Run.UniqueRelicsCollectedThisRun == 2, "4B: unknown relic skipped");
    }

    private static void RemovedRealSectionLoadsDefault()
    {
        if (!TryCatalog(out ContentCatalog catalog)) return;

        EditFile(SlotFile(0), root => ((JObject)root["Sections"]).Remove("run.economy"));

        Session s = new Session(catalog, 0);
        s.Run.CurrentCoins = 555;
        Check(s.Saves.LoadActiveSlot() == LoadResult.Loaded, "4B: load with run.economy removed");
        Check(s.Run.CurrentCoins == 0 && s.Run.MaxCoins == 0 && s.Run.CurrentDungeonLevel == 4,
            "4B: removed section loads its defaults, the rest still restore");
    }

    // ─────────────────────────────────────────────────────────────────
    //  4C — Continue
    // ─────────────────────────────────────────────────────────────────

    private static void ContinueTargeting()
    {
        for (int i = 0; i < SaveService.SlotCount; i++) DeleteAllFiles(i);

        SaveService service = NewService(out _, out TestSlotSection slot);
        Check(service.FindContinueSlot() == SaveService.NoSlot, "4C: Continue hidden on a fresh install");

        // Slot 0 then slot 2 get active runs; slot 2 is written last.
        slot.Name = "Older";
        service.SetActiveSlot(0);
        service.WriteActiveSlot();
        System.Threading.Thread.Sleep(30);
        slot.Name = "Newer";
        service.SetActiveSlot(2);
        service.WriteActiveSlot();
        Check(service.FindContinueSlot() == 2, "4C: with two active slots, Continue targets the most recently written");

        // Slot 2's run dies — Continue falls back to slot 0.
        System.Threading.Thread.Sleep(30);
        service.WriteActiveSlotWipingRun();
        Check(service.FindContinueSlot() == 0, "4C: Continue skips a slot whose run ended, even if it's newest");

        // A corrupted slot never qualifies.
        File.WriteAllText(SlotFile(1), "garbage");
        Check(service.FindContinueSlot() == 0, "4C: Continue ignores corrupted slots");

        // Every run ended — Continue hides.
        service.SetActiveSlot(0);
        service.WriteActiveSlotWipingRun();
        Check(service.FindContinueSlot() == SaveService.NoSlot, "4C: Continue hidden after every slot's run has ended");
    }

    private static void DeleteAllFiles(int index)
    {
        foreach (string suffix in new[] { "", ".bak", ".tmp" })
        {
            string path = SlotFile(index) + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
