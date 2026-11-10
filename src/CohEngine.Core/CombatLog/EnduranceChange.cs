namespace CohEngine.Core.CombatLog;

/// <summary>Endurance gained, spent or drained.</summary>
/// <param name="Line">The line.</param>
/// <param name="Kind">Which way the endurance moved.</param>
/// <param name="Counterpart">The giver (Gained) or the target (DrainedFromTarget, GrantedToTarget); null when the log owner alone is involved.</param>
/// <param name="Power">The power's display name.</param>
/// <param name="Amount">The endurance moved.</param>
/// <param name="IsOverTime">The line says "over time".</param>
public sealed record EnduranceChange(
    CombatLogLine Line,
    EnduranceKind Kind,
    string? Counterpart,
    string Power,
    decimal Amount,
    bool IsOverTime) : RecognisedEvent(Line);
