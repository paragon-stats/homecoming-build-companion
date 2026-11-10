namespace CohEngine.Core.CombatLog;

/// <summary>A line the ignore list claimed: known, and deliberately not used.</summary>
/// <param name="Line">The line.</param>
/// <param name="Reason">Why the line is skipped.</param>
public sealed record IgnoredEvent(CombatLogLine Line, string Reason) : CombatEvent(Line)
{
    /// <inheritdoc/>
    public override CombatOutcome Outcome => CombatOutcome.Ignored;
}
