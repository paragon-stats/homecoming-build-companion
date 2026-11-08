using CohEngine.Core.Data;
using CohEngine.Core.Profiles;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Registry.ScoringMetrics;

/// <summary>The <c>perma_dom</c> metric: Domination's buffed recharge is within its 90 second duration.</summary>
internal static class PermaDominationMetric
{
    /// <summary>Computes the metric.</summary>
    /// <param name="totals">The totals, unused here.</param>
    /// <param name="powerStats">The per-power statistics.</param>
    /// <returns>The value.</returns>
    public static MetricValue Evaluate(TotalStatistics totals, IReadOnlyList<PowerStats> powerStats)
    {
        return MetricValue.Flag(Scoring.PermaRecharge(powerStats, ".Domination", Scoring.DominationDuration));
    }
}
