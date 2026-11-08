using System.Diagnostics.CodeAnalysis;

namespace CohEngine.Core;

/// <summary>The parsed <c>validate</c> command line: the build path, <c>--mids-dir</c> and <c>--format</c>.</summary>
/// <param name="BuildPath">The Mids dump directory or <c>.mbd</c> file.</param>
/// <param name="MidsDirectory">The shared-dump directory, or null for the default.</param>
/// <param name="Json">Whether <c>--format json</c> was given.</param>
internal sealed record ValidateArguments(string BuildPath, string? MidsDirectory, bool Json)
{
    /// <summary>Parses the arguments after <c>validate</c>, accepting <c>--opt value</c> and <c>--opt=value</c>.</summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="parsed">The parsed arguments, when parsing succeeded.</param>
    /// <param name="problem">The reason, when parsing failed.</param>
    /// <returns>True when the arguments parsed.</returns>
    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out ValidateArguments? parsed, out string problem)
    {
        string? buildPath = null;
        string? midsDirectory = null;
        string format = "text";
        parsed = null;
        problem = string.Empty;
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            if (arg is not ("--mids-dir" or "--format") && !arg.StartsWith("--mids-dir=", StringComparison.Ordinal) && !arg.StartsWith("--format=", StringComparison.Ordinal))
            {
                if (arg.StartsWith('-') || buildPath is not null)
                {
                    problem = "unrecognized arguments: " + arg;
                    return false;
                }

                buildPath = arg;
                continue;
            }

            string name = arg.Split('=')[0];
            string? value = arg.Contains('=', StringComparison.Ordinal) ? arg[(name.Length + 1)..] : (index + 1 < args.Count ? args[++index] : null);
            if (value is null)
            {
                problem = $"argument {name}: expected one argument";
                return false;
            }

            if (string.Equals(name, "--mids-dir", StringComparison.Ordinal))
            {
                midsDirectory = value;
            }
            else if (value is "text" or "json")
            {
                format = value;
            }
            else
            {
                problem = $"argument --format: invalid choice: '{value}' (choose from 'text', 'json')";
                return false;
            }
        }

        if (buildPath is null)
        {
            problem = "the following arguments are required: build_dir";
            return false;
        }

        parsed = new ValidateArguments(buildPath, midsDirectory, string.Equals(format, "json", StringComparison.Ordinal));
        return true;
    }
}
