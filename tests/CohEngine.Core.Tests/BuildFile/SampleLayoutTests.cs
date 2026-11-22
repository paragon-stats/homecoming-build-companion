using System.Text.RegularExpressions;

using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.BuildFile;

/// <summary>Every committed file under <c>samples/builds</c> sits in one of the three layouts <c>samples/builds/README.md</c> describes.</summary>
public sealed partial class SampleLayoutTests
{
    private const int BuildsPerCharacter = 3;

    public static TheoryData<string[], string> Breaks => new()
    {
        { ["operator-builds/main.mbd"], "operator-builds/main.mbd: outside" },
        { ["characters/player/hero/main.mbd"], "characters/player/hero/main.mbd: outside" },
        { ["community/scrapper/old/dark-shield.mbd"], "community/scrapper/old/dark-shield.mbd: outside" },
        { ["community/scrapper/Dark-Shield.mbd"], "'Dark-Shield.mbd' is not lower-case words" },
        { ["community/scrapper/dark shield.mbd"], "'dark shield.mbd' is not lower-case words" },
        { ["community/dominator/arsenal-r4.mbd"], "'arsenal-r4.mbd' holds version text" },
        { ["characters/account1/hero/a.mbd", "characters/account1/hero/b.mbd", "characters/account1/hero/c.mbd", "characters/account1/hero/d.mxd"], "characters/account1/hero: 4 builds" },
        { ["community/scrapper/dark-shield.mbd", "community/Scrapper/dark-shield.mbd"], "differ only in letter case" },
    };

    // Lower-case words joined by hyphens; a file name adds its extensions after dots.
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*(\.[a-z0-9]+)*$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LowerCaseName { get; }

    // A revision replaces the file and git keeps the history, so v2, r4, rev3 or i25 does not belong in a name.
    [GeneratedRegex("^(v|r|i|rev|version)[0-9]+$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionWord { get; }

    [GeneratedRegex("^account[0-9]+$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AccountLabel { get; }

    /// <summary>Names every way a list of files breaks the layout.</summary>
    /// <param name="paths">The files, relative to <c>samples/builds</c> with forward slashes.</param>
    /// <returns>One line per problem; empty when the layout holds.</returns>
    internal static List<string> Problems(IReadOnlyList<string> paths)
    {
        List<string> problems = [];
        foreach (string path in paths.Where(path => !string.Equals(path, "README.md", StringComparison.Ordinal)))
        {
            string[] parts = path.Split('/');
            if (!InLayout(parts))
            {
                problems.Add($"{path}: outside characters/<account>/<character>/, reference/<archetype>/<focus>/ and community/<archetype>/");
            }

            problems.AddRange(parts.Where(part => !LowerCaseName.IsMatch(part)).Select(part => $"{path}: '{part}' is not lower-case words joined by hyphens"));
            problems.AddRange(parts.Where(part => part.Split('-', '.').Any(VersionWord.IsMatch)).Select(part => $"{path}: '{part}' holds version text"));
        }

        problems.AddRange(paths
            .Where(path => path.StartsWith("characters/", StringComparison.Ordinal) && (path.EndsWith(".mbd", StringComparison.Ordinal) || path.EndsWith(".mxd", StringComparison.Ordinal)))
            .GroupBy(path => path[..path.LastIndexOf('/')], StringComparer.Ordinal)
            .Where(folder => folder.Count() > BuildsPerCharacter)
            .Select(folder => $"{folder.Key}: {folder.Count()} builds, more than {BuildsPerCharacter}"));
        problems.AddRange(paths
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Where(same => same.Count() > 1)
            .Select(same => $"{string.Join(", ", same)}: differ only in letter case"));
        return problems;
    }

    private static bool InLayout(string[] parts)
    {
        return parts[0] switch
        {
            "characters" => parts.Length == 4 && AccountLabel.IsMatch(parts[1]),
            "reference" => parts.Length == 4,
            "community" => parts.Length == 3,
            _ => false,
        };
    }

    [Fact]
    public void The_committed_sample_builds_follow_the_layout()
    {
        Assert.Empty(Problems(BuildFileFixtures.SampleFiles()));
    }

    [Fact]
    public void Each_layout_with_up_to_three_builds_and_their_other_files_passes()
    {
        Assert.Empty(Problems(
        [
            "README.md",
            "characters/account1/hero/main.mbd",
            "characters/account1/hero/main.playstyle.json",
            "characters/account1/hero/pve.mbd",
            "characters/account1/hero/pvp.mxd",
            "reference/blaster/blapper/energy-energy-4-star.mbd",
            "community/scrapper/dark-shield.mbd",
        ]));
    }

    [Theory]
    [MemberData(nameof(Breaks))]
    public void A_break_of_the_layout_is_named(string[] paths, string problem)
    {
        Assert.Contains(Problems(paths), line => line.Contains(problem, StringComparison.Ordinal));
    }
}
