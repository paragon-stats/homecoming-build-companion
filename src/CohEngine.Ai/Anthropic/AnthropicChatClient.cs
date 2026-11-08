using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.Extensions.AI;

namespace CohEngine.Ai.Anthropic;

/// <summary>
/// An <see cref="IChatClient"/> for the Anthropic Messages API, written over <see cref="HttpClient"/> with
/// source-generated JSON because the official SDK is not Native AOT compatible. No retries.
/// </summary>
public sealed class AnthropicChatClient : IChatClient
{
    /// <summary>The environment variable the API key is read from when no other is named.</summary>
    public const string DefaultApiKeyEnvironmentVariable = "ANTHROPIC_API_KEY";

    /// <summary>The <c>max_tokens</c> sent when <see cref="ChatOptions.MaxOutputTokens"/> is unset; the API requires one.</summary>
    public const int DefaultMaxTokens = 1024;

    private const string DefaultBaseUrl = "https://api.anthropic.com";
    private const string ApiVersion = "2023-06-01";

    // One connection pool for every client that brings no handler.
    private static readonly HttpClient SharedHttp = new();

    private readonly HttpClient http;
    private readonly bool ownsHttp;
    private readonly string apiKey;
    private readonly Uri endpoint;
    private readonly ChatClientMetadata metadata;

    /// <summary>Initializes a new instance of the <see cref="AnthropicChatClient"/> class.</summary>
    /// <param name="model">The model sent when <see cref="ChatOptions.ModelId"/> is unset.</param>
    /// <param name="baseUrl">Replaces <c>https://api.anthropic.com</c> (for a proxy or gateway).</param>
    /// <param name="apiKeyEnvironmentVariable">Replaces <c>ANTHROPIC_API_KEY</c>; the key is read from here and nowhere else.</param>
    /// <param name="handler">Replaces the network layer; tests pass a fake, production leaves it null.</param>
    /// <exception cref="InvalidOperationException">The key variable is empty or unset (E140).</exception>
    public AnthropicChatClient(string model, string? baseUrl = null, string? apiKeyEnvironmentVariable = null, HttpMessageHandler? handler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        string keyVariable = apiKeyEnvironmentVariable ?? DefaultApiKeyEnvironmentVariable;
        apiKey = Environment.GetEnvironmentVariable(keyVariable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{AnthropicErrorCodes.MissingApiKey}: {keyVariable} is not set; export the API key in that variable or pass --api-key-env");

        Uri root = new(baseUrl ?? DefaultBaseUrl);
        endpoint = new Uri(root.AbsoluteUri.TrimEnd('/') + "/v1/messages");
        ownsHttp = handler is not null;
        http = handler is null ? SharedHttp : new HttpClient(handler, disposeHandler: false);
        metadata = new ChatClientMetadata("anthropic", root, model);
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(messages, options, stream: false);
        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (body.ConfigureAwait(false))
        {
            AnthropicResponse message = await JsonSerializer.DeserializeAsync(body, AnthropicJsonContext.Default.AnthropicResponse, cancellationToken).ConfigureAwait(false)
                ?? throw new JsonException("The Messages API returned a null body.");
            List<AIContent> contents = [.. (message.Content ?? []).Select(ToContent).OfType<AIContent>()];
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
            {
                ResponseId = message.MessageId,
                ModelId = message.Model,
                FinishReason = ToFinishReason(message.StopReason),
                Usage = message.Usage is { } usage ? ToUsage(usage.InputTokens, usage.OutputTokens) : null,
                RawRepresentation = message,
            };
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Streaming lands in the next change.");
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }

        return serviceType == typeof(ChatClientMetadata) ? metadata : serviceType.IsInstanceOfType(this) ? this : null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (ownsHttp)
        {
            http.Dispose();
        }
    }

    /// <summary>Maps the conversation and options to the request body.</summary>
    /// <param name="messages">The conversation.</param>
    /// <param name="options">The chat options.</param>
    /// <param name="stream">Whether to ask for Server-Sent Events.</param>
    /// <param name="defaultModel">The model used when the options name none.</param>
    /// <returns>The body.</returns>
    internal static AnthropicRequest BuildRequest(IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream, string defaultModel)
    {
        ArgumentNullException.ThrowIfNull(messages);
        List<string> system = [];
        if (options?.Instructions is { Length: > 0 } instructions)
        {
            system.Add(instructions);
        }

        List<AnthropicMessage> turns = [];
        foreach (ChatMessage message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                system.Add(message.Text);
                continue;
            }

            string role = message.Role == ChatRole.Assistant ? "assistant" : "user";
            turns.Add(new AnthropicMessage(role, [.. message.Contents.Select(ToBlock)]));
        }

        List<AnthropicTool>? tools = options?.Tools is { Count: > 0 } declared ? [.. declared.Select(ToTool)] : null;
        return new AnthropicRequest(
            options?.ModelId ?? defaultModel,
            options?.MaxOutputTokens ?? DefaultMaxTokens,
            turns,
            system.Count > 0 ? string.Join("\n\n", system) : null,
            options?.Temperature,
            tools,
            ToToolChoice(options?.ToolMode),
            stream ? true : null);
    }

