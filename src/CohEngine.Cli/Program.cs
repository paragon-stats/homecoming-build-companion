using CohEngine.Core;
using CohEngine.Core.BuildFile;
using CohEngine.Mcp;

// Core cannot reference CohEngine.Mcp (Mcp references Core), so the one command that needs it dispatches here.
if (args is ["mcp", ..])
{
    return await McpCommand.RunAsync(args[1..], null, null, Console.Error, CancellationToken.None).ConfigureAwait(false);
}

return CliRunner.Run(args, Console.Out, Console.Error, new MbdBuildSource(MbdBuildSource.LocateDefaultMidsDirectory(AppContext.BaseDirectory)));
