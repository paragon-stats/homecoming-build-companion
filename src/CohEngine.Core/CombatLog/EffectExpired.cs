namespace CohEngine.Core.CombatLog;

/// <summary>A status on the log owner ending: "You are no longer &lt;status&gt;." The game logs no other expiry.</summary>
/// <param name="Line">The line.</param>
/// <param name="Status">The status word ("held", "afraid").</param>
public sealed record EffectExpired(CombatLogLine Line, string Status) : RecognisedEvent(Line);
