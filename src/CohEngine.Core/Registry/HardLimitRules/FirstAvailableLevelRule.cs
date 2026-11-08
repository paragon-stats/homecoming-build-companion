using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>
/// H-POWER-002: a power is picked at or after its earliest level. The power's database minimum level encodes its own
/// first availability, so one rule covers tier unlocks and the Ancillary/Patron level-35 gate.
/// </summary>
internal static class FirstAvailableLevelRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        foreach (Power power in context.Picks.Where(pick => pick.Level > 0 && pick.PickLevel + 1 < pick.Level))
        {
            string message = $"{PyText.Repr(power.FullName)} is picked at level {power.PickLevel + 1} but is not available until "
                + $"level {power.Level}";
            yield return new Diagnostic("H-POWER-002", "error", message)
            {
                Fix = "pick this power at or after its earliest available level",
                Expected = power.Level,
                Actual = power.PickLevel + 1,
            };
        }
    }
}
