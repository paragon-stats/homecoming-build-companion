using System.Reflection;

using CohEngine.Ai.Anthropic;

using Microsoft.Extensions.AI;

namespace CohEngine.Ai.Tests.Anthropic;

// Each test names its own base URL variable, so none reads or writes ANTHROPIC_BASE_URL and they run in parallel.
public sealed class AnthropicBaseUrlTests
{
    private const string KeyVariable = "COH_TEST_ANTHROPIC_BASE_URL_KEY";

    [Fact]
    public void The_default_variable_is_ANTHROPIC_BASE_URL()
    {
        Assert.Equal("ANTHROPIC_BASE_URL", AnthropicChatClient.BaseUrlEnvironmentVariable);
    }

    [Fact]
    public void The_base_url_option_wins_over_the_environment_variable()
    {
        Assert.Equal(new Uri("https://option.example/"), ProviderUri("COH_TEST_BASE_URL_OPTION", "https://environment.example", "https://option.example"));
    }

    [Fact]
    public void The_environment_variable_replaces_the_default_without_the_option()
    {
        Assert.Equal(new Uri("https://environment.example/"), ProviderUri("COH_TEST_BASE_URL_SET", "https://environment.example", baseUrl: null));
    }

    [Theory]
    [InlineData("COH_TEST_BASE_URL_UNSET", null)]
    [InlineData("COH_TEST_BASE_URL_EMPTY", "")]
    public void Without_option_or_variable_the_assembly_metadata_default_is_used(string variable, string? value)
    {
        Assert.Equal(new Uri("https://api.anthropic.com/"), ProviderUri(variable, value, baseUrl: null));
    }

    [Fact]
    public void A_variable_that_is_not_an_absolute_http_url_names_the_variable_and_its_fix()
    {
        InvalidOperationException error = Create("COH_TEST_BASE_URL_BAD", "not a url");

        Assert.Equal("E132: COH_TEST_BASE_URL_BAD 'not a url' is not an absolute http(s) URL; set COH_TEST_BASE_URL_BAD to https://host or unset it", error.Message);
    }

    [Fact]
    public void A_variable_with_plain_http_to_a_remote_host_names_the_variable_and_its_fix()
    {
        InvalidOperationException error = Create("COH_TEST_BASE_URL_HTTP", "http://gateway.example");

        Assert.Equal("E132: COH_TEST_BASE_URL_HTTP 'http://gateway.example/' is http on a remote host, which would send the key unencrypted; set COH_TEST_BASE_URL_HTTP to an https URL or a loopback address such as http://localhost, or unset it", error.Message);
    }

    [Fact]
    public void A_variable_with_plain_http_to_loopback_is_used()
    {
        Assert.Equal(new Uri("http://localhost:8080/"), ProviderUri("COH_TEST_BASE_URL_LOOPBACK", "http://localhost:8080", baseUrl: null));
    }

    [Fact]
    public void An_option_that_is_not_an_absolute_http_url_names_the_option_and_its_fix()
    {
        InvalidOperationException error = WithVariable("COH_TEST_BASE_URL_UNUSED", value: null, () => Assert.Throws<InvalidOperationException>(() => ChatClientFactory.Create(Options("ftp://example.test", "COH_TEST_BASE_URL_UNUSED"))));

        Assert.Equal("E132: base URL 'ftp://example.test' is not an absolute http(s) URL; pass --base-url https://host", error.Message);
    }

    [Fact]
    public void The_project_file_sets_the_default_as_assembly_metadata()
    {
        Assert.Equal("https://api.anthropic.com", AnthropicChatClient.DefaultBaseUrl(typeof(AnthropicChatClient).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_missing_or_blank_default_fails_with_E143(string? value)
    {
        AssemblyMetadataAttribute[] metadata = value is null ? [new("Other", "https://x.example")] : [new("AnthropicBaseUrl", value)];

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => AnthropicChatClient.DefaultBaseUrl(metadata));

        Assert.Equal("E143: the CohEngine.Ai assembly has no AnthropicBaseUrl metadata, so there is no default base address; rebuild it from its project file or pass --base-url", error.Message);
    }

    private static ChatProviderOptions Options(string? baseUrl, string variable)
    {
        return new ChatProviderOptions(ChatProvider.Anthropic, ChatPreset.OpenAi, "claude-test-model", baseUrl, KeyVariable, BaseUrlEnvironmentVariable: variable);
    }

    private static InvalidOperationException Create(string variable, string value)
    {
        return WithVariable(variable, value, () => Assert.Throws<InvalidOperationException>(() => ChatClientFactory.Create(Options(baseUrl: null, variable))));
    }

    private static Uri? ProviderUri(string variable, string? value, string? baseUrl)
    {
        return WithVariable(variable, value, () =>
        {
            using IChatClient client = ChatClientFactory.Create(Options(baseUrl, variable));
            return Assert.IsType<ChatClientMetadata>(client.GetService(typeof(ChatClientMetadata))).ProviderUri;
        });
    }

    // The key variable is set to the same value by every test, so parallel tests cannot disturb one another.
    private static T WithVariable<T>(string variable, string? value, Func<T> action)
    {
        Environment.SetEnvironmentVariable(KeyVariable, AnthropicChatClientTests.FakeKey);
        Environment.SetEnvironmentVariable(variable, value);
        try
        {
            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, value: null);
        }
    }
}
