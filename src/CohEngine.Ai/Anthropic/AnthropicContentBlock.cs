using System.Text.Json;
using System.Text.Json.Serialization;

namespace CohEngine.Ai.Anthropic;

/// <summary>A content block, in a request or a response; the fields a block type does not use stay null.</summary>
/// <param name="Type"><c>text</c>, <c>tool_use</c> or <c>tool_result</c>.</param>
/// <param name="Text">The text of a <c>text</c> block.</param>
/// <param name="CallId">The call id of a <c>tool_use</c> block.</param>
/// <param name="Name">The tool name of a <c>tool_use</c> block.</param>
/// <param name="Input">The arguments object of a <c>tool_use</c> block.</param>
/// <param name="ToolUseId">The call id a <c>tool_result</c> block answers.</param>
/// <param name="Content">The result text of a <c>tool_result</c> block.</param>
internal sealed record AnthropicContentBlock(
    string Type,
    string? Text = null,
    [property: JsonPropertyName("id")] string? CallId = null,
    string? Name = null,
    JsonElement? Input = null,
    string? ToolUseId = null,
    string? Content = null);
