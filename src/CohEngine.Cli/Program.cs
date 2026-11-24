using CohEngine.Cli.Ask;
using CohEngine.Core;
using CohEngine.Core.BuildFile;
using CohEngine.Mcp;

// Core cannot reference CohEngine.Mcp (Mcp references Core), so the one command that needs it dispatches here.
if (args is ["mcp", ..])
{
    return await McpCommand.RunAsync(args[1..], input: null, output: null, Console.Error, CancellationToken.None).ConfigureAwait(false);
}

MbdBuildSource mbdSource = new(MbdBuildSource.LocateDefaultMidsDirectory(AppContext.BaseDirectory));

// The same reason for ask: it needs the chat clients and the tool catalog, which Core does not reference.
if (args is ["ask", ..])
{
    return await AskCommand.RunAsync(args[1..], Console.Out, Console.Error, mbdSource, handler: null, baseUrlEnvironmentVariable: null, CancellationToken.None).ConfigureAwait(false);
}

return CliRunner.Run(args, Console.Out, Console.Error, mbdSource);
