using System.ClientModel;
using System.ClientModel.Primitives;

using Microsoft.Extensions.AI;

using OpenAI;

namespace CohEngine.Ai;

/// <summary>Builds the chat client for a provider and preset.</summary>
public static class ChatClientFactory
{
    /// <summary>Creates the client; the API key comes from the environment variable only.</summary>
    /// <param name="options">Provider, preset and overrides.</param>
    /// <returns>A chat client that sends to the chosen server.</returns>
    /// <exception cref="InvalidOperationException">The key variable is empty or unset (E130), or the base URL is not absolute http(s) (E132).</exception>
    public static IChatClient Create(ChatProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Provider != ChatProvider.OpenAiCompatible)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Provider, "Unknown provider.");
        }

        (string baseUrl, string? keyVariable) = options.Preset switch
        {
            ChatPreset.OpenAi => ("https://api.openai.com/v1", "OPENAI_API_KEY"),
            ChatPreset.Mistral => ("https://api.mistral.ai/v1", "MISTRAL_API_KEY"),
            ChatPreset.Local => ("http://localhost:11434/v1", null),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Preset, "Unknown preset."),
        };
        keyVariable = options.ApiKeyEnvironmentVariable ?? keyVariable;

        // Local servers ignore the key, but the SDK refuses an empty one.
        string key = "none";
        if (keyVariable is not null)
        {
            key = Environment.GetEnvironmentVariable(keyVariable) is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException(
                    $"{AiErrorCodes.MissingApiKey}: {keyVariable} is not set; export the API key in that variable or pass --api-key-env");
        }

        string url = options.BaseUrl ?? baseUrl;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"{AiErrorCodes.BadBaseUrl}: base URL '{url}' is not an absolute http(s) URL; pass --base-url https://host/v1");
        }

        OpenAIClientOptions clientOptions = new() { Endpoint = endpoint };
        if (options.Handler is not null)
        {
            clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(options.Handler, disposeHandler: false));
        }

        return new ErrorMappingChatClient(new OpenAIClient(new ApiKeyCredential(key), clientOptions).GetChatClient(options.Model).AsIChatClient(), keyVariable);
    }
}
