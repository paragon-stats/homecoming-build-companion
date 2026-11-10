namespace CohEngine.Mcp;

/// <summary>One scoring metric's value: a number, or a flag.</summary>
/// <param name="Name">The metric name.</param>
/// <param name="Number">The value for a numeric metric, otherwise null.</param>
/// <param name="Flag">The value for a flag metric, otherwise null.</param>
public sealed record MetricView(string Name, double? Number, bool? Flag);
