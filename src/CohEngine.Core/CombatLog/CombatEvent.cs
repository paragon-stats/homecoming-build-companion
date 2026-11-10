namespace CohEngine.Core.CombatLog;

/// <summary>What the classifier made of one log line. Every line yields exactly one event, so none is dropped.</summary>
/// <param name="Line">The line this event came from, verbatim and with its position.</param>
public abstract record CombatEvent(CombatLogLine Line)
{
    /// <summary>Gets the three-way outcome.</summary>
    public abstract CombatOutcome Outcome { get; }
}
