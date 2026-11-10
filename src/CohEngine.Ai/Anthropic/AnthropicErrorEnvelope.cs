namespace CohEngine.Ai.Anthropic;

/// <summary>The body of a non-2xx response, and the data of an <c>error</c> event.</summary>
/// <param name="Error">The error object.</param>
internal sealed record AnthropicErrorEnvelope(AnthropicError? Error);
