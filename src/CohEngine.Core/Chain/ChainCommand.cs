using System.Globalization;

using CohEngine.Core.DamageLayers;

namespace CohEngine.Core.Chain;

/// <summary>The <c>chain</c> subcommand: argument parsing, the error contract, and the report output.</summary>
public static class ChainCommand
{
    private const string Usage = "usage: coh-engine chain build_dir [--mids-dir DIR] [--clicksoff-dir DIR] [--canonical DIR] [--mids-db DIR] "
        + "[--stance {melee,ranged,hybrid,all}] [--form FORM] [--target TARGET] [--move {run,jump,fly}] [--standoff FT] "
        + "[--assume KEY=VALUE] [--format {text,json}]";

    /// <summary>Runs the subcommand.</summary>
    /// <param name="args">The arguments after <c>chain</c>.</param>
    /// <param name="output">Where the report goes.</param>
    /// <param name="error">Where errors go.</param>
    /// <param name="workingDirectory">The directory relative paths resolve against, or null for the process's.</param>
    /// <returns>0 for no findings above info, 1 on an input error or an error finding, 2 for a warning, 2 for a usage error.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        Options? options = Parse(args, error);
        if (options is null)
        {
            return 2;
        }

        try
        {
            return Execute(options, output, workingDirectory);
        }
        catch (ChainException failure)
        {
            return Fail(error, failure.Message);
        }
        catch (MissingKeyException failure)
        {
            return Fail(error, $"E72: an input is missing the key {failure.Message}; re-dump the build or check the data matches its schema");
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            string where = failure is FileNotFoundException { FileName: { } file } ? file : "an input file";
            string reason = failure is FileNotFoundException or DirectoryNotFoundException ? "No such file or directory" : failure.Message;
            return Fail(error, $"E72: cannot read {where}: {reason}; check that the path exists and is readable");
        }
    }

    private static int Fail(TextWriter error, string message)
    {
        error.Write($"error: {message}\n");
        return 1;
    }

    private static int Execute(Options options, TextWriter output, string? workingDirectory)
    {
        string display = ChainPaths.Norm(options.BuildDir);
        string Resolve(string path)
        {
            string normal = ChainPaths.Norm(path);
            return workingDirectory is null || Path.IsPathRooted(normal) ? normal : Path.Combine(workingDirectory, normal);
        }

        string buildDir = Resolve(display);
        ChainSetup setup = ChainReport.BuildChainSetup(new ChainRequest
        {
            BuildDir = buildDir,
            MidsDir = options.MidsDir is not null ? Resolve(options.MidsDir) : Path.GetDirectoryName(Path.GetDirectoryName(buildDir)) ?? ".",
            ClicksOffDir = options.ClicksOffDir is not null ? Resolve(options.ClicksOffDir) : null,
            Canonical = options.Canonical is not null ? Resolve(options.Canonical) : null,
            MidsDb = options.MidsDb is not null ? Resolve(options.MidsDb) : null,
            Scenario = ChainReport.ParseTarget(options.Target),
            Move = options.Move,
            StandoffFt = options.Standoff,
            Assumptions = ChainReport.ParseAssumptions(options.Assume),
        });
        Stance[] stances = EnumText.TryParseStance(options.Stance, out Stance chosen) ? [chosen] : [Stance.Melee, Stance.Ranged, Stance.Hybrid];
        IReadOnlyList<StanceReport> results = ChainReport.RunChain(setup, stances, display, options.Form is null ? null : [options.Form]);
        output.Write((string.Equals(options.Format, "json", StringComparison.Ordinal) ? PyJson.Dumps(results.Select(result => (object?)ChainReport.StanceToJson(setup, result)).ToList()) : string.Join("\n\n", results.Select(result => ChainReport.StanceToText(setup, result)))) + "\n");
        IEnumerable<string> severities = results.SelectMany(result => result.Diagnostics).Select(diagnostic => diagnostic.Severity);
        return severities.Contains("error", StringComparer.Ordinal) ? 1 : severities.Contains("warning", StringComparer.Ordinal) ? 2 : 0;
    }

    private static Options? Parse(IReadOnlyList<string> args, TextWriter error)
    {
        Options options = new();
        List<string> assume = [];
        string? build = null;
        for (int i = 0; i < args.Count; i++)
        {
            string token = args[i];
            string? inline = null;
            if (token.StartsWith("--", StringComparison.Ordinal) && token.Contains('=', StringComparison.Ordinal))
            {
                inline = token[(token.IndexOf('=', StringComparison.Ordinal) + 1)..];
                token = token[..token.IndexOf('=', StringComparison.Ordinal)];
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                if (build is not null)
                {
                    return UsageError(error, $"unrecognized arguments: {token}");
                }

                build = token;
                continue;
            }

            string? value = inline ?? (i + 1 < args.Count ? args[i + 1] : null);
            if (value is null)
            {
                return UsageError(error, $"argument {token}: expected one argument");
            }

            if (inline is null)
            {
                i++;
            }

            string? problem = Apply(options, assume, token, value);
            if (problem is not null)
            {
                return UsageError(error, problem);
            }
        }

        if (build is null)
        {
            return UsageError(error, "the following arguments are required: build_dir");
        }

        return options with { BuildDir = build, Assume = assume };
    }

    private static string? Apply(Options options, List<string> assume, string name, string value)
    {
        switch (name)
        {
            case "--mids-dir":
                options.MidsDir = value;
                return null;
            case "--clicksoff-dir":
                options.ClicksOffDir = value;
                return null;
            case "--canonical":
                options.Canonical = value;
                return null;
            case "--mids-db":
                options.MidsDb = value;
                return null;
            case "--form":
                options.Form = value;
                return null;
            case "--target":
                options.Target = value;
                return null;
            case "--assume":
                assume.Add(value);
                return null;
            case "--stance":
                options.Stance = value;
                return new[] { "melee", "ranged", "hybrid", "all" }.Contains(value) ? null : $"argument --stance: invalid choice: '{value}'";
            case "--move":
                options.Move = value;
                return ChainReport.Moves.Contains(value, StringComparer.Ordinal) ? null : $"argument --move: invalid choice: '{value}'";
            case "--format":
                options.Format = value;
                return value is "text" or "json" ? null : $"argument --format: invalid choice: '{value}'";
            case "--standoff":
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double feet))
                {
                    return $"argument --standoff: invalid float value: '{value}'";
                }

                options.Standoff = feet;
                return null;
            default:
                return $"unrecognized arguments: {name}";
        }
    }

    private static Options? UsageError(TextWriter error, string message)
    {
        error.Write($"{Usage}\ncoh-engine chain: error: {message}\n");
        return null;
    }

    private sealed record Options
    {
        public string BuildDir { get; init; } = string.Empty;

        public string? MidsDir { get; set; }

        public string? ClicksOffDir { get; set; }

        public string? Canonical { get; set; }

        public string? MidsDb { get; set; }

        public string Stance { get; set; } = "all";

        public string? Form { get; set; }

        public string Target { get; set; } = "single";

        public string Move { get; set; } = "run";

        public double? Standoff { get; set; }

        public IReadOnlyList<string> Assume { get; init; } = [];

        public string Format { get; set; } = "text";
    }
}
