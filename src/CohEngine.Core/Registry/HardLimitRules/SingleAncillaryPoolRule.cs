using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>H-POOL-002: at most one Ancillary/Patron (Epic) pool may be selected.</summary>
internal static class SingleAncillaryPoolRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        string[] pools = [.. context.Picks.Where(pick => string.Equals(PowerGroups.Group(pick), PowerGroups.Ancillary, StringComparison.Ordinal)).Select(PowerGroups.PowersetKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        if (pools.Length > 1)
        {
            string message = $"the build draws from {pools.Length} Ancillary/Patron pools ({PyText.List(pools)}); "
                + "only one may be selected (changing it requires a respec)";
            yield return new Diagnostic("H-POOL-002", "error", message)
            {
                Fix = "keep powers from a single Ancillary/Patron pool",
                Expected = 1,
                Actual = pools.Length,
            };
        }
    }
}
