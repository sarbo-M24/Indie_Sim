using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Slot-based save files (save-system-spec.md §2). A plain class, built by
/// SaveBootstrap and owned by GameSession. Handles file I/O, the envelope,
/// format/section versioning, atomic writes, backup recovery and the section
/// registry — and knows nothing about coins, upgrades or any other game data.
/// All of that lives in registered SaveSections.
///
/// Files (in Folder): slot_0..2.json, profile.json, each with .tmp/.bak siblings.
/// Nothing here saves on its own — callers write at the explicit save points.
/// </summary>
public sealed class SaveService
{
    public const int FormatVersion = 1;
    public const int SlotCount = 3;
    public const int NoSlot = -1;

    // File indices besides 0..SlotCount-1.
    public const int ProfileIndex = -1;
    private const int DebugSlotIndex = -2;

    public static string DefaultFolder => Path.Combine(Application.persistentDataPath, "saves");

    public string Folder { get; }
    public string ProfilePath => Path.Combine(Folder, "profile.json");
    public string GetSlotPath(int index) => Path.Combine(Folder, $"slot_{index}.json");

    /// <summary>The slot every slot read/write goes to. NoSlot → the transient debug slot.</summary>
    public int ActiveSlot { get; private set; } = NoSlot;
    public bool HasActiveSlot => ActiveSlot != NoSlot;

    private readonly List<ISaveSection> _sections = new List<ISaveSection>();
    private readonly Dictionary<string, ISaveSection> _sectionsByKey = new Dictionary<string, ISaveSection>();

    // Last envelope read or written per file, so unknown sections and the
    // created timestamp carry over into the next write.
    private readonly Dictionary<int, SaveEnvelope> _known = new Dictionary<int, SaveEnvelope>();

    // Level scene played directly in the editor has no chosen slot: slot
    // writes land here instead of on disk.
    private string _debugSlotJson;
    private bool _warnedDebugSlot;

    private readonly JsonSerializerSettings _settings;
    private readonly JsonSerializer _serializer;

