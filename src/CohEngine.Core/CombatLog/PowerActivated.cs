namespace CohEngine.Core.CombatLog;

/// <summary>"You activated the &lt;power&gt; power."</summary>
/// <param name="Line">The line.</param>
/// <param name="Power">The power's display name.</param>
public sealed record PowerActivated(CombatLogLine Line, string Power) : RecognisedEvent(Line);
