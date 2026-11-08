using System.Net;

using CohEngine.Ai.Anthropic;

using Microsoft.Extensions.AI;

namespace CohEngine.Ai.Tests.Anthropic;

public sealed class AnthropicStreamingAndErrorTests
{
    [Fact]
    public async Task A_canned_stream_yields_two_text_updates_then_the_finish_and_usage()
    {
        using FakeAnthropicHandler handler = new(HttpStatusCode.OK, AnthropicFixtures.Read("stream-two-text-deltas.sse"), "text/event-stream");
        using AnthropicChatClient client = AnthropicChatClientTests.NewClient(handler);

        List<ChatResponseUpdate> updates = await Collect(client);

        Assert.Equal(["Hello", ", hero."], updates.Where(update => update.Text.Length > 0).Select(update => update.Text), StringComparer.Ordinal);
        Assert.Contains("\"stream\":true", Assert.Single(handler.Bodies), StringComparison.Ordinal);
        Assert.All(updates, update => Assert.Equal("msg_fixture_text", update.ResponseId));
        ChatResponseUpdate last = updates[^1];
        Assert.Equal(ChatFinishReason.Stop, last.FinishReason);
        UsageContent usage = Assert.IsType<UsageContent>(Assert.Single(last.Contents));
        Assert.Equal(25, usage.Details.InputTokenCount);
        Assert.Equal(5, usage.Details.OutputTokenCount);
        Assert.Equal("Hello, hero.", updates.ToChatResponse().Text);
    }

    [Fact]
    public async Task A_canned_tool_use_stream_yields_one_call_whose_arguments_round_trip_byte_for_byte()
    {
        using FakeAnthropicHandler handler = new(HttpStatusCode.OK, AnthropicFixtures.Read("stream-tool-use.sse"), "text/event-stream");
        using AnthropicChatClient client = AnthropicChatClientTests.NewClient(handler);

        List<ChatResponseUpdate> updates = await Collect(client);

        FunctionCallContent call = Assert.Single(updates.SelectMany(update => update.Contents).OfType<FunctionCallContent>());
        Assert.Equal("toolu_fixture_01", call.CallId);
        Assert.Equal("lookup_power", call.Name);
        Assert.Equal("""{"powerset":"Fire Blast","level":50,"tags":["ranged","aoe"]}""", AnthropicChatClientTests.ArgumentsJson(call));
        Assert.Equal(ChatFinishReason.ToolCalls, updates[^1].FinishReason);
    }

    [Fact]
    public async Task A_tool_block_with_no_argument_fragments_yields_a_call_with_no_arguments()
    {
        const string Stream = """
            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_fixture_02","name":"list_sets","input":{}}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":3,"delta":{"type":"input_json_delta","partial_json":"{}"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: content_block_stop
            data: {"type":"content_block_stop","index":3}
            """;
        using FakeAnthropicHandler handler = new(HttpStatusCode.OK, Stream, "text/event-stream");
        using AnthropicChatClient client = AnthropicChatClientTests.NewClient(handler);

        List<ChatResponseUpdate> updates = await Collect(client);

        FunctionCallContent call = Assert.IsType<FunctionCallContent>(Assert.Single(Assert.Single(updates).Contents));
        Assert.Empty(call.Arguments!);
    }

