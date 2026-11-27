using System.Diagnostics.CodeAnalysis;

namespace CohEngine.Core.SourceMaterial;

/// <summary>
/// The <c>--name value</c>, <c>--name=value</c> and <c>--flag</c> options of a command that takes no positional
/// arguments, with argparse's error texts.
/// </summary>
internal static class CommandOptions
{
    /// <summary>Parses the options.</summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="valued">The options that take a value; each may repeat, every value kept in order.</param>
    /// <param name="flags">The options that take none.</param>
    /// <param name="values">Each option given, with its values (none for a flag).</param>
    /// <param name="problem">The reason, when parsing failed.</param>
    /// <returns>True when the arguments parsed.</returns>
    public static bool TryParse(IReadOnlyList<string> args, IReadOnlyCollection<string> valued, IReadOnlyCollection<string> flags, [NotNullWhen(true)] out Dictionary<string, List<string>>? values, out string problem)
    {
        values = new(StringComparer.Ordinal);
        problem = string.Empty;
        Queue<string> pending = new(args);
        while (pending.TryDequeue(out string? arg))
        {
            (string name, string? inline) = Split(arg);
            List<string> given = values.GetValueOrDefault(name) ?? [];
            if (flags.Contains(name, StringComparer.Ordinal) && inline is null)
            {
                values[name] = given;
                continue;
            }

            if (!valued.Contains(name, StringComparer.Ordinal))
            {
                problem = "unrecognized arguments: " + arg;
                values = null;
                return false;
            }

            if (inline is null && !pending.TryDequeue(out inline))
            {
                problem = $"argument {name}: expected one argument";
                values = null;
                return false;
            }

            given.Add(inline);
            values[name] = given;
        }

        return true;
    }

    /// <summary>An argument's option name and the value written after <c>=</c>, or null when it has none.</summary>
    private static (string Name, string? Inline) Split(string arg)
    {
        int equals = arg.IndexOf('=', StringComparison.Ordinal);
        return arg.StartsWith("--", StringComparison.Ordinal) && equals > 0 ? (arg[..equals], arg[(equals + 1)..]) : (arg, null);
    }
}
