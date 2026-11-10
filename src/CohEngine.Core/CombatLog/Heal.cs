namespace CohEngine.Core.CombatLog;

/// <summary>Hit points restored.</summary>
/// <param name="Line">The line.</param>
/// <param name="Healer">Who healed, or null when the log says "you" or "your".</param>
/// <param name="Recipient">Who was healed, or null when the log says "you".</param>
/// <param name="Power">The power's display name.</param>
/// <param name="Amount">The hit points restored.</param>
/// <param name="IsOverTime">The line says "over time".</param>
public sealed record Heal(
    CombatLogLine Line,
    string? Healer,
    string? Recipient,
    string Power,
    decimal Amount,
    bool IsOverTime) : RecognisedEvent(Line);
