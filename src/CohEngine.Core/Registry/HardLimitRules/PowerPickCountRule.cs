using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>H-POWER-001: a build may pick at most 24 powers (inherents excluded).</summary>
internal static class PowerPickCountRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        int count = context.Picks.Count;
        if (count > PowerGroups.MaxPowerPicks)
        {
            string message = $"the build picks {count} powers; the game grants only {PowerGroups.MaxPowerPicks} picks "
                + "(inherents excluded)";
            yield return new Diagnostic("H-POWER-001", "error", message)
            {
                Fix = $"remove {count - PowerGroups.MaxPowerPicks} power pick(s)",
                Expected = PowerGroups.MaxPowerPicks,
                Actual = count,
            };
        }
    }
}
