using CohEngine.Core.Chain;
using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

public sealed class ChainCommandTests
{
    private static readonly FixtureStore Store = FixtureStore.Locate(AppContext.BaseDirectory);
    private static readonly string Build = "tests/fixtures/mids/builds/shield_scrapper_set_bonuses";

    public static TheoryData<string[], string> UsageErrors => new()
    {
        { [], "the following arguments are required: build_dir" },
        { [Build, "extra"], "unrecognized arguments: extra" },
        { [Build, "--bogus", "1"], "unrecognized arguments: --bogus" },
        { [Build, "--format"], "argument --format: expected one argument" },
        { [Build, "--format", "xml"], "argument --format: invalid choice: 'xml'" },
        { [Build, "--stance", "side"], "argument --stance: invalid choice: 'side'" },
        { [Build, "--move", "swim"], "argument --move: invalid choice: 'swim'" },
        { [Build, "--standoff", "far"], "argument --standoff: invalid float value: 'far'" },
    };

    public static TheoryData<string[], int, string> Runs => new()
    {
        { [Build, "--stance", "ranged"], 0, "== ranged stance ==" },
        { [Build, "--stance=melee", "--form", string.Empty], 0, "== melee stance ==" },
        { [Build, "--target", "spawn:3", "--standoff", "30", "--move", "jump", "--assume", "damage.enhancement_avg=0.5"], 0, "== hybrid stance ==" },
        { [Build, "--format=json", "--stance", "melee"], 0, "\"stance\": \"melee\"" },
        { [Build, "--mids-dir", "tests/fixtures/mids", "--canonical", "data/canonical", "--clicksoff-dir", Build, "--stance", "melee"], 0, "== melee stance ==" },
    };

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int code = ChainCommand.Run(args, output, error, Store.RepositoryRoot);
        return (code, output.ToString(), error.ToString());
    }

    [Theory]
    [MemberData(nameof(UsageErrors))]
    public void A_bad_command_line_prints_usage_and_exits_2(string[] args, string problem)
    {
        (int code, _, string error) = Run(args);

        Assert.Equal($"2 coh-engine chain: error: {problem}", $"{code} {error.Split('\n')[1]}");
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public void A_good_command_line_reports_and_exits_0(string[] args, int expectedCode, string expectedText)
    {
        (int code, string output, _) = Run(args);

        Assert.Equal($"{expectedCode} True", $"{code} {output.Contains(expectedText, StringComparison.Ordinal)}");
    }

    [Fact]
    public void The_text_output_ends_with_a_single_newline_and_separates_stances_by_a_blank_line()
    {
        (_, string output, _) = Run(Build);

        Assert.Contains("\n\n== ranged stance ==", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_warning_finding_exits_2()
    {
        using TempTree tree = new();
        string build = tree.CopyFiles(Path.Combine(Store.MidsDirectory, "builds", "Laser - NUKE - Corruptor (Fire Blast - Radiation Emission)"), "b");

        Assert.Equal(2, Run(build, "--mids-dir", Store.MidsDirectory).Code);
    }

    [Theory]
    [InlineData("--standoff", "0", "error: E70: standoff is 0 ft but must be positive")]
    [InlineData("--target", "many", "error: E68: target 'many'")]
    [InlineData("--assume", "nope", "error: E67: assumption 'nope'")]
    [InlineData("--form", "Nova", "error: E65: form 'Nova' is not a form of this build")]
    [InlineData("--move", "fly", "error: E69: movement mode 'fly'")]
    public void An_input_error_prints_one_error_line_and_exits_1(string option, string value, string expected)
    {
        (int code, string output, string error) = Run(Build, option, value);

        Assert.Equal($"1 True {string.Empty}", $"{code} {error.StartsWith(expected, StringComparison.Ordinal)} {output}");
    }

    [Fact]
    public void A_missing_key_is_reported_as_e72()
    {
        using TempTree tree = new();
        string build = tree.CopyFiles(Path.Combine(Store.MidsDirectory, "builds", "shield_scrapper_set_bonuses"), "b");
        File.WriteAllText(Path.Combine(build, "totals.json"), File.ReadAllText(Path.Combine(build, "totals.json")).Replace("\"Class\"", "\"Klass\"", StringComparison.Ordinal));

        Assert.Equal("1 error: E72: an input is missing the key 'Class'; re-dump the build or check the data matches its schema\n", $"{Run(build, "--mids-dir", Store.MidsDirectory).Code} {Run(build, "--mids-dir", Store.MidsDirectory).Error}");
    }

    [Fact]
    public void A_missing_directory_is_reported_as_e72()
    {
        using TempTree tree = new();

        Assert.Equal("error: E72: cannot read an input file: No such file or directory; check that the path exists and is readable\n", Run(Build, "--canonical", tree.FullPath("none")).Error);
    }

    [Fact]
    public void A_missing_file_names_itself_in_e72()
    {
        using TempTree tree = new();

        Assert.StartsWith("error: E72: cannot read ", Run(Build, "--mids-db", tree.Root).Error, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_file_is_reported_as_e72()
    {
        using TempTree tree = new();
        Directory.CreateDirectory(tree.FullPath("AttribMod.json"));

        Assert.StartsWith("error: E72: cannot read an input file: ", Run(Build, "--mids-db", tree.Root).Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tests/x/", "tests{0}x")]
    [InlineData("./a/./b", "a{0}b")]
    [InlineData("a\\b", "a{0}b")]
    [InlineData("", ".")]
    [InlineData("/abs/p", "{0}abs{0}p")]
    public void Paths_print_as_python_prints_them(string path, string expected)
    {
        Assert.Equal(string.Format(System.Globalization.CultureInfo.InvariantCulture, expected, Path.DirectorySeparatorChar), ChainPaths.Norm(path));
    }

    [Fact]
    public void The_repository_root_is_found_from_the_binary()
    {
        Assert.Equal(Store.RepositoryRoot, ChainPaths.RepoRoot());
    }
}
