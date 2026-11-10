namespace CohEngine.Ai.Anthropic;

/// <summary>One turn of the conversation.</summary>
/// <param name="Role"><c>user</c> or <c>assistant</c>.</param>
/// <param name="Content">The turn's content blocks.</param>
internal sealed record AnthropicMessage(string Role, IReadOnlyList<AnthropicContentBlock> Content);
