using System.Globalization;
using System.Text.RegularExpressions;

using CohEngine.Core.Chain;

namespace CohEngine.Core.SourceMaterial;

/// <summary>
/// The names the public repository must not carry, from <c>COH_BANNED_NAMES</c> (comma-separated), matched as whole words
/// in any case. <c>scripts/dev/check-banned-words.cs</c> reads the same variable with the same rule.
/// </summary>
public static class BannedNames
{
    /// <summary>The environment variable that holds the names.</summary>
    public const string EnvironmentVariable = "COH_BANNED_NAMES";

    /// <summary>The names in a <c>COH_BANNED_NAMES</c> value: split at commas, trimmed, empty ones dropped.</summary>
    /// <param name="value">The variable's value, or null when it is unset.</param>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> Parse(string? value)
    {
        return (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>The whole-word, any-case pattern for the names, or null when there are none.</summary>
    /// <param name="names">The names.</param>
    /// <returns>The pattern, or null.</returns>
    public static Regex? Pattern(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return names.Count == 0
            ? null
            : new Regex(@"\b(?:" + string.Join('|', names.Select(Regex.Escape)) + @")\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// The names to withhold: those in <c>COH_BANNED_NAMES</c>, else, outside CI as the banned-word check does, the git
    /// user name and its first word, so a local run cannot write the operator's name into a committed file.
    /// </summary>
    /// <param name="value">The variable's value, or null when it is unset.</param>
    /// <param name="gitUserName">Reads the git user name; null when git has none.</param>
    /// <param name="inCi">True under CI (<c>GITHUB_ACTIONS</c> set), where the runner's git name is not the operator's.</param>
    /// <returns>The names; none when there is nothing to withhold.</returns>
    internal static IReadOnlyList<string> Names(string? value, Func<string?> gitUserName, bool inCi)
    {
        IReadOnlyList<string> names = Parse(value);
        if (names.Count > 0 || inCi || gitUserName() is not { } full)
        {
            return names;
        }

        return [.. new[] { full, full.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0] }.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The rows that name no banned name, judged on each row's one-line JSON, and how many were left out.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The kept rows and the count withheld.</returns>
    internal static (IReadOnlyList<PyObject> Kept, int Withheld) Withhold(IReadOnlyList<PyObject> rows, Regex pattern)
    {
        List<PyObject> kept = [.. rows.Where(row => !pattern.IsMatch(PyJson.Dumps(row, indented: false, ensureAscii: false)))];
        return (kept, rows.Count - kept.Count);
    }

    /// <summary>The one status line of a run.</summary>
    /// <param name="withheld">The rows withheld.</param>
    /// <returns>The line.</returns>
    internal static string Report(int withheld)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{withheld} row(s) withheld by the name filter");
    }
}
