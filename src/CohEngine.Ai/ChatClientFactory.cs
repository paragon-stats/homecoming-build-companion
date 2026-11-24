using System.ClientModel;
using System.ClientModel.Primitives;

using CohEngine.Ai.Anthropic;

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
        if (options.Provider == ChatProvider.Anthropic)
        {
            return CreateAnthropic(options);
        }

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

        string key = ReadKey(keyVariable);
        string url = options.BaseUrl ?? baseUrl;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"{AiErrorCodes.BadBaseUrl}: base URL '{url}' is not an absolute http(s) URL; pass --base-url https://host/v1");
        }

        RequireSecure(endpoint, options.Preset == ChatPreset.Local);
        OpenAIClientOptions clientOptions = new() { Endpoint = endpoint };
        if (options.Handler is not null)
        {
            clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(options.Handler, disposeHandler: false));
        }

        return new ErrorMappingChatClient(new OpenAIClient(new ApiKeyCredential(key), clientOptions).GetChatClient(options.Model).AsIChatClient(), keyVariable);
    }

    // The base URL option wins, then ANTHROPIC_BASE_URL; the client falls back to its built-in default.
    private static AnthropicChatClient CreateAnthropic(ChatProviderOptions options)
    {
        if (options.BaseUrl is not null)
        {
            RequireAnthropicUrl(options.BaseUrl, variable: null);
            return new AnthropicChatClient(options.Model, options.BaseUrl, options.ApiKeyEnvironmentVariable, options.Handler);
        }

        string variable = options.BaseUrlEnvironmentVariable ?? AnthropicChatClient.BaseUrlEnvironmentVariable;
        string? fromEnvironment = Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? value : null;
        if (fromEnvironment is not null)
        {
            RequireAnthropicUrl(fromEnvironment, variable);
        }

        return new AnthropicChatClient(options.Model, fromEnvironment, options.ApiKeyEnvironmentVariable, options.Handler);
    }

    // A value from the environment names its variable and its fix; one from the option names the option.
    private static void RequireAnthropicUrl(string baseUrl, string? variable)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? root) || root.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(variable is null
                ? $"{AiErrorCodes.BadBaseUrl}: base URL '{baseUrl}' is not an absolute http(s) URL; pass --base-url https://host"
                : $"{AiErrorCodes.BadBaseUrl}: {variable} '{baseUrl}' is not an absolute http(s) URL; set {variable} to https://host or unset it");
        }

        if (variable is null)
        {
            RequireSecure(root, local: false);
        }
        else if (!root.IsLoopback && !string.Equals(root.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{AiErrorCodes.BadBaseUrl}: {variable} '{root}' is http on a remote host, which would send the key unencrypted; set {variable} to an https URL or a loopback address such as http://localhost, or unset it");
        }
    }

    // Local servers ignore the key, but the SDK refuses an empty one.
    private static string ReadKey(string? keyVariable)
    {
        if (keyVariable is null)
        {
            return "none";
        }

        return Environment.GetEnvironmentVariable(keyVariable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{AiErrorCodes.MissingApiKey}: {keyVariable} is not set; export the API key in that variable or pass --api-key-env");
    }

    // A key sent over plain http crosses the network readable; only a loopback host or the local preset may do that.
    private static void RequireSecure(Uri endpoint, bool local)
    {
        if (!local && !endpoint.IsLoopback && !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{AiErrorCodes.BadBaseUrl}: base URL '{endpoint}' is http on a remote host, which would send the key unencrypted; use https, or a loopback address such as http://localhost");
        }
    }
}
