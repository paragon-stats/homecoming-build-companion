using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>H-FMT-001: <c>PowerSets</c> must have 8 entries, or MidsReborn fails at <c>Character.Reset</c>.</summary>
internal static class MbdPowersetsRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        if (context.Format is { } format && format.PowerSets.Count != MbdFormat.PowerSetsExpected)
        {
            yield return new Diagnostic("H-FMT-001", "error", $"PowerSets has {format.PowerSets.Count} entries; MidsReborn indexes {MbdFormat.PowerSetsExpected}")
            {
                Fix = "pad PowerSets with \"\" entries or drop the extras until it has 8",
                Expected = MbdFormat.PowerSetsExpected,
                Actual = format.PowerSets.Count,
            };
        }
    }
}
