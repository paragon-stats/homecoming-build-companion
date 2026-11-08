using System.Runtime.CompilerServices;
using System.Text;

namespace CohEngine.Ai.Anthropic;

/// <summary>
/// A hand-written Server-Sent Events reader (no <c>Regex</c>, no reflection) for the Messages API stream.
/// Events are blank-line delimited; comments and fields other than <c>event</c> and <c>data</c> are skipped.
/// </summary>
internal static class SseParser
{
    private const string DefaultEvent = "message";

    /// <summary>Reads <paramref name="stream"/> to its end, yielding each event that carries data.</summary>
    /// <param name="stream">The response body.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The events, in stream order.</returns>
    public static async IAsyncEnumerable<SseEvent> ReadAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using StreamReader reader = new(stream, Encoding.UTF8);
        string eventName = DefaultEvent;
        StringBuilder data = new();
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    yield return new SseEvent(eventName, data.ToString());
                }

                eventName = DefaultEvent;
                data.Clear();
                continue;
            }

            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon == 0)
            {
                continue;
            }

            string field = colon < 0 ? line : line[..colon];
            string value = colon < 0 ? string.Empty : line[(colon + 1)..];
            value = value.StartsWith(' ') ? value[1..] : value;
            switch (field)
            {
                case "event":
                    eventName = value;
                    break;
                case "data":
                    data.Append(data.Length > 0 ? "\n" : string.Empty).Append(value);
                    break;
                default:
                    break;
            }
        }

        // A server that closes without the final blank line still delivered its last event.
        if (data.Length > 0)
        {
            yield return new SseEvent(eventName, data.ToString());
        }
    }
}
