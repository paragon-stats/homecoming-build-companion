using System.Diagnostics.CodeAnalysis;

using CohEngine.Core.Registry;

namespace CohEngine.Core;

/// <summary>The parsed <c>validate</c> command line: the build path, <c>--mids-dir</c>, <c>--format</c> and every <c>--expect</c>.</summary>
/// <param name="BuildPath">The Mids dump directory or <c>.mbd</c> file.</param>
/// <param name="MidsDirectory">The shared-dump directory, or null for the default.</param>
/// <param name="Json">Whether <c>--format json</c> was given.</param>
/// <param name="Expectations">The <c>--expect metric=value</c> claims, in command-line order.</param>
internal sealed record ValidateArguments(string BuildPath, string? MidsDirectory, bool Json, IReadOnlyList<Expectation> Expectations)
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
        List<Expectation> expectations = [];
        parsed = null;
        problem = string.Empty;
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            if (arg is not ("--mids-dir" or "--format" or "--expect") && !arg.StartsWith("--mids-dir=", StringComparison.Ordinal) && !arg.StartsWith("--format=", StringComparison.Ordinal) && !arg.StartsWith("--expect=", StringComparison.Ordinal))
            {
                if (arg.StartsWith('-') || buildPath is not null)
                {
                    problem = "unrecognized arguments: " + arg;
                    return false;
                }

                buildPath = arg;
                continue;
            }

            if (!TryTakeValue(args, ref index, out string name, out string value))
            {
                problem = $"argument {name}: expected one argument";
                return false;
            }

            if (string.Equals(name, "--mids-dir", StringComparison.Ordinal))
            {
                midsDirectory = value;
            }
            else if (string.Equals(name, "--expect", StringComparison.Ordinal))
            {
                problem = ParseExpectation(value, expectations);
                if (problem.Length != 0)
                {
                    return false;
                }
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

        parsed = new ValidateArguments(buildPath, midsDirectory, string.Equals(format, "json", StringComparison.Ordinal), expectations);
        return true;
    }

    /// <summary>Reads an option's value, from <c>--opt=value</c> or from the next argument, which it then consumes.</summary>
    private static bool TryTakeValue(IReadOnlyList<string> args, ref int index, out string name, out string value)
    {
        string arg = args[index];
        name = arg.Split('=')[0];
        if (arg.Contains('=', StringComparison.Ordinal))
        {
            value = arg[(name.Length + 1)..];
            return true;
        }

        index++;
        value = index < args.Count ? args[index] : string.Empty;
        return index < args.Count;
    }

    /// <summary>Adds a <c>metric=value</c> claim; returns the reason it is malformed, or an empty string.</summary>
    private static string ParseExpectation(string value, List<Expectation> expectations)
    {
        int split = value.IndexOf('=', StringComparison.Ordinal);
        if (split < 1 || split == value.Length - 1)
        {
            return $"argument --expect: expected metric=value, got '{value}'";
        }

        expectations.Add(new Expectation(value[..split], value[(split + 1)..]));
        return string.Empty;
    }
}
