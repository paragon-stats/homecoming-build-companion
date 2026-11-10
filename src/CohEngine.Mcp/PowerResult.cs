namespace CohEngine.Mcp;

/// <summary>The <c>power_lookup</c> tool's result.</summary>
/// <param name="DataVersion">The engine build the answer came from.</param>
/// <param name="Power">The power.</param>
public sealed record PowerResult(string DataVersion, PowerView Power);
