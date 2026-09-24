/// <summary>
/// Lifecycle for a cig's effect, per UpgradeSystemSpec.md: Apply() on buy,
/// ApplyMaxed() on burn (tier forced to 4, rarity unchanged), Remove() when
/// the level-boundary signal fires after a burn. Implemented directly by
/// each concrete CigData subclass (CritChanceCigData, StompPowerCigData,
/// etc.) — the asset you create in the Project window *is* the effect, per
/// the current data model. All three are hooks for effects that need to
/// react to those moments; every effect in this game is a pure stat
/// contributor, so they're no-ops — the actual numbers are computed in
/// Contribute, which Pack calls on every held instance whenever it
/// recomputes PackStats. Contribute is not in the spec's interface
/// description but is kept as the pull-based mechanism: PackStats is rebuilt
/// from scratch from RunStats.HeldCigs every time, which is what makes state
/// survive the RoguelikeMode -> BossArena scene boundary and makes Remove()
/// trivial (drop the instance, recompute) — see UpgradeSystemPlan.md's
/// "pull-based stat resolver" rationale. Crucially, since a CigData is now a
/// shared ScriptableObject ASSET (not a per-purchase object), none of these
/// four methods may write to `this` — see CigData.cs's warning about
/// runtime writes to assets persisting across Editor play sessions.
/// </summary>
public interface IUpgradeEffect
{
    void Apply(int tier, Rarity rarity);
    void ApplyMaxed(Rarity rarity);
    void Remove();
    void Contribute(CigInstance instance, ref PackStats stats);
}
