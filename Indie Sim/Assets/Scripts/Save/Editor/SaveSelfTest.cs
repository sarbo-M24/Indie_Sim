using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4A acceptance checks for SaveService, runnable from Tools/Save/Run Self-Test
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

    private static void DeleteAllFiles(int index)
    {
        foreach (string suffix in new[] { "", ".bak", ".tmp" })
        {
            string path = SlotFile(index) + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
