using CohEngine.Core.Registry;

namespace CohEngine.Core;

/// <summary>
/// The CLI's whole behavior, in Core so it is testable and coverage-measured;
/// Program.cs stays a one-line shim.
/// </summary>
public static class CliRunner
{
    private const string Usage = "usage: coh-engine [--version | --help | rules]";

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

        if (args is ["rules"])
        {
            PrintRules(output);
            return 0;
        }

        error.WriteLine(Usage);
        return 2;
    }

    /// <summary>Lists the registries with the headings and order <c>python -m coh_engine rules</c> prints.</summary>
    private static void PrintRules(TextWriter output)
    {
        (string Heading, IReadOnlyList<RuleInfo> Rules)[] sections =
        [
            ("legality rules (a new dimension is a new @legality_rule):", RuleRegistry.Legality),
            ("hard-limit rules (a new limit is a new @hard_limit_rule):", RuleRegistry.HardLimits),
            ("enhancement aspect handlers (a new aspect is a new @_register):", RuleRegistry.EnhancementAspects),
            ("scoring metrics (a new metric is a new @metric):", RuleRegistry.Metrics),
        ];
        foreach ((string heading, IReadOnlyList<RuleInfo> rules) in sections)
        {
            output.WriteLine(heading);
            foreach (RuleInfo rule in rules)
            {
                output.WriteLine("  " + rule.Id);
            }
        }
    }
}
