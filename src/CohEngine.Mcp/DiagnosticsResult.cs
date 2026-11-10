using System.Text.Json;

namespace CohEngine.Mcp;

/// <summary>A diagnostics run, as <c>validate --format json</c> prints it.</summary>
/// <param name="DataVersion">The engine build the answer came from.</param>
/// <param name="ExitCode">The exit code <c>validate</c> would give: 1 on an error, 2 on warnings only, else 0.</param>
/// <param name="Diagnostics">The diagnostics array, byte for byte what the CLI prints.</param>
public sealed record DiagnosticsResult(string DataVersion, int ExitCode, JsonElement Diagnostics);
