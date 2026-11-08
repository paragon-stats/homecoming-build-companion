using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using CohEngine.Ai.Anthropic;

using Microsoft.Extensions.AI;

namespace CohEngine.Ai.Tests.Anthropic;

public sealed class AnthropicChatClientTests
{
    private const string Model = "claude-test-model";
    private const string FakeKey = "fake-key-for-tests-only";

    [Fact]
    public void A_missing_key_variable_fails_with_E140_and_names_the_variable()
    {
        string variable = NewVariable(value: null);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new AnthropicChatClient(Model, apiKeyEnvironmentVariable: variable));

        Assert.StartsWith("E140: " + variable + " is not set", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_text_response_posts_to_the_messages_endpoint_with_the_key_and_version_headers()
    {
        using FakeAnthropicHandler handler = new(HttpStatusCode.OK, AnthropicFixtures.Read("response-text.json"));
        using AnthropicChatClient client = NewClient(handler, baseUrl: "https://gateway.example/anthropic/");

        ChatResponse response = await client.GetResponseAsync("Tell me about Fire Blast.", cancellationToken: TestContext.Current.CancellationToken);

        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://gateway.example/anthropic/v1/messages"), request.RequestUri);
        Assert.Equal(FakeKey, Assert.Single(request.Headers.GetValues("x-api-key")));
        Assert.Equal("2023-06-01", Assert.Single(request.Headers.GetValues("anthropic-version")));
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("Fire Blast is a ranged damage set.", response.Text);
        Assert.Equal("msg_fixture_text", response.ResponseId);
        Assert.Equal(Model, response.ModelId);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal(25, response.Usage!.InputTokenCount);
        Assert.Equal(9, response.Usage.OutputTokenCount);
        Assert.Equal(34, response.Usage.TotalTokenCount);
    }

    [Fact]
    public async Task A_tool_use_response_maps_to_one_call_with_its_arguments_intact()
    {
        string fixture = AnthropicFixtures.Read("response-tool-use.json");
        using FakeAnthropicHandler handler = new(HttpStatusCode.OK, fixture);
        using AnthropicChatClient client = NewClient(handler);

        ChatResponse response = await client.GetResponseAsync("Look up Fire Blast.", cancellationToken: TestContext.Current.CancellationToken);

        FunctionCallContent call = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
        Assert.Equal("toolu_fixture_01", call.CallId);
        Assert.Equal("lookup_power", call.Name);
        using JsonDocument document = JsonDocument.Parse(fixture);
        string expected = document.RootElement.GetProperty("content")[1].GetProperty("input").GetRawText();
        Assert.Equal(expected, ArgumentsJson(call));
        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
        Assert.Equal("Looking it up.", response.Text);
    }

    [Fact]
    public async Task A_system_user_and_tool_declaration_conversation_sends_the_golden_body()
    {
        using FakeAnthropicHandler handler = new(HttpStatusCode.OK, AnthropicFixtures.Read("response-text.json"));
        using AnthropicChatClient client = NewClient(handler);
        ChatMessage[] messages =
        [
            new(ChatRole.System, "You answer City of Heroes build questions."),
            new(ChatRole.User, "What does Fire Blast do?"),
        ];
        ChatOptions options = new() { Temperature = 0.5f, Tools = [LookupPower()], ToolMode = ChatToolMode.Auto };

        await client.GetResponseAsync(messages, options, TestContext.Current.CancellationToken);

        JsonNode? sent = JsonNode.Parse(Assert.Single(handler.Bodies));
        JsonNode? golden = JsonNode.Parse(AnthropicFixtures.Read("request-system-user-tool.json"));
        Assert.True(JsonNode.DeepEquals(golden, sent), sent?.ToJsonString());
    }

    [Fact]
    public void A_tool_round_trip_maps_calls_and_results_to_blocks_and_instructions_join_the_system_prompt()
    {
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal) { ["powerset"] = "Fire Blast", ["level"] = 50 };
        ChatMessage[] messages =
        [
            new(ChatRole.System, "Be brief."),
            new(ChatRole.User, "Look up Fire Blast."),
            new(ChatRole.Assistant, [new FunctionCallContent("toolu_fixture_01", "lookup_power", arguments)]),
            new(ChatRole.Tool, [new FunctionResultContent("toolu_fixture_01", "Ranged damage."), new FunctionResultContent("toolu_fixture_02", 42)]),
            new(ChatRole.Assistant, [new FunctionCallContent("toolu_fixture_03", "list_sets")]),
        ];
        ChatOptions options = new() { Instructions = "You answer build questions.", ModelId = "claude-other-model", MaxOutputTokens = 64 };

        JsonNode body = Serialize(AnthropicChatClient.BuildRequest(messages, options, stream: true, Model));

        Assert.Equal("claude-other-model", (string?)body["model"]);
        Assert.Equal(64, (int?)body["max_tokens"]);
        Assert.True((bool?)body["stream"]);
        Assert.Equal("You answer build questions.\n\nBe brief.", (string?)body["system"]);
        Assert.Null(body["tools"]);
        Assert.Null(body["tool_choice"]);
        JsonArray turns = body["messages"]!.AsArray();
        Assert.Equal(["user", "assistant", "user", "assistant"], turns.Select(turn => (string?)turn!["role"]), StringComparer.Ordinal);
        Assert.Equal("""{"type":"tool_use","id":"toolu_fixture_01","name":"lookup_power","input":{"powerset":"Fire Blast","level":50}}""", turns[1]!["content"]![0]!.ToJsonString());
        Assert.Equal("""{"type":"tool_result","tool_use_id":"toolu_fixture_01","content":"Ranged damage."}""", turns[2]!["content"]![0]!.ToJsonString());
        Assert.Equal("42", (string?)turns[2]!["content"]![1]!["content"]);
        Assert.Equal("""{}""", turns[3]!["content"]![0]!["input"]!.ToJsonString());
    }