    [Fact]
    public async Task A_canned_401_maps_to_one_typed_exception_without_the_key()
    {
        using FakeAnthropicHandler handler = new(HttpStatusCode.Unauthorized, AnthropicFixtures.Read("error-401.json")) { RequestId = "req_fixture_01" };
        using AnthropicChatClient client = AnthropicChatClientTests.NewClient(handler);

        AnthropicApiException exception = await Assert.ThrowsAsync<AnthropicApiException>(
            () => client.GetResponseAsync("Hi", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(401, exception.Status);
        Assert.Equal("authentication_error", exception.ErrorType);
        Assert.Equal("req_fixture_01", exception.RequestId);
        Assert.Equal("Anthropic API 401 authentication_error: invalid x-api-key", exception.Message);
        Assert.DoesNotContain(AnthropicChatClientTests.FakeKey, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_overloaded_response_fails_the_stream_before_any_update()
    {
        using FakeAnthropicHandler handler = new((HttpStatusCode)529, AnthropicFixtures.Read("error-overloaded.json"));
        using AnthropicChatClient client = AnthropicChatClientTests.NewClient(handler);

        AnthropicApiException exception = await Assert.ThrowsAsync<AnthropicApiException>(() => Collect(client));

        Assert.Equal(529, exception.Status);
        Assert.Equal("overloaded_error", exception.ErrorType);
        Assert.Null(exception.RequestId);
    }

    [Fact]
    public async Task An_error_event_inside_the_stream_maps_to_the_same_exception()
    {
        string stream = "event: error\ndata: " + AnthropicFixtures.Read("error-overloaded.json");
        using FakeAnthropicHandler handler = new(HttpStatusCode.OK, stream, "text/event-stream");
        using AnthropicChatClient client = AnthropicChatClientTests.NewClient(handler);

        AnthropicApiException exception = await Assert.ThrowsAsync<AnthropicApiException>(() => Collect(client));

        Assert.Equal(200, exception.Status);
        Assert.Equal("overloaded_error", exception.ErrorType);
        Assert.Equal("Anthropic API 200 overloaded_error: Overloaded", exception.Message);
    }

    [Fact]
    public async Task A_non_json_error_body_falls_back_to_the_status_line()
    {
        using FakeAnthropicHandler handler = new(HttpStatusCode.BadGateway, "<html>bad gateway</html>", "text/html");
        using AnthropicChatClient client = AnthropicChatClientTests.NewClient(handler);

        AnthropicApiException exception = await Assert.ThrowsAsync<AnthropicApiException>(
            () => client.GetResponseAsync("Hi", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(502, exception.Status);
        Assert.Equal("unknown_error", exception.ErrorType);
        Assert.Equal("Anthropic API 502 unknown_error: Bad Gateway", exception.Message);
    }

    [Fact]
    public async Task An_error_message_that_echoes_the_key_is_redacted()
    {
        string body = $$$"""{"type":"error","error":{"type":"permission_error","message":"key {{{AnthropicChatClientTests.FakeKey}}} lacks access"}}""";
        using FakeAnthropicHandler handler = new(HttpStatusCode.Forbidden, body);
        using AnthropicChatClient client = AnthropicChatClientTests.NewClient(handler);

        AnthropicApiException exception = await Assert.ThrowsAsync<AnthropicApiException>(
            () => client.GetResponseAsync("Hi", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Anthropic API 403 permission_error: key [redacted] lacks access", exception.Message);
    }

    [Fact]
    public async Task A_null_stream_event_is_refused()
    {
        using FakeAnthropicHandler handler = new(HttpStatusCode.OK, "event: ping\ndata: null\n\n", "text/event-stream");
        using AnthropicChatClient client = AnthropicChatClientTests.NewClient(handler);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => Collect(client));
    }

    [Fact]
    public void The_standard_constructors_carry_an_unknown_error_type()
    {
        InvalidOperationException cause = new("cause");

        Assert.Equal("unknown_error", new AnthropicApiException().ErrorType);
        Assert.Equal("Anthropic API 0 unknown_error: plain", new AnthropicApiException("plain").Message);
        AnthropicApiException wrapped = new("wrapped", cause);
        Assert.Same(cause, wrapped.InnerException);
        Assert.Equal("unknown_error", wrapped.ErrorType);
        Assert.Equal(0, wrapped.Status);
    }

    private static async Task<List<ChatResponseUpdate>> Collect(AnthropicChatClient client)
    {
        List<ChatResponseUpdate> updates = [];
        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync("Hi", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true))
        {
            updates.Add(update);
        }

        return updates;
    }
}
