namespace CohEngine.Core;

/// <summary>
/// The CLI's whole behavior, in Core so it is testable and coverage-measured;
/// Program.cs stays a one-line shim.
/// </summary>
public static class CliRunner
{
    private const string Usage = "usage: coh-engine [--version | --help]";

    /// <summary>Runs the CLI and returns the process exit code.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="output">Where normal output goes.</param>
    /// <param name="error">Where usage errors go.</param>
    /// <returns>0 on success, 2 on an unrecognised command line.</returns>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args is ["--version"])
        {
            output.WriteLine("coh-engine " + EngineInfo.Version);
            return 0;
        }

        if (args is ["--help"] or ["-h"])
        {
            output.WriteLine(Usage);
            return 0;
        }

        error.WriteLine(Usage);
        return 2;
    }
}
