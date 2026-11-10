using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
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

    // One connection pool for every client that brings no handler. The pool recycles connections so DNS
    // changes are seen, and the timeout allows a long generation that the 100 s default would cut off.
    private static readonly HttpClient SharedHttp = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = TimeSpan.FromMinutes(10),
    };

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
        metadata = new ChatClientMetadata("anthropic", root, model);

        // Last, so no later statement can throw and leave the owned client undisposed.
        ownsHttp = handler is not null;
        http = handler is null ? SharedHttp : new HttpClient(handler, disposeHandler: false);
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint);
        FillRequest(request, messages, options, stream: false);
        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await ThrowIfFailedAsync(response, cancellationToken).ConfigureAwait(false);
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
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint);
        FillRequest(request, messages, options, stream: true);
        using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfFailedAsync(response, cancellationToken).ConfigureAwait(false);
        Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (body.ConfigureAwait(false))
        {
            StreamState state = new();
            await foreach (SseEvent sse in SseParser.ReadAsync(body, cancellationToken).ConfigureAwait(false))
            {
                if (HandleEvent(state, sse, response) is { } update)
                {
                    yield return update;
                }
            }

            if (!state.Stopped || state.Pending.Count > 0)
            {
                throw new AnthropicApiException(
                    (int)response.StatusCode,
                    "incomplete_stream",
                    $"{AnthropicErrorCodes.IncompleteStream}: the response stream ended before message_stop; retry the request",
                    Redact(RequestIdOf(response)));
            }
        }
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
            turns.Add(new AnthropicMessage(role, [.. message.Contents.Select(content => ToBlock(content, nameof(messages)))]));
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

    private static AnthropicContentBlock ToBlock(AIContent content, string messagesParameter)
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
                Content: ToResultText(result.Result, messagesParameter) is { } resultText ? JsonSerializer.SerializeToElement(resultText, AnthropicJsonContext.Default.String) : null),
            _ => throw new NotSupportedException($"The Anthropic client does not send {content.GetType().Name}."),
        };
    }

    // Serializing an arbitrary object needs metadata that a Native AOT build does not have.
    // The error names the public parameter the bad result arrived through.
    private static string? ToResultText(object? result, string messagesParameter)
    {
        return result switch
        {
            null => null,
            string text => text,
            JsonElement element => element.GetRawText(),
            _ => throw new ArgumentException(
                $"{AnthropicErrorCodes.UnsupportedToolResult}: tool result must be a string or JsonElement under AOT; serialize it before returning it",
                messagesParameter),
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

    private static UsageDetails ToUsage(int? input, int? output)
    {
        return new UsageDetails { InputTokenCount = input, OutputTokenCount = output, TotalTokenCount = input + output };
    }

    private static JsonElement? ParseArguments(StringBuilder json)
    {
        if (json.Length == 0)
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(json.ToString());
        return document.RootElement.Clone();
    }

    private static string? RequestIdOf(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("request-id", out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
    }

    private static AnthropicError? ReadError(string text)
    {
        try
        {
            return JsonSerializer.Deserialize(text, AnthropicJsonContext.Default.AnthropicErrorEnvelope)?.Error;
        }
        catch (JsonException)
        {
            // A proxy's HTML error page: the caller falls back to the status line.
            return null;
        }
    }

    private async Task ThrowIfFailedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw ApiError(response, ReadError(text), response.ReasonPhrase);
    }

    private AnthropicApiException ApiError(HttpResponseMessage response, AnthropicError? error, string? fallbackMessage)
    {
        return new AnthropicApiException(
            (int)response.StatusCode,
            Redact(error?.Type) ?? "unknown_error",
            Redact(error?.Message ?? fallbackMessage) ?? string.Empty,
            Redact(RequestIdOf(response)));
    }

    private ChatResponseUpdate? HandleEvent(StreamState state, SseEvent sse, HttpResponseMessage response)
    {
        AnthropicStreamEvent data = JsonSerializer.Deserialize(sse.Data, AnthropicJsonContext.Default.AnthropicStreamEvent)
            ?? throw new JsonException($"The Messages API sent a null {sse.Event} event.");
        List<AIContent> contents = [];
        ChatFinishReason? finishReason = null;

        // Events with no case here (ping, thinking deltas) carry nothing the caller needs.
        switch (sse.Event)
        {
            case "message_start":
                state.ResponseId = data.Message?.MessageId;
                state.ModelId = data.Message?.Model;
                state.InputTokens = data.Message?.Usage?.InputTokens;
                break;
            case "content_block_start" when data.ContentBlock is { Type: "tool_use" } block:
                state.Pending[data.Index] = new PendingCall(block.CallId ?? string.Empty, block.Name ?? string.Empty, new StringBuilder());
                break;
            case "content_block_delta" when data.Delta is { Type: "text_delta", Text: { } text }:
                contents.Add(new TextContent(text));
                break;
            case "content_block_delta" when data.Delta is { Type: "input_json_delta" } delta:
                PendingCall open = state.Pending.TryGetValue(data.Index, out PendingCall? found)
                    ? found
                    : throw new JsonException($"The Messages API sent input_json_delta for content block {data.Index}, which no tool_use block started.");
                open.Json.Append(delta.PartialJson);
                break;
            case "content_block_stop" when state.Pending.Remove(data.Index, out PendingCall? call):
                contents.Add(ToCall(call.CallId, call.Name, ParseArguments(call.Json)));
                break;
            case "message_delta":
                finishReason = ToFinishReason(data.Delta?.StopReason);
                contents.Add(new UsageContent(ToUsage(state.InputTokens, data.Usage?.OutputTokens)));
                break;
            case "message_stop":
                state.Stopped = true;
                break;
            case "error":
                throw ApiError(response, data.Error, fallbackMessage: null);
        }

        return contents.Count > 0 || finishReason is not null
            ? new ChatResponseUpdate(ChatRole.Assistant, contents) { ResponseId = state.ResponseId, ModelId = state.ModelId, FinishReason = finishReason }
            : null;
    }

    // The caller owns the request under a using declaration, so a throw here cannot leak it.
    private void FillRequest(HttpRequestMessage request, IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream)
    {
        AnthropicRequest body = BuildRequest(messages, options, stream, metadata.DefaultModelId!);
        request.Content = JsonContent.Create(body, AnthropicJsonContext.Default.AnthropicRequest);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
    }

    private string? Redact(string? text)
    {
        // The API does not echo the key today; the replace keeps a future echo out of logs and exception text.
        return text?.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
    }

    /// <summary>A tool call whose argument fragments are still arriving.</summary>
    private sealed record PendingCall(string CallId, string Name, StringBuilder Json);

    /// <summary>What one stream has seen so far.</summary>
    private sealed class StreamState
    {
        // Keyed by content-block index; a call is whole when its block stops.
        public Dictionary<int, PendingCall> Pending { get; } = [];

        public string? ResponseId { get; set; }

        public string? ModelId { get; set; }

        public int? InputTokens { get; set; }

        public bool Stopped { get; set; }
    }
}
