using CohEngine.Core.Data;
using CohEngine.Core.Profiles;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Registry.ScoringMetrics;

/// <summary>The <c>defense_positional_min</c> metric: the positional softcap measure.</summary>
internal static class DefensePositionalMinMetric
{
    /// <summary>Computes the metric.</summary>
    /// <param name="totals">The totals.</param>
    /// <param name="powerStats">The per-power statistics, unused here.</param>
    /// <returns>The value.</returns>
    public static MetricValue Evaluate(TotalStatistics totals, IReadOnlyList<PowerStats> powerStats)
    {
        return MetricValue.Of(Scoring.DefensePositionalMin(totals));
    }
}
