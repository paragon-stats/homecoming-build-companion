using System.Text.RegularExpressions;

using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.LegalityRules;

/// <summary>
/// H-ENH-005: stealth procs, and superior or attuned versions of one set piece, are mutually exclusive. It gates on the
/// mutex name, never on the <c>Superior</c> flag: purples carry <c>Superior</c> with no mutex.
/// </summary>
internal static partial class MutexRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The legality context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(LegalityContext context)
    {
        string? stealth = null;
        Dictionary<string, string> versions = new(StringComparer.Ordinal);
        foreach ((Power power, _, EnhancementLegality? enhancement) in context.Filled)
        {
            if (enhancement is null || string.Equals(enhancement.MutexName, "None", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(enhancement.MutexName, "Stealth", StringComparison.Ordinal))
            {
                if (stealth is not null)
                {
                    yield return Conflict(enhancement, power, $"the stealth proc in {PyText.Repr(stealth)}", "one stealth proc");
                }
                else
                {
                    stealth = power.FullName;
                }

                continue;
            }

            string baseVersion = VersionPrefix().Replace(enhancement.Uid, string.Empty);
            if (versions.TryGetValue(baseVersion, out string? other))
            {
                yield return Conflict(enhancement, power, $"a version of the same piece in {PyText.Repr(other)}", "one version");
            }
            else
            {
                versions[baseVersion] = power.FullName;
            }
        }
    }

    [GeneratedRegex("(?:Attuned_|Superior_)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionPrefix();

    private static Diagnostic Conflict(EnhancementLegality enhancement, Power power, string conflict, string limit)
    {
        string message = $"{PyText.Repr(enhancement.LongName)} ({enhancement.Uid}) in {PyText.Repr(power.FullName)} is mutually exclusive with {conflict}; "
            + $"only {limit} may be slotted across the build";
        return new Diagnostic("H-ENH-005", "error", message)
        {
            Fix = "remove one of the mutually exclusive pieces",
        };
    }
}
