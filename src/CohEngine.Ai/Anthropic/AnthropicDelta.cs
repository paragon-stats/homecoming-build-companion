namespace CohEngine.Ai.Anthropic;

/// <summary>A streamed delta.</summary>
/// <param name="Type"><c>text_delta</c> or <c>input_json_delta</c>; null on <c>message_delta</c>.</param>
/// <param name="Text">The text of a <c>text_delta</c>.</param>
/// <param name="PartialJson">The arguments fragment of an <c>input_json_delta</c>.</param>
/// <param name="StopReason">The stop reason of a <c>message_delta</c>.</param>
internal sealed record AnthropicDelta(string? Type, string? Text, string? PartialJson, string? StopReason);
