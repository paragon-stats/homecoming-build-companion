namespace CohEngine.Core.CombatLog;

/// <summary>A status the log owner's power put on a target: "You &lt;verb&gt; &lt;target&gt; with your &lt;power&gt;."</summary>
/// <param name="Line">The line.</param>
/// <param name="Status">The verb as printed (Taunt, Immobilize, Stun, Hold, Sleep, Confuse, Terrorize, Placate).</param>
/// <param name="Target">The target.</param>
/// <param name="Power">The power's display name.</param>
public sealed record DebuffApplied(CombatLogLine Line, string Status, string Target, string Power) : RecognisedEvent(Line);
