using System.Text.RegularExpressions;

namespace CohEngine.Core.Tests.Diagnostics;

/// <summary>The <c>{severity}: {code}: {message}</c> form of docs/style-guides/error-output.md, checked on a command's error output.</summary>
internal static partial class CodedOutput
{
    [GeneratedRegex(@"\bE[0-9]{2,3}: ", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Coded { get; }

    [GeneratedRegex("^(error|warning): E[0-9]{2,3}: ", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Labelled { get; }

    /// <summary>Asserts that every line carrying an error code opens with its severity, and returns those lines.</summary>
    /// <param name="error">The command's error output.</param>
    /// <returns>The coded lines.</returns>
    public static string[] AssertLabelled(string error)
    {
        string[] coded = [.. error.Split(Environment.NewLine).Where(line => Coded.IsMatch(line))];
        Assert.All(coded, line => Assert.Matches(Labelled, line));
        return coded;
    }
}
