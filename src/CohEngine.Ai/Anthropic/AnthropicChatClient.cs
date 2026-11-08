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
        using HttpRequestMessage request = CreateRequest(messages, options, stream: true);
        using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfFailedAsync(response, cancellationToken).ConfigureAwait(false);
        Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (body.ConfigureAwait(false))
        {
            // Tool-call arguments arrive as JSON fragments per block index; the call is whole when its block stops.
            Dictionary<int, (string CallId, string Name, StringBuilder Json)> pending = [];
            string? responseId = null;
            string? modelId = null;
            int inputTokens = 0;
            await foreach (SseEvent sse in SseParser.ReadAsync(body, cancellationToken).ConfigureAwait(false))
            {
                AnthropicStreamEvent data = JsonSerializer.Deserialize(sse.Data, AnthropicJsonContext.Default.AnthropicStreamEvent)
                    ?? throw new JsonException($"The Messages API sent a null {sse.Event} event.");
                List<AIContent> contents = [];
                ChatFinishReason? finishReason = null;
                switch (sse.Event)
                {
                    case "message_start":
                        responseId = data.Message?.MessageId;
                        modelId = data.Message?.Model;
                        inputTokens = data.Message?.Usage?.InputTokens ?? 0;
                        break;
                    case "content_block_start" when data.ContentBlock is { Type: "tool_use" } block:
                        pending[data.Index] = (block.CallId ?? string.Empty, block.Name ?? string.Empty, new StringBuilder());
                        break;
                    case "content_block_delta" when data.Delta is { Type: "text_delta", Text: { } text }:
                        contents.Add(new TextContent(text));
                        break;
                    case "content_block_delta" when data.Delta is { Type: "input_json_delta" } delta && pending.TryGetValue(data.Index, out var call):
                        call.Json.Append(delta.PartialJson);
                        break;
                    case "content_block_stop" when pending.Remove(data.Index, out var call):
                        contents.Add(ToCall(call.CallId, call.Name, ParseArguments(call.Json)));
                        break;
                    case "message_delta":
                        finishReason = ToFinishReason(data.Delta?.StopReason);
                        contents.Add(new UsageContent(ToUsage(inputTokens, data.Usage?.OutputTokens ?? 0)));
                        break;
                    case "error":
                        throw ApiError(response, data.Error, fallbackMessage: null);
                    default: // ping, message_stop, thinking deltas: nothing the caller needs.
                        break;
                }

                if (contents.Count > 0 || finishReason is not null)
                {
                    yield return new ChatResponseUpdate(ChatRole.Assistant, contents)
                    {
                        ResponseId = responseId,
                        ModelId = modelId,
                        FinishReason = finishReason,
                    };
                }
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

    private async Task ThrowIfFailedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        AnthropicError? error = null;
        try
        {
            error = JsonSerializer.Deserialize(text, AnthropicJsonContext.Default.AnthropicErrorEnvelope)?.Error;
        }
        catch (JsonException)
        {
            // A proxy's HTML error page: fall back to the status line below.
        }

        throw ApiError(response, error, response.ReasonPhrase);
    }

    private AnthropicApiException ApiError(HttpResponseMessage response, AnthropicError? error, string? fallbackMessage)
    {
        // The API does not echo the key today; the replace keeps a future echo out of logs and exception text.
        string message = (error?.Message ?? fallbackMessage ?? string.Empty).Replace(apiKey, "[redacted]", StringComparison.Ordinal);
        return new AnthropicApiException((int)response.StatusCode, error?.Type ?? "unknown_error", message, RequestIdOf(response));
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
