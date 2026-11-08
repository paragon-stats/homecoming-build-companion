using System.Net;

using Microsoft.Extensions.AI;

namespace CohEngine.Ai.Tests;

public sealed class ChatClientFactoryTests
{
    private const string CannedReply = """
        {"id":"c1","object":"chat.completion","created":1,"model":"m","choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"hello back"}}]}
        """;

    private const string StreamReply = """
        data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"m","choices":[{"index":0,"delta":{"role":"assistant","content":"hi there"},"finish_reason":null}]}

        data: [DONE]


        """;

    private const string TestKey = "fake-key-for-tests";

    private static async Task<string> AskAsync(ChatProviderOptions options, FakeHandler handler)
    {
        using IChatClient client = ChatClientFactory.Create(options with { Handler = handler });
        ChatResponse response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")], cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
        return response.Text;
    }

    private static ChatProviderOptions Local(string? baseUrl) =>
        new(ChatProvider.OpenAiCompatible, ChatPreset.Local, "llama3", baseUrl);

    private static T WithKey<T>(string variable, string? value, Func<T> action)
    {
        string? previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, value);
        try
        {
            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public async Task One_request_posts_once_to_the_chat_endpoint_with_model_and_messages()
    {
        FakeHandler handler = new(HttpStatusCode.OK, CannedReply);

        string text = await AskAsync(Local("http://localhost:11434/v1"), handler);

        Assert.Equal("hello back", text);
        (HttpMethod method, Uri? uri, string body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("http://localhost:11434/v1/chat/completions", uri?.ToString());
        Assert.Contains("\"model\":\"llama3\"", body, StringComparison.Ordinal);
        Assert.Contains("hello", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://localhost:8000/v1")]
    [InlineData("http://localhost:8080/v1")]
    [InlineData("http://localhost:8080/v1/")]
    public async Task Local_preset_works_against_ollama_vllm_and_llama_cpp_url_shapes(string baseUrl)
    {
        FakeHandler handler = new(HttpStatusCode.OK, CannedReply);

        string text = await AskAsync(Local(baseUrl), handler);

        Assert.Equal("hello back", text);
        Assert.Equal(baseUrl.TrimEnd('/') + "/chat/completions", Assert.Single(handler.Requests).Uri?.ToString());
    }

    [Fact]
    public async Task Local_preset_defaults_to_the_ollama_address()
    {
        FakeHandler handler = new(HttpStatusCode.OK, CannedReply);

        await AskAsync(Local(null), handler);

        Assert.Equal("http://localhost:11434/v1/chat/completions", Assert.Single(handler.Requests).Uri?.ToString());
    }

    [Fact]
    public async Task Mistral_preset_posts_to_the_mistral_api()
    {
        FakeHandler handler = new(HttpStatusCode.OK, CannedReply);
        ChatProviderOptions options = new(ChatProvider.OpenAiCompatible, ChatPreset.Mistral, "mistral-small-latest");

        await WithKey("MISTRAL_API_KEY", TestKey, () => AskAsync(options, handler));

        Assert.Equal("https://api.mistral.ai/v1/chat/completions", Assert.Single(handler.Requests).Uri?.ToString());
    }

    [Fact]
    public async Task Openai_preset_posts_to_the_openai_api()
    {
        FakeHandler handler = new(HttpStatusCode.OK, CannedReply);
        ChatProviderOptions options = new(ChatProvider.OpenAiCompatible, ChatPreset.OpenAi, "gpt-test");

        await WithKey("OPENAI_API_KEY", TestKey, () => AskAsync(options, handler));

        Assert.Equal("https://api.openai.com/v1/chat/completions", Assert.Single(handler.Requests).Uri?.ToString());
    }

    [Theory]
    [InlineData(ChatPreset.OpenAi, "OPENAI_API_KEY")]
    [InlineData(ChatPreset.Mistral, "MISTRAL_API_KEY")]
    public void A_missing_key_variable_fails_with_a_named_error(ChatPreset preset, string variable)
    {
        ChatProviderOptions options = new(ChatProvider.OpenAiCompatible, preset, "m");

        InvalidOperationException error = WithKey(variable, null, () => Assert.Throws<InvalidOperationException>(() => ChatClientFactory.Create(options)));

        Assert.Equal($"E130: {variable} is not set; export the API key in that variable or pass --api-key-env", error.Message);
    }

    [Fact]
    public void A_named_key_variable_replaces_the_preset_default_and_must_be_set()
    {
        ChatProviderOptions options = Local("http://localhost:8000/v1") with { ApiKeyEnvironmentVariable = "COH_TEST_KEY_VARIABLE" };

        InvalidOperationException error = WithKey("COH_TEST_KEY_VARIABLE", string.Empty, () => Assert.Throws<InvalidOperationException>(() => ChatClientFactory.Create(options)));

        Assert.StartsWith("E130: COH_TEST_KEY_VARIABLE", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_401_surfaces_as_one_typed_exception_without_the_key_or_the_body()
    {
        FakeHandler handler = new(HttpStatusCode.Unauthorized, $$$"""{"error":{"message":"Incorrect API key provided: {{{TestKey}}}","type":"invalid_request_error"}}""");
        ChatProviderOptions options = new(ChatProvider.OpenAiCompatible, ChatPreset.OpenAi, "m");

        ChatProviderException error = await Assert.ThrowsAsync<ChatProviderException>(() => WithKey("OPENAI_API_KEY", TestKey, () => AskAsync(options, handler)));

        Assert.Equal(401, error.Status);
        Assert.Equal("E131: the provider returned HTTP 401; check the key in OPENAI_API_KEY and the base URL", error.Message);
        Assert.DoesNotContain(TestKey, error.ToString(), StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_streaming_401_surfaces_as_the_same_typed_exception()
    {
        FakeHandler handler = new(HttpStatusCode.Unauthorized, $$$"""{"error":{"message":"bad key {{{TestKey}}}"}}""");
        using IChatClient client = ChatClientFactory.Create(Local("http://localhost:8000/v1") with { Handler = handler });

        ChatProviderException error = await Assert.ThrowsAsync<ChatProviderException>(async () =>
        {
            await foreach (ChatResponseUpdate unused in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hello")], cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false))
            {
            }
        });

        Assert.Equal(401, error.Status);
        Assert.DoesNotContain(TestKey, error.ToString(), StringComparison.Ordinal);
        Assert.Contains("(no key variable)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_streaming_reply_yields_the_assistant_text()
    {
        FakeHandler handler = new(HttpStatusCode.OK, StreamReply);
        using IChatClient client = ChatClientFactory.Create(Local("http://localhost:8000/v1") with { Handler = handler });

        string text = string.Empty;
        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hello")], cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false))
        {
            text += update.Text;
        }

        Assert.Equal("hi there", text);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("/v1")]
    [InlineData("ftp://host/v1")]
    public void A_base_url_that_is_not_absolute_http_fails_with_a_named_error(string baseUrl)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ChatClientFactory.Create(Local(baseUrl)));

        Assert.Equal($"E132: base URL '{baseUrl}' is not an absolute http(s) URL; pass --base-url https://host/v1", error.Message);
    }

    [Fact]
    public void An_unknown_provider_or_preset_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatClientFactory.Create(new((ChatProvider)99, ChatPreset.Local, "m")));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatClientFactory.Create(new(ChatProvider.OpenAiCompatible, (ChatPreset)99, "m")));
        Assert.Throws<ArgumentNullException>(() => ChatClientFactory.Create(null!));
    }
}
