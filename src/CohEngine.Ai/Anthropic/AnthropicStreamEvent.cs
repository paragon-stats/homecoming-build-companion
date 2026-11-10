namespace CohEngine.Ai.Anthropic;

/// <summary>The data of one streamed event; the fields an event type does not use stay null.</summary>
/// <param name="Type">The event type.</param>
/// <param name="Index">The content-block index of a block event.</param>
/// <param name="Message">The message of <c>message_start</c>.</param>
/// <param name="ContentBlock">The block of <c>content_block_start</c>.</param>
/// <param name="Delta">The delta of <c>content_block_delta</c> and <c>message_delta</c>.</param>
/// <param name="Usage">The usage of <c>message_delta</c>.</param>
/// <param name="Error">The error of an <c>error</c> event.</param>
internal sealed record AnthropicStreamEvent(
    string? Type,
    int Index,
    AnthropicResponse? Message,
    AnthropicContentBlock? ContentBlock,
    AnthropicDelta? Delta,
    AnthropicUsage? Usage,
    AnthropicError? Error);
