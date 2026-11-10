using System.Text.Json;

namespace CohEngine.Ai.Anthropic;

/// <summary>A function declaration.</summary>
/// <param name="Name">The tool name.</param>
/// <param name="Description">What the tool does.</param>
/// <param name="InputSchema">The JSON schema of the arguments.</param>
internal sealed record AnthropicTool(string Name, string? Description, JsonElement InputSchema);
