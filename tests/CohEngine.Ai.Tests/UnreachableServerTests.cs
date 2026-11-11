using Microsoft.Extensions.AI;

namespace CohEngine.Ai.Tests;

public sealed class UnreachableServerTests
{
    private static IChatClient Create()
    {
        return ChatClientFactory.Create(new ChatProviderOptions(ChatProvider.OpenAiCompatible, ChatPreset.Local, "llama3", "http://localhost:11434/v1", Handler: new ThrowingHandler()));
    }

    [Fact]
    public async Task A_failed_connection_is_a_provider_error_with_its_own_code_not_an_http_status()
    {
        using IChatClient client = Create();

        ChatProviderException error = await Assert.ThrowsAsync<ChatProviderException>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal((0, "E133"), (error.Status, error.Message[..4]));
        Assert.DoesNotContain("HTTP 0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_connection_while_streaming_is_the_same_error()
    {
        using IChatClient client = Create();

        ChatProviderException error = await Assert.ThrowsAsync<ChatProviderException>(async () =>
        {
            await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false))
            {
                Assert.NotNull(update);
            }
        });

        Assert.StartsWith("E133: the provider could not be reached", error.Message, StringComparison.Ordinal);
    }
}
