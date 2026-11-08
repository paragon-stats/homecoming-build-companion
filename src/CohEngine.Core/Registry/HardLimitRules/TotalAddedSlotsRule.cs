using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>H-SLOT-001: total added slots across the build must not exceed the cap.</summary>
internal static class TotalAddedSlotsRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        int total = context.AddedByPower.Values.Sum();
        if (total > context.MaxSlots)
        {
            yield return new Diagnostic("H-SLOT-001", "error", $"total added slots ({total}) exceeds the cap ({context.MaxSlots})")
            {
                Fix = $"remove {total - context.MaxSlots} slot(s) from one or more powers",
                Expected = context.MaxSlots,
                Actual = total,
            };
        }
    }
}
