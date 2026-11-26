using System.Diagnostics;
using System.Text.RegularExpressions;

using CohEngine.Core.Chain;
using CohEngine.Core.SourceMaterial;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.SourceMaterial;

// Invented names only: a real name must never be committed.
public sealed partial class BannedNamesTests
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
    public void The_names_are_split_at_commas_and_trimmed()
    {
        Assert.Equal(["Zorvex Quill", "Brannoc"], BannedNames.Parse(Names));
        Assert.Empty(BannedNames.Parse(null));
        Assert.Null(BannedNames.Pattern([]));
    }

    [Fact]
    public void A_row_is_judged_on_its_json_and_only_the_count_is_reported()
    {
        Regex pattern = BannedNames.Pattern(BannedNames.Parse(Names))!;
        PyObject named = new() { { "title", "Brannoc's \"guide\"" } };
        PyObject clean = new() { { "title", "Fire guide" } };

        (IReadOnlyList<PyObject> kept, int withheld) = BannedNames.Withhold([named, clean], pattern);

        Assert.Equal((1, 1), (kept.Count, withheld));
        Assert.Same(clean, kept[0]);
        Assert.Equal("1 row(s) withheld by the name filter", BannedNames.Report(pattern, withheld));
        Assert.Equal("name filter off: COH_BANNED_NAMES is unset, no row withheld", BannedNames.Report(pattern: null, withheld: 0));
        Assert.Equal(2, BannedNames.Withhold([named, clean], pattern: null).Kept.Count);
    }

    [Fact]
    public void The_filter_and_the_repository_s_banned_word_check_judge_the_same_lines_alike()
    {
        Regex pattern = BannedNames.Pattern(BannedNames.Parse(Names))!;
        using TempTree tree = new();
        string seed = tree.Write("seed.txt", string.Join('\n', Lines) + "\n");
        ProcessStartInfo start = new("dotnet", ["run", Path.Join(Fixtures.Store.RepositoryRoot, "scripts", "dev", "check-banned-words.cs"), "--", "--seed", seed])
        {
            WorkingDirectory = Fixtures.Store.RepositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment[BannedNames.EnvironmentVariable] = Names;

        // The script and the build it triggers are CI tooling, not product code: the coverage profiler is not attached to them.
        start.Environment["CORECLR_ENABLE_PROFILING"] = "0";

        using Process script = Process.Start(start)!;
        script.StandardOutput.ReadToEnd();
        string report = script.StandardError.ReadToEnd();
        script.WaitForExit();

        IEnumerable<int> flaggedByScript = OperatorNameHit.Matches(report).Select(match => int.Parse(match.Groups["line"].Value, System.Globalization.CultureInfo.InvariantCulture));
        IEnumerable<int> flaggedByFilter = Lines.Select((line, index) => (line, index)).Where(pair => pattern.IsMatch(pair.line)).Select(pair => pair.index + 1);
        Assert.Equal([1, 2, 4], flaggedByFilter);
        Assert.Equal(flaggedByFilter, flaggedByScript);
    }
}
