namespace CohEngine.Core.CombatLog;

/// <summary>The three ways an endurance line moves endurance.</summary>
public enum EnduranceKind
{
    /// <summary>The log owner gains endurance (own power or another player's).</summary>
    Gained,

    /// <summary>The log owner's own power costs the owner endurance.</summary>
    Spent,

    /// <summary>The log owner's power drains endurance from a target.</summary>
    DrainedFromTarget,

    /// <summary>The log owner's power gives endurance to someone else.</summary>
    GrantedToTarget,
}
