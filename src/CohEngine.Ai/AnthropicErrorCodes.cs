namespace CohEngine.Ai;

/// <summary>Error codes E140 to E149 are reserved for the Anthropic chat client.</summary>
public static class AnthropicErrorCodes
{
    /// <summary>The environment variable that should hold the Anthropic API key is empty or unset.</summary>
    public const string MissingApiKey = "E140";

    /// <summary>The response stream ended before <c>message_stop</c>, or with a tool call still open.</summary>
    public const string IncompleteStream = "E141";

    /// <summary>A tool result is neither a string nor a <c>JsonElement</c>, so a Native AOT build cannot serialize it.</summary>
    public const string UnsupportedToolResult = "E142";
}
