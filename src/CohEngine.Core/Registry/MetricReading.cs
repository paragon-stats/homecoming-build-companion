using CohEngine.Core.Profiles;

namespace CohEngine.Core.Registry;

/// <summary>One registered scoring metric's value for a build.</summary>
/// <param name="Name">The metric name.</param>
/// <param name="Value">The value: a number or a flag.</param>
public sealed record MetricReading(string Name, MetricValue Value);
