namespace CohEngine.Core.CombatLog;

/// <summary>The base of every event in the taxonomy; its outcome is always <see cref="CombatOutcome.Recognised"/>.</summary>
/// <param name="Line">The line this event came from.</param>
public abstract record RecognisedEvent(CombatLogLine Line) : CombatEvent(Line)
{
    /// <inheritdoc/>
    public sealed override CombatOutcome Outcome => CombatOutcome.Recognised;
}
