using CohEngine.Core.Data;
using CohEngine.Core.Profiles;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Registry;

/// <summary>A scoring metric as the registry holds it: the name a profile target references and how to compute it.</summary>
/// <param name="Info">The metric's identity.</param>
/// <param name="Evaluate">The metric: totals and per-power statistics to a number or a flag.</param>
internal sealed record RegisteredMetric(RuleInfo Info, Func<TotalStatistics, IReadOnlyList<PowerStats>, MetricValue> Evaluate);