    [Theory]
    [InlineData("none", null)]
    [InlineData("any", null)]
    [InlineData("tool", "lookup_power")]
    public void Tool_modes_map_to_the_api_tool_choice(string type, string? name)
    {
        ChatToolMode mode = type switch
        {
            "none" => ChatToolMode.None,
            "any" => ChatToolMode.RequireAny,
            _ => ChatToolMode.RequireSpecific(name!),
        };
        ChatOptions options = new() { Tools = [LookupPower()], ToolMode = mode };

        JsonNode body = Serialize(AnthropicChatClient.BuildRequest([new ChatMessage(ChatRole.User, "Hi")], options, stream: false, Model));

        Assert.Equal(type, (string?)body["tool_choice"]!["type"]);
        Assert.Equal(name, (string?)body["tool_choice"]!["name"]);
        Assert.Null(body["stream"]);
    }

    [Fact]
    public void Content_and_tools_the_api_cannot_take_are_refused()
    {
        ChatMessage image = new(ChatRole.User, [new DataContent(new byte[] { 1 }, "image/png")]);
        ChatOptions withHostedTool = new() { Tools = [new HostedWebSearchTool()] };

        Assert.Throws<NotSupportedException>(() => AnthropicChatClient.BuildRequest([image], null, stream: false, Model));
        Assert.Throws<NotSupportedException>(() => AnthropicChatClient.BuildRequest([new ChatMessage(ChatRole.User, "Hi")], withHostedTool, stream: false, Model));
        Assert.Throws<ArgumentNullException>(() => AnthropicChatClient.BuildRequest(null!, null, stream: false, Model));
    }

    [Theory]
    [InlineData("max_tokens", "length")]
    [InlineData("stop_sequence", "stop")]
    [InlineData("refusal", "content_filter")]
    [InlineData("pause_turn", "pause_turn")]
    public async Task Stop_reasons_map_to_finish_reasons(string stopReason, string finishReason)
    {
        string body = $$"""{"id":"msg_fixture_text","model":"{{Model}}","content":[{"type":"thinking","thinking":"hidden"}],"stop_reason":"{{stopReason}}"}""";
        using FakeAnthropicHandler handler = new(HttpStatusCode.OK, body);
        using AnthropicChatClient client = NewClient(handler);

        ChatResponse response = await client.GetResponseAsync("Hi", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new ChatFinishReason(finishReason), response.FinishReason);
        Assert.Empty(response.Messages[0].Contents);
        Assert.Null(response.Usage);
    }

    [Fact]
    public async Task A_null_body_is_refused()
    {
        using FakeAnthropicHandler handler = new(HttpStatusCode.OK, "null");
        using AnthropicChatClient client = NewClient(handler);

        await Assert.ThrowsAsync<JsonException>(() => client.GetResponseAsync("Hi", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetService_returns_the_metadata_and_the_client_itself()
    {
        using FakeAnthropicHandler handler = new(HttpStatusCode.OK, "{}");
        using AnthropicChatClient client = NewClient(handler);

        ChatClientMetadata metadata = Assert.IsType<ChatClientMetadata>(client.GetService(typeof(ChatClientMetadata)));
        Assert.Equal("anthropic", metadata.ProviderName);
        Assert.Equal(new Uri("https://api.anthropic.com"), metadata.ProviderUri);
        Assert.Equal(Model, metadata.DefaultModelId);
        Assert.Same(client, client.GetService(typeof(IChatClient)));
        Assert.Null(client.GetService(typeof(string)));
        Assert.Null(client.GetService(typeof(IChatClient), "keyed"));
        Assert.Throws<ArgumentNullException>(() => client.GetService(null!));
    }

    [Fact]
    public void A_client_without_a_handler_uses_the_shared_connection_pool_and_disposes_cleanly()
    {
        string variable = NewVariable(FakeKey);
        AnthropicChatClient client = new(Model, apiKeyEnvironmentVariable: variable);

        client.Dispose();
        client.Dispose();

        Assert.Same(client, client.GetService(typeof(AnthropicChatClient)));
    }

    internal static AnthropicChatClient NewClient(HttpMessageHandler handler, string? baseUrl = null)
    {
        return new AnthropicChatClient(Model, baseUrl, NewVariable(FakeKey), handler);
    }

    internal static string ArgumentsJson(FunctionCallContent call)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, object?> argument in call.Arguments!)
            {
                writer.WritePropertyName(argument.Key);
                ((JsonElement)argument.Value!).WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string NewVariable(string? value)
    {
        string variable = "COH_TEST_ANTHROPIC_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, value);
        return variable;
    }

    private static AIFunctionDeclaration LookupPower()
    {
        using JsonDocument schema = JsonDocument.Parse("""{"type":"object","properties":{"powerset":{"type":"string"}},"required":["powerset"]}""");
        return AIFunctionFactory.CreateDeclaration("lookup_power", "Looks up one power.", schema.RootElement.Clone());
    }

    private static JsonNode Serialize(AnthropicRequest request)
    {
        return JsonNode.Parse(JsonSerializer.Serialize(request, AnthropicJsonContext.Default.AnthropicRequest))!;
    }
}
