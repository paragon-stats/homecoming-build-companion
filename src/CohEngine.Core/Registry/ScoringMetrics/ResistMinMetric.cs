using CohEngine.Core.Data;
using CohEngine.Core.Profiles;

namespace CohEngine.Core.Registry.ScoringMetrics;

/// <summary>The <c>resist_min</c> metric: the lowest typed resistance.</summary>
internal static class ResistMinMetric
{
    /// <summary>Computes the metric.</summary>
    /// <param name="totals">The totals.</param>
    /// <returns>The value.</returns>
    public static MetricValue Evaluate(TotalStatistics totals)
    {
        return MetricValue.Of(Scoring.ResistMin(totals));
    }
}
