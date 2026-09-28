/// <summary>
/// Slot-scoped data: belongs to the active save slot and survives death
/// (save-system-spec.md, Slot scope). Owned by GameSession.Slot, saved by the
/// slot.meta section. Future per-slot meta-progression goes here.
/// </summary>
[System.Serializable]
public class SlotData
{
    public string Name = "";
}
