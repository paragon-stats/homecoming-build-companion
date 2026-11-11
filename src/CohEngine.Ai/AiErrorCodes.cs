namespace CohEngine.Ai;

/// <summary>Error codes E130 to E139 are reserved for the chat clients, and E140 to E149 for the Anthropic chat client.</summary>
public static class AiErrorCodes
{
    /// <summary>The environment variable that should hold the API key is empty or unset.</summary>
    public const string MissingApiKey = "E130";

    /// <summary>The provider answered with an HTTP error status.</summary>
    public const string ProviderRejected = "E131";

    /// <summary>The base URL is not an absolute http or https URL.</summary>
    public const string BadBaseUrl = "E132";

    /// <summary>No HTTP response came back: the connection failed or the server is down.</summary>
    public const string ProviderUnreachable = "E133";

    /// <summary>The E140 to E149 block, for the Anthropic chat client.</summary>
    public static class Anthropic
    {
        /// <summary>The environment variable that should hold the Anthropic API key is empty or unset.</summary>
        public const string MissingApiKey = "E140";

        /// <summary>The response stream ended before <c>message_stop</c>, or with a tool call still open.</summary>
        public const string IncompleteStream = "E141";

        /// <summary>A tool result is neither a string nor a <c>JsonElement</c>, so a Native AOT build cannot serialize it.</summary>
        public const string UnsupportedToolResult = "E142";
    }
}
