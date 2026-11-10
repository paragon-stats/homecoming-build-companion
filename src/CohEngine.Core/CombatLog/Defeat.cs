namespace CohEngine.Core.CombatLog;

/// <summary>A foe defeated.</summary>
/// <param name="Line">The line.</param>
/// <param name="Defeater">Who landed the defeat, or null for the log owner ("You have defeated ...").</param>
/// <param name="Victim">The defeated foe.</param>
public sealed record Defeat(CombatLogLine Line, string? Defeater, string Victim) : RecognisedEvent(Line);
