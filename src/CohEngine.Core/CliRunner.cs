using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Registry;

namespace CohEngine.Core;

/// <summary>
/// The CLI's whole behavior, in Core so it is testable and coverage-measured;
/// Program.cs stays a one-line shim.
/// </summary>
public static class CliRunner
{
    private const string Usage = "usage: coh-engine [--version | --help | rules | validate <build> [--mids-dir DIR] [--format text|json]]";

    /// <summary>Runs the CLI and returns the process exit code.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="output">Where normal output goes.</param>
    /// <param name="error">Where usage errors go.</param>
    /// <param name="mbdSource">Resolves a <c>.mbd</c> file for <c>validate</c>; null reports the <c>.mbd</c> form as unavailable.</param>
    /// <returns>The exit code: for <c>validate</c>, 1 on an error finding or bad input, 2 on warnings only; 2 on an unrecognised command line.</returns>
    public static int Run(string[] args, TextWriter output, TextWriter error, IMbdBuildSource? mbdSource = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        switch (args)
        {
            case []:
                output.WriteLine("coh_engine: MidsReborn build-math port.");
                output.WriteLine("Spec: docs/engine/mids-port-spec.md. Commands: rules, validate, chain (score lands with the build-eval pipeline).");
                return 0;
            case ["--version"]:
                output.WriteLine("coh-engine " + EngineInfo.Version);
                return 0;
            case ["--help"] or ["-h"]:
                output.WriteLine(Usage);
                return 0;
            case ["rules"]:
                PrintRules(output);
                return 0;
            case ["chain", ..]:
                return ChainCommand.Run(args[1..], output, error);
            case ["validate", ..]:
                return RunValidate(args, output, error, mbdSource);
            default:
                error.WriteLine(Usage);
                return 2;
        }
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

    private static int RunValidate(string[] args, TextWriter output, TextWriter error, IMbdBuildSource? mbdSource)
    {
        if (!ValidateArguments.TryParse(args[1..], out ValidateArguments? parsed, out string problem))
        {
            error.WriteLine(Usage);
            error.WriteLine("coh-engine validate: error: " + problem);
            return 2;
        }

        return Guarded(
            () =>
            {
                IReadOnlyList<Diagnostic> diagnostics = Validate(parsed, mbdSource);
                output.WriteLine(parsed.Json ? DiagnosticFormatter.FormatJson(diagnostics) : diagnostics.Count > 0 ? DiagnosticFormatter.FormatText(diagnostics) : "no violations");
                return DiagnosticFormatter.ExitCode(diagnostics);
            },
            error);
    }

    private static IReadOnlyList<Diagnostic> Validate(ValidateArguments parsed, IMbdBuildSource? mbdSource)
    {
        if (!string.Equals(Path.GetExtension(parsed.BuildPath), ".mbd", StringComparison.Ordinal))
        {
            string full = Path.GetFullPath(parsed.BuildPath.TrimEnd('/', '\\'));
            string defaultMids = Path.GetDirectoryName(Path.GetDirectoryName(full)) ?? string.Empty;
            return ValidateCommand.RunDump(parsed.BuildPath, parsed.MidsDirectory ?? defaultMids);
        }

        return mbdSource is null
            ? throw new EngineException($"E72: cannot read {parsed.BuildPath}: .mbd validation is not available in this build; validate a Mids dump directory instead")
            : ValidateCommand.RunMbd(mbdSource, parsed.BuildPath, parsed.MidsDirectory ?? mbdSource.DefaultMidsDirectory);
    }

    /// <summary>
    /// Runs a subcommand; an input error prints one <c>error:</c> line on stderr and exits 1, with no stack trace. An
    /// exception already carrying its <c>E&lt;NN&gt;</c> message is printed as is; a missing or unreadable file, or a
    /// dump whose shape is wrong, is E72.
    /// </summary>
    private static int Guarded(Func<int> run, TextWriter error)
    {
        string message;
        try
        {
            return run();
        }
        catch (Exception exception) when (exception is EngineException or ArgumentException or InvalidDataException)
        {
            message = exception is ArgumentException { ParamName: { } name }
                ? exception.Message.Replace($" (Parameter '{name}')", string.Empty, StringComparison.Ordinal)
                : exception.Message;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            string where = exception is FileNotFoundException { FileName: { } file } ? file : "an input file";
            string reason = exception is FileNotFoundException or DirectoryNotFoundException ? "No such file or directory" : exception.Message;
            message = $"E72: cannot read {where}: {reason}; check that the path exists and is readable";
        }
        catch (Exception exception) when (exception is KeyNotFoundException or JsonException)
        {
            message = $"E72: an input is missing a key or holds the wrong shape ({exception.Message}); re-dump the build or check the data matches its schema";
        }

        error.WriteLine("error: " + message);
        return 1;
    }
}
