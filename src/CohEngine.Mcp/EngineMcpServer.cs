using CohEngine.Core;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Server;

namespace CohEngine.Mcp;

/// <summary>Runs the engine as a Model Context Protocol server.</summary>
public static class EngineMcpServer
{
    /// <summary>Serves the engine's tools until the client closes the connection.</summary>
    /// <param name="input">The stream to read requests from; null reads standard input.</param>
    /// <param name="output">The stream to write responses to; null writes standard output.</param>
    /// <param name="cancellationToken">Stops the server.</param>
    /// <returns>A task that completes when the connection closes.</returns>
    public static async Task RunAsync(Stream? input, Stream? output, CancellationToken cancellationToken)
    {
        ServiceCollection services = new();

        // Tools are registered by type with source-generated JSON metadata: nothing is discovered by reflection.
        IMcpServerBuilder builder = services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "coh-engine", Version = EngineInfo.Version })
            .WithTools<EngineTools>(McpJsonContext.Default.Options);
        _ = input is null || output is null ? builder.WithStdioServerTransport() : builder.WithStreamServerTransport(input, output);
        ServiceProvider provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            await provider.GetRequiredService<McpServer>().RunAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
