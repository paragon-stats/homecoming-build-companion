using System.Text;

using CohEngine.Ai.Anthropic;

namespace CohEngine.Ai.Tests.Anthropic;

public sealed class SseParserTests
{
    [Fact]
    public async Task A_canned_stream_yields_its_two_text_deltas_in_order()
    {
        await using FileStream stream = File.OpenRead(AnthropicFixtures.PathOf("stream-two-text-deltas.sse"));

        List<SseEvent> events = await Collect(stream);

        List<SseEvent> deltas = [.. events.Where(sse => string.Equals(sse.Event, "content_block_delta", StringComparison.Ordinal))];
        Assert.Equal(2, deltas.Count);
        Assert.Contains("\"text\":\"Hello\"", deltas[0].Data, StringComparison.Ordinal);
        Assert.Contains("\"text\":\", hero.\"", deltas[1].Data, StringComparison.Ordinal);
        Assert.Equal(
            ["message_start", "content_block_start", "ping", "content_block_delta", "content_block_delta", "content_block_stop", "message_delta", "message_stop"],
            events.Select(sse => sse.Event),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task Comments_unknown_fields_and_multi_line_data_follow_the_event_stream_rules()
    {
        const string Text = ": keep-alive\r\nid: 7\r\ndata:first\r\ndata: second\r\nretry\r\n\r\n\r\nevent: done\ndata: last";
        await using MemoryStream stream = new(Encoding.UTF8.GetBytes(Text));

        List<SseEvent> events = await Collect(stream);

        Assert.Equal([new SseEvent("message", "first\nsecond"), new SseEvent("done", "last")], events);
    }

    private static async Task<List<SseEvent>> Collect(Stream stream)
    {
        List<SseEvent> events = [];
        await foreach (SseEvent sse in SseParser.ReadAsync(stream, TestContext.Current.CancellationToken).ConfigureAwait(true))
        {
            events.Add(sse);
        }

        return events;
    }
}
