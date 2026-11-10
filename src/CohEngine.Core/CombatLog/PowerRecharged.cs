namespace CohEngine.Core.CombatLog;

/// <summary>"&lt;power&gt; is recharged."</summary>
/// <param name="Line">The line.</param>
/// <param name="Power">The power's display name.</param>
public sealed record PowerRecharged(CombatLogLine Line, string Power) : RecognisedEvent(Line);
