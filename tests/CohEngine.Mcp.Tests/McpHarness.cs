using System.IO.Pipes;
using System.Text.Json.Nodes;

using CohEngine.TestSupport;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CohEngine.Mcp.Tests;

/// <summary>The shared fixture store and an in-process client wired to the server over a pair of pipes.</summary>
internal sealed class McpHarness : IAsyncDisposable
{
    private readonly AnonymousPipeServerStream requests;
    private readonly AnonymousPipeServerStream responses;
    private readonly AnonymousPipeClientStream serverReads;
    private readonly AnonymousPipeClientStream serverWrites;
    private readonly Task serverRun;
    private McpClient? client;

    private McpHarness()
    {
        requests = new(PipeDirection.Out);
        responses = new(PipeDirection.In);
        serverReads = new(PipeDirection.In, requests.ClientSafePipeHandle);
        serverWrites = new(PipeDirection.Out, responses.ClientSafePipeHandle);
        serverRun = McpCommand.RunAsync([], serverReads, serverWrites, TextWriter.Null, CancellationToken.None);
    }

    public static FixtureStore Store { get; } = FixtureStore.Locate(AppContext.BaseDirectory);

    public McpClient Client => client ?? throw new InvalidOperationException("the harness has not connected");

    public static async Task<McpHarness> StartAsync()
    {
        McpHarness harness = new();
        bool connected = false;
        try
        {
            harness.client = await McpClient.CreateAsync(new StreamClientTransport(harness.requests, harness.responses), cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
            connected = true;
            return harness;
        }
        finally
        {
            if (!connected)
            {
                await harness.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public static string BuildDir(string name)
    {
        string parity = Path.Join(Store.MidsDirectory, "builds", name);
        return Directory.Exists(parity) ? parity : Path.Join(Store.MidsDirectory, "regressions", name);
    }

    public async Task<CallToolResult> CallAsync(string tool, params (string Name, object Value)[] arguments)
    {
        Dictionary<string, object?> named = arguments.ToDictionary(item => item.Name, object? (item) => item.Value, StringComparer.Ordinal);
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
        try
        {
            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await requests.DisposeAsync().ConfigureAwait(false);
            await responses.DisposeAsync().ConfigureAwait(false);
            await serverReads.DisposeAsync().ConfigureAwait(false);
            await serverWrites.DisposeAsync().ConfigureAwait(false);
        }

        // The pipes closed under the server, which is how a client ends the session; how it ends is not under test here.
        await Task.WhenAny(serverRun).ConfigureAwait(false);
    }

    private static string TextOf(CallToolResult result)
    {
        return result.Content.OfType<TextContentBlock>().Single().Text;
    }
}
