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
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            int equals = arg.IndexOf('=', StringComparison.Ordinal);
            string name = arg.StartsWith("--", StringComparison.Ordinal) && equals > 0 ? arg[..equals] : arg;
            List<string> given = values.TryGetValue(name, out List<string>? known) ? known : [];
            if (flags.Contains(name, StringComparer.Ordinal) && equals < 0)
            {
                values[name] = given;
            }
            else if (!valued.Contains(name, StringComparer.Ordinal))
            {
                problem = "unrecognized arguments: " + arg;
                values = null;
                return false;
            }
            else if (equals > 0 || index + 1 < args.Count)
            {
                given.Add(equals > 0 ? arg[(equals + 1)..] : args[++index]);
                values[name] = given;
            }
            else
            {
                problem = $"argument {name}: expected one argument";
                values = null;
                return false;
            }
        }

        return true;
    }
}
