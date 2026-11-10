namespace CohEngine.Core.Registry;

/// <summary>A <c>--expect metric=value</c> claim, both halves as typed.</summary>
/// <param name="Metric">The scoring metric name.</param>
/// <param name="Value">The claimed value text: a number, or <c>true</c>/<c>false</c> for a flag metric.</param>
public sealed record Expectation(string Metric, string Value);
