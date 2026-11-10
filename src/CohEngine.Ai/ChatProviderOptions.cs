namespace CohEngine.Ai;

/// <summary>Settings for one chat client.</summary>
/// <param name="Provider">The wire protocol.</param>
/// <param name="Preset">The server defaults.</param>
/// <param name="Model">The model name sent with each request.</param>
/// <param name="BaseUrl">Replaces the preset's base URL (for vLLM, llama.cpp or a proxy).</param>
/// <param name="ApiKeyEnvironmentVariable">Replaces the preset's key variable; the key is read from here and nowhere else.</param>
/// <param name="Handler">Replaces the network layer; tests pass a fake, production leaves it null.</param>
public sealed record ChatProviderOptions(
    ChatProvider Provider,
    ChatPreset Preset,
    string Model,
    string? BaseUrl = null,
    string? ApiKeyEnvironmentVariable = null,
    HttpMessageHandler? Handler = null);
