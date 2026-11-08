namespace CohEngine.Ai.Anthropic;

/// <summary>The Messages API request body.</summary>
/// <param name="Model">The model name.</param>
/// <param name="MaxTokens">The output-token ceiling the API requires.</param>
/// <param name="Messages">The user and assistant turns.</param>
/// <param name="System">The system prompt, which the API takes at the top level.</param>
/// <param name="Temperature">The sampling temperature.</param>
/// <param name="Tools">The function declarations.</param>
/// <param name="ToolChoice">How the model may pick a tool.</param>
/// <param name="Stream">Whether the response streams as Server-Sent Events.</param>
internal sealed record AnthropicRequest(
    string Model,
    int MaxTokens,
    IReadOnlyList<AnthropicMessage> Messages,
    string? System,
    float? Temperature,
    IReadOnlyList<AnthropicTool>? Tools,
    AnthropicToolChoice? ToolChoice,
    bool? Stream);
