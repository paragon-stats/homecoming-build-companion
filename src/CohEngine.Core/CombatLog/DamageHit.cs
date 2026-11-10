namespace CohEngine.Core.CombatLog;

/// <summary>Damage dealt or taken, at the two decimal places the log prints.</summary>
/// <param name="Line">The line.</param>
/// <param name="Direction">Outgoing for damage the owner deals, Incoming for damage the owner takes.</param>
/// <param name="Counterpart">The target of outgoing damage, or the source of incoming damage.</param>
/// <param name="Power">The power's display name; a proc reads "&lt;Full Set Name&gt;: &lt;Piece Name&gt;".</param>
/// <param name="Amount">The damage.</param>
/// <param name="DamageType">The damage type word as printed ("Fire", "Negative Energy").</param>
/// <param name="IsDot">The line says "over time".</param>
/// <param name="IsProc">The power name carries the "&lt;set&gt;: &lt;piece&gt;" form of an enhancement proc.</param>
/// <param name="Modifier">The parenthesised word after the amount ("CRITICAL", "JUDGEMENT CRITICAL", "SCOURGE"), or null.</param>
/// <param name="Origin">The pet or effect name that prefixes the line, or null.</param>
public sealed record DamageHit(
    CombatLogLine Line,
    CombatDirection Direction,
    string Counterpart,
    string Power,
    decimal Amount,
    string DamageType,
    bool IsDot,
    bool IsProc,
    string? Modifier,
    string? Origin) : RecognisedEvent(Line);
