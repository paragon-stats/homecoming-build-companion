namespace CohEngine.Core.CombatLog;

/// <summary>Experience, with the debt it paid off and the influence (or infamy) that came with it.</summary>
/// <param name="Line">The line.</param>
/// <param name="Experience">The experience gained.</param>
/// <param name="DebtWorkedOff">The debt paid off from that experience; 0 when the line names none.</param>
/// <param name="Currency">The influence or infamy gained; 0 when the line names none.</param>
/// <param name="CurrencyName">The currency word as printed ("influence", "infamy"), or null when none was gained.</param>
public sealed record ExperienceGained(
    CombatLogLine Line,
    long Experience,
    long DebtWorkedOff,
    long Currency,
    string? CurrencyName) : RecognisedEvent(Line);
