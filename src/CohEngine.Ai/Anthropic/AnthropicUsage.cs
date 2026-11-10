namespace CohEngine.Ai.Anthropic;

/// <summary>Token counts.</summary>
/// <param name="InputTokens">Prompt tokens; absent or null in some <c>message_delta</c> events.</param>
/// <param name="OutputTokens">Generated tokens.</param>
internal sealed record AnthropicUsage(int? InputTokens, int? OutputTokens);
