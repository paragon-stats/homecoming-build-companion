using System.Text.Json.Serialization;

namespace CohEngine.Ai.Anthropic;

/// <summary>A non-streaming response, and the <c>message</c> of a <c>message_start</c> event.</summary>
/// <param name="MessageId">The message id.</param>
/// <param name="Model">The model that answered.</param>
/// <param name="Content">The content blocks.</param>
/// <param name="StopReason">Why generation stopped.</param>
/// <param name="Usage">The token counts.</param>
internal sealed record AnthropicResponse(
    [property: JsonPropertyName("id")] string? MessageId,
    string? Model,
    IReadOnlyList<AnthropicContentBlock>? Content,
    string? StopReason,
    AnthropicUsage? Usage);
