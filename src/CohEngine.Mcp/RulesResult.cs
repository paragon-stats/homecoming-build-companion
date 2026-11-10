namespace CohEngine.Mcp;

/// <summary>The <c>rules</c> tool's result: every registry, in the order the CLI lists them.</summary>
/// <param name="DataVersion">The engine build the answer came from.</param>
/// <param name="Legality">The legality rules.</param>
/// <param name="HardLimits">The hard-limit rules.</param>
/// <param name="EnhancementAspects">The enhancement aspect handlers.</param>
/// <param name="Metrics">The scoring metrics.</param>
public sealed record RulesResult(string DataVersion, IReadOnlyList<RuleView> Legality, IReadOnlyList<RuleView> HardLimits, IReadOnlyList<RuleView> EnhancementAspects, IReadOnlyList<RuleView> Metrics);
