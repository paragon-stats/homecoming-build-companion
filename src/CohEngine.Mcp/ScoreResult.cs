namespace CohEngine.Mcp;

/// <summary>The <c>score</c> tool's result.</summary>
/// <param name="DataVersion">The engine build the answer came from.</param>
/// <param name="ClassName">The build's class.</param>
/// <param name="Metrics">Every registered metric.</param>
public sealed record ScoreResult(string DataVersion, string ClassName, IReadOnlyList<MetricView> Metrics);
