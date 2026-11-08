namespace CohEngine.Ai.Anthropic;

/// <summary>The API's error object.</summary>
/// <param name="Type">The error type, such as <c>authentication_error</c>.</param>
/// <param name="Message">The human-readable message.</param>
internal sealed record AnthropicError(string? Type, string? Message);
