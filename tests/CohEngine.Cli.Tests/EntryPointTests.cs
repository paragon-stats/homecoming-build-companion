using System.Reflection;

using CohEngine.Cli.Ask;

namespace CohEngine.Cli.Tests;

/// <summary>Runs the published program's real entry point, so the dispatch in Program.cs is measured, not excluded.</summary>
public sealed class EntryPointTests
{
    [Theory]
    [InlineData(0, "rules")]
    [InlineData(2, "mcp", "--unknown")]
    [InlineData(2, "ask")]
    public void The_entry_point_dispatches_each_command_to_its_runner(int expected, params string[] args)
    {
        MethodInfo entry = typeof(AskCommand).Assembly.EntryPoint!;

        object? exit = entry.Invoke(null, [args]);

        Assert.Equal(expected, exit);
    }
}
