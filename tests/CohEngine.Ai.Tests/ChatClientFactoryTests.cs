using System.ClientModel;
using System.Net;

using Microsoft.Extensions.AI;

namespace CohEngine.Ai.Tests;

public sealed class ChatClientFactoryTests
{
    private const string CannedReply = """
        {"id":"c1","object":"chat.completion","created":1,"model":"m","choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"hello back"}}]}
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
    public async Task A_401_surfaces_as_one_typed_exception_without_the_key()
    {
        FakeHandler handler = new(HttpStatusCode.Unauthorized, """{"error":{"message":"Incorrect API key provided","type":"invalid_request_error"}}""");
        ChatProviderOptions options = new(ChatProvider.OpenAiCompatible, ChatPreset.OpenAi, "m");

        ClientResultException error = await Assert.ThrowsAsync<ClientResultException>(() => WithKey("OPENAI_API_KEY", TestKey, () => AskAsync(options, handler)));

        Assert.Equal(401, error.Status);
        Assert.DoesNotContain(TestKey, error.ToString(), StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void An_unknown_provider_or_preset_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatClientFactory.Create(new((ChatProvider)99, ChatPreset.Local, "m")));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatClientFactory.Create(new(ChatProvider.OpenAiCompatible, (ChatPreset)99, "m")));
        Assert.Throws<ArgumentNullException>(() => ChatClientFactory.Create(null!));
    }
}
