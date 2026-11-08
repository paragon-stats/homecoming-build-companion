using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>H-POOL-001: a build may draw from at most 4 power pools (Ancillary/Patron excluded).</summary>
internal static class PoolCountRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        string[] pools = [.. context.Picks.Where(pick => string.Equals(PowerGroups.Group(pick), PowerGroups.Pool, StringComparison.Ordinal)).Select(PowerGroups.PowersetKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        if (pools.Length > PowerGroups.MaxPowerPools)
        {
            string message = $"the build draws from {pools.Length} power pools ({PyText.List(pools)}); the limit is {PowerGroups.MaxPowerPools}";
            yield return new Diagnostic("H-POOL-001", "error", message)
            {
                Fix = $"drop a pool so at most {PowerGroups.MaxPowerPools} remain",
                Expected = PowerGroups.MaxPowerPools,
                Actual = pools.Length,
            };
        }
    }
}
