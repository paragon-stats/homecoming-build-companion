using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.PatchNotes;
using CohEngine.Core.Registry;
using CohEngine.Core.SourceMaterial;

namespace CohEngine.Core;

/// <summary>
/// The CLI's whole behavior, in Core so it is testable and coverage-measured;
/// Program.cs stays a one-line shim.
/// </summary>
public static class CliRunner
{
    private const string Usage = "usage: coh-engine [--version | --help | rules | mcp | chain BUILD_DIR [options] | score BUILD_DIR [--mids-dir DIR] [--attribmod PATH] [--maths PATH] | ask --build BUILD --model NAME [--provider anthropic|openai|mistral|local] [--base-url URL] [--api-key-env NAME] [--] <question> | extract-canonical [--zip ZIP] [--out-dir DIR] [--force] | patch-notes [--out DIR] [--source NAME]... [--delay S] [--user-agent UA] [--canonical DIR] [--cache DIR] [--reparse] | source-manifest [--source-root PATH] [--out FILE] | validate <build> [--mids-dir DIR] [--format text|json] [--slotting] [--expect NAME=VALUE]...] (a claim holds when the number, rounded half away from zero to the claim's own decimal places, equals it)";

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
                output.WriteLine("Spec: docs/engine/mids-port-spec.md. Commands: rules, validate, chain, score, extract-canonical, patch-notes, source-manifest, mcp, ask.");
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
            case ["extract-canonical", ..]:
                return CanonicalExtractCommand.Run(args[1..], output, error);
            case ["validate", ..]:
                return RunValidate(args, output, error, mbdSource);
            case ["score", ..]:
                return RunScore(args, output, error);
            case ["patch-notes", ..]:
                return Guarded(() => PatchNotesCommand.Run(args[1..], output, error, handler: null), error);
            case ["source-manifest", ..]:
                return Guarded(() => SourceManifest.Run(args[1..], output, error, Environment.GetEnvironmentVariable, () => SourceRoot.GitUserName(Directory.GetCurrentDirectory())), error);
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
                IReadOnlyList<Diagnostic> diagnostics = [.. Validate(parsed, mbdSource), .. Verify(parsed)];
                output.WriteLine(ValidationText(parsed.Json, diagnostics));
                return DiagnosticFormatter.ExitCode(diagnostics);
            },
            error);
    }

    private static int RunScore(string[] args, TextWriter output, TextWriter error)
    {
        if (!ScoreCommand.TryParse(args[1..], out ScoreCommand? parsed, out string problem))
        {
            error.WriteLine(Usage);
            error.WriteLine("coh-engine score: error: " + problem);
            return 2;
        }

        return Guarded(
            () =>
            {
                output.WriteLine(parsed.Run());
                return 0;
            },
            error);
    }

    private static string ValidationText(bool json, IReadOnlyList<Diagnostic> diagnostics)
    {
        if (json)
        {
            return DiagnosticFormatter.FormatJson(diagnostics);
        }

        return diagnostics.Count > 0 ? DiagnosticFormatter.FormatText(diagnostics) : "no violations";
    }

    private static IReadOnlyList<Diagnostic> Validate(ValidateArguments parsed, IMbdBuildSource? mbdSource)
    {
        if (!IsMbd(parsed))
        {
            return ValidateCommand.RunDump(parsed.BuildPath, parsed.MidsDirectory ?? DumpScoring.DefaultMidsDirectory(parsed.BuildPath), ChecksOf(parsed));
        }

        return mbdSource is null
            ? throw new EngineException($"E72: cannot read {parsed.BuildPath}: .mbd validation is not available in this build; validate a Mids dump directory instead")
            : ValidateCommand.RunMbd(mbdSource, parsed.BuildPath, parsed.MidsDirectory ?? mbdSource.DefaultMidsDirectory, ChecksOf(parsed));
    }

    private static ValidateChecks ChecksOf(ValidateArguments parsed)
    {
        return parsed.Slotting ? ValidateChecks.All | ValidateChecks.Slotting : ValidateChecks.All;
    }

    /// <summary>The <c>--expect</c> claims checked against the build's computed metrics; nothing is computed when none were given.</summary>
    private static IReadOnlyList<Diagnostic> Verify(ValidateArguments parsed)
    {
        if (parsed.Expectations.Count == 0)
        {
            return [];
        }

        if (IsMbd(parsed))
        {
            throw new EngineException($"{ErrorCodes.E163}: cannot compute metrics for {parsed.BuildPath}: --expect needs a Mids dump directory, not a .mbd file, because a .mbd holds no power effects; validate a dump directory instead");
        }

        string mids = parsed.MidsDirectory ?? DumpScoring.DefaultMidsDirectory(parsed.BuildPath);
        return Expectations.Verify(parsed.Expectations, DumpScoring.Compute(parsed.BuildPath, mids), TotalsData.LoadEnumMaps(Path.Join(mids, "enums.json")), parsed.BuildPath);
    }

    private static bool IsMbd(ValidateArguments parsed)
    {
        return string.Equals(Path.GetExtension(parsed.BuildPath), ".mbd", StringComparison.Ordinal);
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
