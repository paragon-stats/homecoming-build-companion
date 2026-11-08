using Microsoft.Extensions.AI;

namespace CohEngine.Ai;

/// <summary>Builds the chat client for a provider and preset.</summary>
public static class ChatClientFactory
{
    /// <summary>Creates the client; the API key comes from the environment variable only.</summary>
    /// <param name="options">Provider, preset and overrides.</param>
    /// <returns>A chat client that sends to the chosen server.</returns>
    public static IChatClient Create(ChatProviderOptions options) => throw new NotSupportedException();
}
