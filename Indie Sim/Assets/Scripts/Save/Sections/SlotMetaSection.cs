using System;

[Serializable]
public class SlotMetaDto
{
    public string Name = "";
}

/// <summary>slot.meta — the slot's name. Slot scope: survives death.</summary>
public class SlotMetaSection : SaveSection<SlotMetaDto>
{
    private readonly Func<SlotData> _slot;

    public SlotMetaSection(Func<SlotData> slot) => _slot = slot;

    public override string Key => "slot.meta";
    public override SaveScope Scope => SaveScope.Slot;

    protected override SlotMetaDto Capture() => new SlotMetaDto { Name = _slot().Name };
    protected override void Restore(SlotMetaDto dto) => _slot().Name = dto.Name ?? "";
    protected override SlotMetaDto CreateDefault() => new SlotMetaDto();
    protected override void FillSummary(SlotMetaDto dto, SlotSummary summary) => summary.SlotName = dto.Name;
}
