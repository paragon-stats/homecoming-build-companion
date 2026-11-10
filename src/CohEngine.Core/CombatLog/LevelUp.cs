namespace CohEngine.Core.CombatLog;

/// <summary>"Your combat improves to level N!"</summary>
/// <param name="Line">The line.</param>
/// <param name="Level">The new level.</param>
public sealed record LevelUp(CombatLogLine Line, int Level) : RecognisedEvent(Line);
