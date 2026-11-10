namespace CohEngine.Mcp;

/// <summary>The <c>coh-engine mcp</c> command: serves the engine over MCP until the client disconnects.</summary>
public static class McpCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The arguments after <c>mcp</c>; none are accepted.</param>
    /// <param name="input">The stream to read requests from; null reads standard input.</param>
    /// <param name="output">The stream to write responses to; null writes standard output.</param>
    /// <param name="error">Where a usage error goes.</param>
    /// <param name="cancellationToken">Stops the server.</param>
    /// <returns>0 when the client disconnected, 2 on a bad command line.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, Stream? input, Stream? output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(error);
        if (args.Count != 0)
        {
            error.WriteLine("usage: coh-engine mcp");
            error.WriteLine("coh-engine mcp: error: unrecognized arguments: " + string.Join(' ', args));
            return 2;
        }

        await EngineMcpServer.RunAsync(input, output, cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
