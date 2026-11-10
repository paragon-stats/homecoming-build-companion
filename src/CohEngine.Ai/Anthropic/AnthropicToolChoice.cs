namespace CohEngine.Ai.Anthropic;

/// <summary>The tool-choice setting.</summary>
/// <param name="Type"><c>auto</c>, <c>any</c>, <c>none</c> or <c>tool</c>.</param>
/// <param name="Name">The required tool when <paramref name="Type"/> is <c>tool</c>.</param>
internal sealed record AnthropicToolChoice(string Type, string? Name = null);