    public SaveService(string folder)
    {
        Folder = folder;
        _settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            // DTO fields with initialisers (e.g. new List<>()) must be replaced
            // by the file's value, not appended to.
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = { new StringEnumConverter() }
        };
        _serializer = JsonSerializer.Create(_settings);
    }

    // ─────────────────────────────────────────────────────────────────
    //  REGISTRY
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Registers a section. Call once per section, from SaveBootstrap.</summary>
    public void Register(ISaveSection section)
    {
        if (section == null) throw new ArgumentNullException(nameof(section));
        if (string.IsNullOrEmpty(section.Key)) throw new ArgumentException("Save section key is empty.");
        if (_sectionsByKey.ContainsKey(section.Key)) throw new ArgumentException($"Save section '{section.Key}' is already registered.");
        if (section.Version < 1) throw new ArgumentException($"Save section '{section.Key}' has version {section.Version}; versions start at 1.");

        _sections.Add(section);
        _sectionsByKey.Add(section.Key, section);
    }

    // ─────────────────────────────────────────────────────────────────
    //  ACTIVE SLOT
    // ─────────────────────────────────────────────────────────────────

    public void SetActiveSlot(int index)
    {
        ValidateSlotIndex(index);
        ActiveSlot = index;
    }

    public void ClearActiveSlot() => ActiveSlot = NoSlot;

    // ─────────────────────────────────────────────────────────────────
    //  PROFILE (Global scope)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Restores Global sections from profile.json. On Corrupted the runtime
    /// keeps its defaults and profile writes are refused, so the file is
    /// never silently overwritten.
    /// </summary>
    public LoadResult LoadProfile()
    {
        LoadResult result = Load(ProfileIndex);
        if (result == LoadResult.Corrupted)
        {
            foreach (ISaveSection section in SectionsFor(ProfileIndex))
                section.Restore(section.CreateDefault());
            Debug.LogError("[SaveService] profile.json and its backup are unreadable. Running on defaults; profile writes are disabled this session so the file can be inspected.");
        }
        return result;
    }

    public bool WriteProfile() => Write(ProfileIndex, includeRun: true);

    // ─────────────────────────────────────────────────────────────────
    //  SLOTS (Slot + Run scope)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Header-only read for the slot-select UI. Restores nothing.</summary>
    public SlotInfo GetSlotInfo(int index)
    {
        ValidateSlotIndex(index);

        if (!TryRead(index, prepare: false, out SaveEnvelope envelope, out _))
            return new SlotInfo(index, SlotState.Corrupted, null);
        if (envelope == null)
            return new SlotInfo(index, SlotState.Empty, null);

        SlotState state = envelope.Header.Summary.HasActiveRun ? SlotState.ActiveRun : SlotState.NoRun;
        return new SlotInfo(index, state, envelope.Header);
    }

    /// <summary>
    /// Main-menu Continue target: the slot with an active run whose header
    /// was written most recently, or NoSlot. Worked out from the headers every
    /// time rather than a stored "last slot" pointer, which could go stale
    /// after a death, delete or corruption. Corrupted and no-run slots never
    /// qualify.
    /// </summary>
    public int FindContinueSlot()
    {
        int best = NoSlot;
        DateTime bestTime = DateTime.MinValue;
        for (int i = 0; i < SlotCount; i++)
        {
            SlotInfo info = GetSlotInfo(i);
            if (info.State != SlotState.ActiveRun || info.Header.LastWrittenUtc <= bestTime) continue;
            best = i;
            bestTime = info.Header.LastWrittenUtc;
        }
        return best;
    }

    /// <summary>Restores Slot and Run sections from the active slot.</summary>
    public LoadResult LoadActiveSlot() => Load(CurrentSlotFileIndex());

    /// <summary>Captures Slot and Run sections into the active slot.</summary>
    public bool WriteActiveSlot() => Write(CurrentSlotFileIndex(), includeRun: true);

    /// <summary>
    /// Run end (death / victory): writes Slot sections only. Every Run-scope
    /// entry — including ones this build has no handler for — is dropped.
    /// </summary>
    public bool WriteActiveSlotWipingRun() => Write(CurrentSlotFileIndex(), includeRun: false);

    public void DeleteSlot(int index)
    {
        ValidateSlotIndex(index);

        string path = GetSlotPath(index);
        TryDelete(path);
        TryDelete(path + ".bak");
        TryDelete(path + ".tmp");
        _known.Remove(index);
    }

    // ─────────────────────────────────────────────────────────────────
    //  LOAD / WRITE CORE
    // ─────────────────────────────────────────────────────────────────

    private LoadResult Load(int fileIndex)
    {
        if (!TryRead(fileIndex, prepare: true, out SaveEnvelope envelope, out Dictionary<ISaveSection, object> dtos))
            return LoadResult.Corrupted;

        // Every section read successfully (or gets its default) — only now
        // touch runtime state, so a bad file can never half-restore.
        foreach (ISaveSection section in SectionsFor(fileIndex))
            section.Restore(dtos.TryGetValue(section, out object dto) ? dto : section.CreateDefault());

        if (envelope != null) _known[fileIndex] = envelope;
        else _known.Remove(fileIndex);

        return envelope != null ? LoadResult.Loaded : LoadResult.Empty;
    }

    private bool Write(int fileIndex, bool includeRun)
    {
        SaveEnvelope previous;
        if (!_known.TryGetValue(fileIndex, out previous))
        {
            if (!TryRead(fileIndex, prepare: false, out previous, out _))
            {
                Debug.LogError($"[SaveService] Refusing to write {FileLabel(fileIndex)}: the existing file is corrupted. Delete it first.");
                return false;
            }
        }

        DateTime now = DateTime.UtcNow;
        SaveEnvelope envelope = new SaveEnvelope();
        envelope.Header.FormatVersion = FormatVersion;
        envelope.Header.SlotIndex = fileIndex == DebugSlotIndex ? NoSlot : fileIndex;
        envelope.Header.CreatedUtc = previous != null ? previous.Header.CreatedUtc : now;
        envelope.Header.LastWrittenUtc = now;

        // Sections with no handler in this build are preserved untouched.
        if (previous != null)
        {
            foreach (KeyValuePair<string, SaveSectionEntry> pair in previous.Sections)
            {
                if (_sectionsByKey.ContainsKey(pair.Key)) continue;
                if (!includeRun && pair.Value.Scope == SaveScope.Run) continue;
                envelope.Sections[pair.Key] = pair.Value;
            }
        }

        foreach (ISaveSection section in SectionsFor(fileIndex))
        {
            if (!includeRun && section.Scope == SaveScope.Run) continue;

            object dto = section.Capture();
            envelope.Sections[section.Key] = new SaveSectionEntry
            {
                Version = section.Version,
                Scope = section.Scope,
                Payload = JToken.FromObject(dto, _serializer)
            };
            section.FillSummary(dto, envelope.Header.Summary);
        }

        string json = JsonConvert.SerializeObject(envelope, _settings);

        if (fileIndex == DebugSlotIndex)
        {
            _debugSlotJson = json;
            _known[fileIndex] = envelope;
            return true;
        }

        try
        {
            Directory.CreateDirectory(Folder);
            WriteAtomic(PathFor(fileIndex), json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Writing {FileLabel(fileIndex)} failed: {e.Message}");
            return false;
        }

        _known[fileIndex] = envelope;
        return true;
    }

    // Serialize to .tmp, move the current file to .bak, rename .tmp into
    // place. A crash between the moves leaves .bak, which Load falls back to.
    private static void WriteAtomic(string path, string json)
    {
        string tmp = path + ".tmp";
        string bak = path + ".bak";

        File.WriteAllText(tmp, json);
        if (File.Exists(path))
        {
            if (File.Exists(bak)) File.Delete(bak);
            File.Move(path, bak);
        }
        File.Move(tmp, path);
    }

    /// <summary>
    /// Tries the main file, then .bak. Returns false only when a file exists
    /// but neither loads (Corrupted). envelope is null when nothing exists.
    /// With prepare, every section's DTO is read too, so a payload that fails
    /// to deserialize or migrate also falls through to the backup.
    /// </summary>
    private bool TryRead(int fileIndex, bool prepare, out SaveEnvelope envelope, out Dictionary<ISaveSection, object> dtos)
    {
        envelope = null;
        dtos = new Dictionary<ISaveSection, object>();

        if (fileIndex == DebugSlotIndex)
        {
            if (_debugSlotJson == null) return true;
            envelope = Parse(_debugSlotJson);
            if (prepare) dtos = Prepare(envelope, fileIndex);
            return true;
        }

        string main = PathFor(fileIndex);
        bool anyFileExisted = false;

        foreach (string path in new[] { main, main + ".bak" })
        {
            if (!File.Exists(path)) continue;
            anyFileExisted = true;

            try
            {
                SaveEnvelope candidate = Parse(File.ReadAllText(path));
                Dictionary<ISaveSection, object> candidateDtos = prepare ? Prepare(candidate, fileIndex) : dtos;

                if (path != main)
                    Debug.LogWarning($"[SaveService] Recovered {FileLabel(fileIndex)} from its backup.");

                envelope = candidate;
                dtos = candidateDtos;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] {Path.GetFileName(path)} failed to load: {e.Message}");
            }
        }

        return !anyFileExisted;
    }

    private SaveEnvelope Parse(string json)
    {
        SaveEnvelope envelope = JsonConvert.DeserializeObject<SaveEnvelope>(json, _settings);
        if (envelope?.Header == null || envelope.Sections == null)
            throw new JsonException("Missing header or sections.");
        if (envelope.Header.FormatVersion < 1 || envelope.Header.FormatVersion > FormatVersion)
            throw new JsonException($"Save format v{envelope.Header.FormatVersion} is not supported (this build reads up to v{FormatVersion}).");
        if (envelope.Header.Summary == null)
            envelope.Header.Summary = new SlotSummary();

        foreach (KeyValuePair<string, SaveSectionEntry> pair in envelope.Sections)
        {
            if (pair.Value == null)
                throw new JsonException($"Section '{pair.Key}' is null.");
            if (_sectionsByKey.TryGetValue(pair.Key, out ISaveSection section) && pair.Value.Version > section.Version)
                throw new JsonException($"Section '{pair.Key}' is v{pair.Value.Version}, newer than this build's v{section.Version}.");
        }

        return envelope;
    }

    private Dictionary<ISaveSection, object> Prepare(SaveEnvelope envelope, int fileIndex)
    {
        Dictionary<ISaveSection, object> dtos = new Dictionary<ISaveSection, object>();
        foreach (ISaveSection section in SectionsFor(fileIndex))
        {
            if (envelope.Sections.TryGetValue(section.Key, out SaveSectionEntry entry))
                dtos[section] = section.Read(entry.Payload, entry.Version, _serializer);
            // Missing → default, applied at restore time.
        }
        return dtos;
    }

    // ─────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────

    private IEnumerable<ISaveSection> SectionsFor(int fileIndex)
    {
        bool profile = fileIndex == ProfileIndex;
        foreach (ISaveSection section in _sections)
        {
            if (profile == (section.Scope == SaveScope.Global))
                yield return section;
        }
    }

    private int CurrentSlotFileIndex()
    {
        if (HasActiveSlot) return ActiveSlot;

        if (!_warnedDebugSlot)
        {
            _warnedDebugSlot = true;
#if UNITY_EDITOR
            Debug.Log("[SaveService] No active slot (scene played directly?) — using the transient debug slot; nothing is written to disk.");
#else
            Debug.LogWarning("[SaveService] No active slot — slot saves go to the transient debug slot and are lost on quit.");
#endif
        }
        return DebugSlotIndex;
    }

    private string PathFor(int fileIndex) => fileIndex == ProfileIndex ? ProfilePath : GetSlotPath(fileIndex);

    private static string FileLabel(int fileIndex) =>
        fileIndex == ProfileIndex ? "profile.json" :
        fileIndex == DebugSlotIndex ? "the debug slot" :
        $"slot_{fileIndex}.json";

    private static void ValidateSlotIndex(int index)
    {
        if (index < 0 || index >= SlotCount)
            throw new ArgumentOutOfRangeException(nameof(index), $"Slot index {index} is outside 0..{SlotCount - 1}.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Couldn't delete {Path.GetFileName(path)}: {e.Message}");
        }
    }
}
