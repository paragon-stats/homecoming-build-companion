using System.ClientModel;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace CohEngine.Ai;

/// <summary>Turns the SDK's HTTP errors, whose text includes the response body, into <see cref="ChatProviderException"/>.</summary>
internal sealed class ErrorMappingChatClient(IChatClient inner, string? keyVariable) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        }
        catch (ClientResultException error)
        {
            throw Map(error);
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using ConfiguredCancelableAsyncEnumerable<ChatResponseUpdate>.Enumerator updates = base.GetStreamingResponseAsync(messages, options, cancellationToken)
            .WithCancellation(cancellationToken).ConfigureAwait(false).GetAsyncEnumerator();
        while (true)
        {
            bool more;
            try
            {
                more = await updates.MoveNextAsync();
            }
            catch (ClientResultException error)
            {
                throw Map(error);
            }

            if (!more)
            {
                yield break;
            }

            yield return updates.Current;
        }
    }

    // The inner exception is dropped: its message holds the response body, which a server may fill with the key.
    private ChatProviderException Map(ClientResultException error) =>
        new(error.Status, $"{AiErrorCodes.ProviderRejected}: the provider returned HTTP {error.Status}; check the key in {keyVariable ?? "(no key variable)"} and the base URL");
}
