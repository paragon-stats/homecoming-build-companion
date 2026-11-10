namespace CohEngine.Ai;

/// <summary>The wire protocol the client speaks.</summary>
public enum ChatProvider
{
    /// <summary>The OpenAI chat completions protocol, as served by OpenAI, Mistral and local servers.</summary>
    OpenAiCompatible,
}
