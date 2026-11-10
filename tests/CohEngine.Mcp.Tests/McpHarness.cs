using System.IO.Pipes;
using System.Text.Json.Nodes;

using CohEngine.TestSupport;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CohEngine.Mcp.Tests;

/// <summary>The shared fixture store and an in-process client wired to the server over a pair of pipes.</summary>
internal sealed class McpHarness : IAsyncDisposable
{
    private readonly Task serverRun;
    private readonly IDisposable[] streams;

    private McpHarness(McpClient client, Task serverRun, IDisposable[] streams)
    {
        Client = client;
        this.serverRun = serverRun;
        this.streams = streams;
    }

    public static FixtureStore Store { get; } = FixtureStore.Locate(AppContext.BaseDirectory);

    public McpClient Client { get; }

    public static async Task<McpHarness> StartAsync()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        AnonymousPipeServerStream requests = new(PipeDirection.Out);
        AnonymousPipeServerStream responses = new(PipeDirection.In);
        AnonymousPipeClientStream serverReads = new(PipeDirection.In, requests.ClientSafePipeHandle);
        AnonymousPipeClientStream serverWrites = new(PipeDirection.Out, responses.ClientSafePipeHandle);
        Task running = McpCommand.RunAsync([], serverReads, serverWrites, TextWriter.Null, CancellationToken.None);
        McpClient client = await McpClient.CreateAsync(new StreamClientTransport(requests, responses), cancellationToken: cancellation).ConfigureAwait(false);
        return new McpHarness(client, running, [requests, responses, serverReads, serverWrites]);
    }

    public static string BuildDir(string name)
    {
        return Path.Join(Store.MidsDirectory, "builds", name);
    }

    public async Task<CallToolResult> CallAsync(string tool, params (string Name, object Value)[] arguments)
    {
        Dictionary<string, object?> named = arguments.ToDictionary(item => item.Name, item => (object?)item.Value, StringComparer.Ordinal);
        return await Client.CallToolAsync(tool, named, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonNode> CallJsonAsync(string tool, params (string Name, object Value)[] arguments)
    {
        CallToolResult result = await CallAsync(tool, arguments).ConfigureAwait(false);
        Assert.NotEqual(true, result.IsError);
        return JsonNode.Parse(TextOf(result)) ?? throw new InvalidDataException("the tool returned JSON null");
    }

    public async Task<string> CallErrorAsync(string tool, params (string Name, object Value)[] arguments)
    {
        CallToolResult result = await CallAsync(tool, arguments).ConfigureAwait(false);
        Assert.True(result.IsError);

        // The SDK prefixes the tool's own message with the tool name; the message is what the engine wrote.
        string prefix = $"An error occurred invoking '{tool}': ";
        string text = TextOf(result);
        Assert.StartsWith(prefix, text, StringComparison.Ordinal);
        return text[prefix.Length..];
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync().ConfigureAwait(false);
        foreach (IDisposable stream in streams)
        {
            stream.Dispose();
        }

        try
        {
            await serverRun.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // The pipes closed under the server: that is how a client ends the session.
        }
    }

    private static string TextOf(CallToolResult result)
    {
        return result.Content.OfType<TextContentBlock>().Single().Text;
    }
}
