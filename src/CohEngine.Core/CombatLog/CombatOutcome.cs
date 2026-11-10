namespace CohEngine.Core.CombatLog;

/// <summary>What the classifier decided about one line. Every line gets exactly one.</summary>
public enum CombatOutcome
{
    /// <summary>The line is a taxonomy event the engine can use.</summary>
    Recognised,

    /// <summary>The line is known and deliberately not used; <see cref="IgnoredEvent.Reason"/> says why.</summary>
    Ignored,

    /// <summary>No rule claimed the line. A coverage report lists these.</summary>
    Unknown,
}
