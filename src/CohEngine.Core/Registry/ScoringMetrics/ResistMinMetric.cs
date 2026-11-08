using CohEngine.Core.Data;
using CohEngine.Core.Profiles;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Registry.ScoringMetrics;

/// <summary>The <c>resist_min</c> metric: the lowest typed resistance.</summary>
internal static class ResistMinMetric
{
    /// <summary>Computes the metric.</summary>
    /// <param name="totals">The totals.</param>
    /// <param name="powerStats">The per-power statistics, unused here.</param>
    /// <returns>The value.</returns>
    public static MetricValue Evaluate(TotalStatistics totals, IReadOnlyList<PowerStats> powerStats)
    {
        return MetricValue.Of(Scoring.ResistMin(totals));
    }
}
