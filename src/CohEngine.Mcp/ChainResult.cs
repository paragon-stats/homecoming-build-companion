using System.Text.Json;

namespace CohEngine.Mcp;

/// <summary>An attack-chain plan, as <c>chain --format json</c> prints it.</summary>
/// <param name="DataVersion">The hash of the data files the answer was computed from, or <c>unknown</c> when it read none.</param>
/// <param name="ExitCode">The exit code <c>chain</c> would give: 1 on an error finding, 2 on warnings only, else 0.</param>
/// <param name="Stances">One report per stance, as the CLI prints them.</param>
public sealed record ChainResult(string DataVersion, int ExitCode, JsonElement Stances);
