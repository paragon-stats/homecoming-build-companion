using System.Reflection;

using CohEngine.Ai.Anthropic;

using Microsoft.Extensions.AI;

namespace CohEngine.Ai.Tests.Anthropic;

// Every test here that reads ANTHROPIC_BASE_URL sits in this one class, so xUnit runs them one at a time.
public sealed class AnthropicBaseUrlTests
{
    private const string KeyVariable = "COH_TEST_ANTHROPIC_BASE_URL_KEY";

    [Fact]
    public void The_base_url_option_wins_over_the_environment_variable()
    {
        Assert.Equal(new Uri("https://option.example/"), ProviderUri("https://option.example", "https://environment.example"));
    }

    [Fact]
    public void The_environment_variable_replaces_the_default_without_the_option()
    {
        Assert.Equal(new Uri("https://environment.example/"), ProviderUri(baseUrl: null, "https://environment.example"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Without_option_or_variable_the_assembly_metadata_default_is_used(string? environment)
    {
        Assert.Equal(new Uri("https://api.anthropic.com/"), ProviderUri(baseUrl: null, environment));
    }

    [Fact]
    public void A_variable_that_is_not_an_absolute_http_url_fails_with_E132()
    {
        InvalidOperationException error = WithVariables("not a url", () => Assert.Throws<InvalidOperationException>(() => ChatClientFactory.Create(Options(baseUrl: null))));

        Assert.StartsWith("E132: base URL 'not a url'", error.Message, StringComparison.Ordinal);
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

        Assert.StartsWith("E143: the CohEngine.Ai assembly has no AnthropicBaseUrl metadata", error.Message, StringComparison.Ordinal);
    }

    private static ChatProviderOptions Options(string? baseUrl)
    {
        return new ChatProviderOptions(ChatProvider.Anthropic, ChatPreset.OpenAi, "claude-test-model", baseUrl, KeyVariable);
    }

    private static Uri? ProviderUri(string? baseUrl, string? environment)
    {
        return WithVariables(environment, () =>
        {
            using IChatClient client = ChatClientFactory.Create(Options(baseUrl));
            return Assert.IsType<ChatClientMetadata>(client.GetService(typeof(ChatClientMetadata))).ProviderUri;
        });
    }

    private static T WithVariables<T>(string? baseUrl, Func<T> action)
    {
        string? previousBase = Environment.GetEnvironmentVariable(AnthropicChatClient.BaseUrlEnvironmentVariable);
        string? previousKey = Environment.GetEnvironmentVariable(KeyVariable);
        Environment.SetEnvironmentVariable(AnthropicChatClient.BaseUrlEnvironmentVariable, baseUrl);
        Environment.SetEnvironmentVariable(KeyVariable, AnthropicChatClientTests.FakeKey);
        try
        {
            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(AnthropicChatClient.BaseUrlEnvironmentVariable, previousBase);
            Environment.SetEnvironmentVariable(KeyVariable, previousKey);
        }
    }
}
