namespace CohEngine.Core.CombatLog;

/// <summary>A buff the log owner received: "Your &lt;power&gt; has &lt;effect&gt;."</summary>
/// <param name="Line">The line.</param>
/// <param name="Power">The power's display name.</param>
/// <param name="Effect">The effect text, without the final period ("increased your rate of attack").</param>
public sealed record BuffApplied(CombatLogLine Line, string Power, string Effect) : RecognisedEvent(Line);
