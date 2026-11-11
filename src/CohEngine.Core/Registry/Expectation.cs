namespace CohEngine.Core.Registry;

/// <summary>A <c>--expect name=value</c> claim, both halves as typed.</summary>
/// <param name="Metric">The scoring metric name, or a <c>defense.&lt;type&gt;</c> or <c>resistance.&lt;type&gt;</c> total.</param>
/// <param name="Value">The claimed value text: a number, or <c>true</c>/<c>false</c> for a flag metric.</param>
public sealed record Expectation(string Metric, string Value);
