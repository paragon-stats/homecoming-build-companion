using CohEngine.Core.Data;

namespace CohEngine.Core.Registry;

/// <summary>A build dump run through the whole totals pipeline.</summary>
/// <param name="ClassName">The build's class name, from <c>totals.json</c>.</param>
/// <param name="Totals">The uncapped totals.</param>
/// <param name="TotalsCapped">The AT- and server-capped totals.</param>
/// <param name="Metrics">Every registered scoring metric, in registry order.</param>
public sealed record ComputedBuild(string ClassName, TotalStatistics Totals, TotalStatistics TotalsCapped, IReadOnlyList<MetricReading> Metrics);
