using System.Globalization;
using System.Text.RegularExpressions;

using CohEngine.Core.SourceMaterial;
using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

// Invented names only: a real name must never be committed.
[Collection(DevScripts.Collection)]
public sealed partial class BannedNamesScriptTests
{
    private const string Names = " Zorvex Quill , ,Brannoc ";

    private static readonly string[] Lines =
    [
        "a guide by zorvex quill",
        "ZORVEX QUILL again",
        "Zorvex Quillsby is another person",
        "brannoc's build",
        "Brannocs is not the name",
        "xBrannoc is not either",
        "nothing here",
    ];

    [GeneratedRegex(@":(?<line>\d+): operator name", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OperatorNameHit { get; }

    [Fact]
    public void The_manifest_filter_and_the_banned_word_check_judge_the_same_lines_alike()
    {
        Regex pattern = BannedNames.Pattern(BannedNames.Parse(Names))!;
        using TempTree tree = new();
        string seed = tree.Write("seed.txt", string.Join('\n', Lines) + "\n");

        (_, string report) = DevScripts.RunScriptWithEnvironment("check-banned-words.cs", tree.Root, new Dictionary<string, string?>(StringComparer.Ordinal) { [BannedNames.EnvironmentVariable] = Names }, "--seed", seed);

        IEnumerable<int> flaggedByScript = OperatorNameHit.Matches(report).Select(match => int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture));
        IEnumerable<int> flaggedByFilter = Lines.Select((line, index) => (line, index)).Where(pair => pattern.IsMatch(pair.line)).Select(pair => pair.index + 1);
        Assert.Equal([1, 2, 4], flaggedByFilter);
        Assert.Equal(flaggedByFilter, flaggedByScript);
    }
}
