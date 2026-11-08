namespace CohEngine.Core.Profiles;

/// <summary>One resolved goal target: a metric, a comparison op and a concrete value.</summary>
/// <param name="Metric">The metric name.</param>
/// <param name="Comparison">The comparison: <c>==</c>, <c>&gt;=</c> or <c>&lt;=</c>.</param>
/// <param name="Value">The value the metric is compared with.</param>
public sealed record Target(string Metric, string Comparison, MetricValue Value);
