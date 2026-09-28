using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>Where a section lives and when it is wiped (save-system-spec.md §2).</summary>
public enum SaveScope
{
    Global, // profile.json — shared by every slot
    Slot,   // slot file — survives death
    Run     // slot file — wiped when the run ends
}

/// <summary>
/// On-disk shape of every save file (slots and profile alike): a header the
/// slot-select UI can read without restoring anything, plus the sections map.
/// This is the file contract — change it only together with FormatVersion.
/// </summary>
[Serializable]
public class SaveEnvelope
{
    public SaveHeader Header = new SaveHeader();
    public Dictionary<string, SaveSectionEntry> Sections = new Dictionary<string, SaveSectionEntry>();
}

[Serializable]
public class SaveHeader
{
    public int FormatVersion;
    public int SlotIndex;            // SaveService.ProfileIndex for profile.json
    public DateTime CreatedUtc;
    public DateTime LastWrittenUtc;
    public SlotSummary Summary = new SlotSummary();
}

/// <summary>
/// What a slot tile shows. Filled at write time by the sections being written
/// (SaveSection.FillSummary) — SaveService itself knows nothing about coins or
/// dungeons. Run-scope fields stay at their defaults when no run is written.
/// </summary>
[Serializable]
public class SlotSummary
{
    public string SlotName = "";
    public int Coins;
    public int DungeonNumber;
    public bool HasActiveRun;
}

[Serializable]
public class SaveSectionEntry
{
    public int Version;
    // Stored so Run-scope data can be wiped even for sections this build has
    // no handler for (unknown sections are preserved, not understood).
    public SaveScope Scope;
    public JToken Payload;
}

public enum SlotState
{
    Empty,      // no file
    NoRun,      // named, Run data empty (after death / victory)
    ActiveRun,  // Run data exists
    Corrupted   // main file and backup both failed — Delete is the only option
}

/// <summary>Header-only view of a slot for the slot-select UI.</summary>
public readonly struct SlotInfo
{
    public readonly int Index;
    public readonly SlotState State;
    public readonly SaveHeader Header; // null when Empty or Corrupted

    public SlotInfo(int index, SlotState state, SaveHeader header)
    {
        Index = index;
        State = state;
        Header = header;
    }
}

/// <summary>Result of loading a file into the running game.</summary>
public enum LoadResult
{
    Loaded,     // sections restored (missing ones got defaults)
    Empty,      // nothing on disk — defaults restored
    Corrupted   // nothing restored; runtime state untouched
}
