using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// The extension point for saved data (save-system-spec.md §2). SaveService
/// only ever talks to this interface — derive from SaveSection&lt;TDto&gt;
/// instead of implementing it directly.
///
/// Loading is two-phase so a slot can never be half-restored: Read() every
/// section first (migrate + deserialize, may throw), and only if all succeed
/// Restore() each DTO into GameSession.
/// </summary>
public interface ISaveSection
{
    string Key { get; }
    SaveScope Scope { get; }
    int Version { get; }

    object Capture();
    object Read(JToken payload, int payloadVersion, JsonSerializer serializer);
    object CreateDefault();
    void Restore(object dto);
    void FillSummary(object dto, SlotSummary summary);
}

/// <summary>
/// One saved chunk of game data. TDto is the on-disk contract — a plain class
/// separate from the runtime model, so GameSession/RunStats can be refactored
/// freely behind it. Capture reads from GameSession (never scene managers);
/// Restore writes back into GameSession.
///
/// Bumping Version: override Migrate to upgrade older payloads step by step.
/// Without an override, an older payload fails to load (the slot shows as
/// Corrupted) rather than being silently misread.
/// </summary>
public abstract class SaveSection<TDto> : ISaveSection where TDto : class
{
    public abstract string Key { get; }
    public abstract SaveScope Scope { get; }
    public virtual int Version => 1;

    protected abstract TDto Capture();
    protected abstract void Restore(TDto dto);
    protected abstract TDto CreateDefault();

    /// <summary>Upgrades a payload written at fromVersion to the current Version.</summary>
    protected virtual JToken Migrate(JToken payload, int fromVersion)
    {
        throw new InvalidOperationException($"Section '{Key}' has no migration from v{fromVersion} to v{Version}.");
    }

    /// <summary>Contributes to the slot header summary at write time.</summary>
    protected virtual void FillSummary(TDto dto, SlotSummary summary) { }

    object ISaveSection.Capture() => Capture();

    object ISaveSection.Read(JToken payload, int payloadVersion, JsonSerializer serializer)
    {
        if (payloadVersion < Version)
            payload = Migrate(payload, payloadVersion);

        TDto dto = payload?.ToObject<TDto>(serializer);
        if (dto == null)
            throw new JsonException($"Section '{Key}' payload is null.");
        return dto;
    }

    object ISaveSection.CreateDefault() => CreateDefault();
    void ISaveSection.Restore(object dto) => Restore((TDto)dto);
    void ISaveSection.FillSummary(object dto, SlotSummary summary) => FillSummary((TDto)dto, summary);
}
