namespace CohEngine.Ai;

/// <summary>Default base URL and key variable for a known server.</summary>
public enum ChatPreset
{
    /// <summary>api.openai.com, key in OPENAI_API_KEY.</summary>
    OpenAi,

    /// <summary>api.mistral.ai, key in MISTRAL_API_KEY.</summary>
    Mistral,

    /// <summary>Ollama on localhost:11434, no key.</summary>
    Local,
}
