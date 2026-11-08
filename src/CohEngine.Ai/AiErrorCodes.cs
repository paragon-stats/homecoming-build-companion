namespace CohEngine.Ai;

/// <summary>Error codes E130 to E139 are reserved for the chat clients.</summary>
public static class AiErrorCodes
{
    /// <summary>The environment variable that should hold the API key is empty or unset.</summary>
    public const string MissingApiKey = "E130";

    /// <summary>The provider answered with an HTTP error status.</summary>
    public const string ProviderRejected = "E131";

    /// <summary>The base URL is not an absolute http or https URL.</summary>
    public const string BadBaseUrl = "E132";
}