    private static AnthropicContentBlock ToBlock(AIContent content)
    {
        return content switch
        {
            TextContent text => new AnthropicContentBlock("text", Text: text.Text),
            FunctionCallContent call => new AnthropicContentBlock(
                "tool_use",
                CallId: call.CallId,
                Name: call.Name,
                Input: JsonSerializer.SerializeToElement(
                    call.Arguments ?? new Dictionary<string, object?>(StringComparer.Ordinal),
                    AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(IDictionary<string, object?>)))),
            FunctionResultContent result => new AnthropicContentBlock(
                "tool_result",
                ToolUseId: result.CallId,
                Content: result.Result as string ?? JsonSerializer.Serialize(result.Result, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(object)))),
            _ => throw new NotSupportedException($"The Anthropic client does not send {content.GetType().Name}."),
        };
    }

    private static AnthropicTool ToTool(AITool tool)
    {
        return tool is AIFunctionDeclaration function
            ? new AnthropicTool(function.Name, function.Description is { Length: > 0 } description ? description : null, function.JsonSchema)
            : throw new NotSupportedException($"The Anthropic client does not send the {tool.GetType().Name} tool.");
    }

    private static AnthropicToolChoice? ToToolChoice(ChatToolMode? mode)
    {
        return mode switch
        {
            null => null,
            NoneChatToolMode => new AnthropicToolChoice("none"),
            RequiredChatToolMode { RequiredFunctionName: { } name } => new AnthropicToolChoice("tool", name),
            RequiredChatToolMode => new AnthropicToolChoice("any"),
            _ => new AnthropicToolChoice("auto"),
        };
    }

    private static AIContent? ToContent(AnthropicContentBlock block)
    {
        return block.Type switch
        {
            "text" => new TextContent(block.Text),
            "tool_use" => ToCall(block.CallId ?? string.Empty, block.Name ?? string.Empty, block.Input),
            _ => null,
        };
    }

    private static FunctionCallContent ToCall(string callId, string name, JsonElement? input)
    {
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal);
        if (input is { ValueKind: JsonValueKind.Object } element)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                arguments[property.Name] = property.Value;
            }
        }

        return new FunctionCallContent(callId, name, arguments);
    }

    private static ChatFinishReason? ToFinishReason(string? stopReason)
    {
        return stopReason switch
        {
            null => null,
            "end_turn" or "stop_sequence" => ChatFinishReason.Stop,
            "max_tokens" => ChatFinishReason.Length,
            "tool_use" => ChatFinishReason.ToolCalls,
            "refusal" => ChatFinishReason.ContentFilter,
            _ => new ChatFinishReason(stopReason),
        };
    }

    private static UsageDetails ToUsage(int input, int output)
    {
        return new UsageDetails { InputTokenCount = input, OutputTokenCount = output, TotalTokenCount = input + output };
    }

    private HttpRequestMessage CreateRequest(IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream)
    {
        AnthropicRequest body = BuildRequest(messages, options, stream, metadata.DefaultModelId!);
        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(body, AnthropicJsonContext.Default.AnthropicRequest),
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        return request;
    }
}
