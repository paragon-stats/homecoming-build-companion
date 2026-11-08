using CohEngine.Core.Data;

namespace CohEngine.Core.SetBonuses;

/// <summary>The set-bonus exemplar gate: no -3 set-minimum retention, only the power's pick level against ForceLevel.</summary>
internal static class ExemplarGate
{
    /// <summary>
    /// Whether a power's set bonuses count. The gate reads the build's pick level, not the database minimum: the two
    /// coincide at ForceLevel 50, and under exemplar a power picked above the level drops its bonuses.
    /// </summary>
    /// <param name="power">The build power.</param>
    /// <param name="forceLevel">The exemplar level.</param>
    /// <returns>True when the power's set bonuses count.</returns>
    public static bool Passes(Power power, int forceLevel)
    {
        return power.PickLevel <= forceLevel;
    }
}
