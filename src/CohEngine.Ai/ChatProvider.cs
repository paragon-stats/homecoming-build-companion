namespace CohEngine.Ai;

/// <summary>The wire protocol the client speaks.</summary>
public enum ChatProvider
{
    /// <summary>The OpenAI chat completions protocol, as served by OpenAI, Mistral and local servers.</summary>
    OpenAiCompatible,

    /// <summary>The Anthropic Messages API; the preset does not apply.</summary>
    Anthropic,
}
