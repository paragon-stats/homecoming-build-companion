using CohEngine.Core.Data;
using CohEngine.Core.Profiles;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Registry.ScoringMetrics;

/// <summary>The <c>perma_hasten</c> metric: Hasten's buffed recharge is within its 120 second duration.</summary>
internal static class PermaHastenMetric
{
    /// <summary>Computes the metric.</summary>
    /// <param name="totals">The totals, unused here.</param>
    /// <param name="powerStats">The per-power statistics.</param>
    /// <returns>The value.</returns>
    public static MetricValue Evaluate(TotalStatistics totals, IReadOnlyList<PowerStats> powerStats)
    {
        return MetricValue.Flag(Scoring.PermaRecharge(powerStats, ".Hasten", Scoring.HastenDuration));
    }
}
