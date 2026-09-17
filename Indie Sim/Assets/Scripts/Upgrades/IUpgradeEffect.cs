/// <summary>
/// Lifecycle for a cig's effect. Apply/ApplyMaxed/Remove are hooks for
/// effects that need to react to those moments; most tiered/flat stat
/// effects leave them empty and do everything in Contribute, which Pack
/// calls on every held instance whenever it recomputes PackStats.
/// </summary>
public interface IUpgradeEffect
{
    void Apply();
    void ApplyMaxed();
    void Remove();
    void Contribute(CigInstance instance, ref PackStats stats);
}
