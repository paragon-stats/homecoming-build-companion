namespace CohEngine.Core.CombatLog;

/// <summary>A hit or miss, with the to-hit chance and roll when the log prints them.</summary>
/// <param name="Line">The line.</param>
/// <param name="Direction">Outgoing when the log owner attacks, Incoming when something attacks the owner.</param>
/// <param name="Counterpart">The target of an outgoing attack, or the attacker of an incoming one.</param>
/// <param name="Power">The power's display name.</param>
/// <param name="Hit">True for a hit, false for a miss.</param>
/// <param name="ChanceToHit">The printed chance, as a percentage; null on an autohit.</param>
/// <param name="Roll">The printed roll (0 to 100); null on an autohit.</param>
/// <param name="Autohit">The line says the power cannot miss; the sample is not a roll.</param>
/// <param name="ForcedByStreakbreaker">The server forced the hit after a miss streak; the sample is not a roll.</param>
/// <param name="IsSelf">The counterpart is the log owner's own character, so the line is self-targeted, not offensive.</param>
/// <param name="Origin">The pet or effect name that prefixes the line ("Burn Flames:  ..."), or null.</param>
public sealed record AttackRoll(
    CombatLogLine Line,
    CombatDirection Direction,
    string Counterpart,
    string Power,
    bool Hit,
    decimal? ChanceToHit,
    decimal? Roll,
    bool Autohit,
    bool ForcedByStreakbreaker,
    bool IsSelf,
    string? Origin) : RecognisedEvent(Line);
