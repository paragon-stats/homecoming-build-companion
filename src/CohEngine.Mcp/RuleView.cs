namespace CohEngine.Mcp;

/// <summary>One rule or metric the registry holds.</summary>
/// <param name="RuleId">The rule id.</param>
/// <param name="Summary">The one-line summary.</param>
public sealed record RuleView(string RuleId, string Summary);
