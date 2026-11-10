namespace CohEngine.Mcp;

/// <summary>The <c>totals</c> tool's result.</summary>
/// <param name="DataVersion">The engine build the answer came from.</param>
/// <param name="ClassName">The build's class.</param>
/// <param name="Totals">The uncapped totals.</param>
/// <param name="TotalsCapped">The AT- and server-capped totals.</param>
public sealed record TotalsResult(string DataVersion, string ClassName, TotalsView Totals, TotalsView TotalsCapped);
