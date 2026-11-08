namespace CohEngine.Ai.Anthropic;

/// <summary>Token counts.</summary>
/// <param name="InputTokens">Prompt tokens.</param>
/// <param name="OutputTokens">Generated tokens.</param>
internal sealed record AnthropicUsage(int InputTokens, int OutputTokens);
