namespace CohEngine.Core.CombatLog;

/// <summary>Which way an attack or damage line points, from the log owner's side.</summary>
public enum CombatDirection
{
    /// <summary>The log owner (or a pet, or a teammate's effect on the owner's screen) acts on someone else.</summary>
    Outgoing,

    /// <summary>Someone acts on the log owner.</summary>
    Incoming,
}
