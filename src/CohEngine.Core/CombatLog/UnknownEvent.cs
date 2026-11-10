namespace CohEngine.Core.CombatLog;

/// <summary>A line no rule claimed. It keeps its text and position so a coverage report can list it.</summary>
/// <param name="Line">The line, verbatim.</param>
public sealed record UnknownEvent(CombatLogLine Line) : CombatEvent(Line)
{
    /// <inheritdoc/>
    public override CombatOutcome Outcome => CombatOutcome.Unknown;
}
