using CohEngine.Core.Chain;
using CohEngine.Core.SourceMaterial;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.SourceMaterial;

// The expected-*.json files are what `python -m scripts.forum_scrape.manifest` wrote for the same fixture roots, with
// COH_BANNED_NAMES unset. The fixture captures are copied byte for byte from the homecoming-source-material repository.
public sealed class SourceManifestTests
{
    private static readonly string Captures = Path.Join(Fixtures.Store.Root, "source-material");

    private static (int Code, string Output, string Error) Run(Dictionary<string, string>? variables, params string[] args)
    {
        return RunAs(gitUserName: null, variables, args);
    }

    private static (int Code, string Output, string Error) RunAs(string? gitUserName, Dictionary<string, string>? variables, params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int code = SourceManifest.Run(args, output, error, name => variables?.GetValueOrDefault(name), () => gitUserName);
        return (code, output.ToString(), error.ToString());
    }

    [Theory]
    [InlineData("source")]
    [InlineData("captures")]
    public void The_manifest_of_the_fixture_captures_is_the_bytes_the_python_wrote(string root)
    {
        using TempTree tree = new();
        string written = tree.FullPath("nested/manifest.json");

        (int code, string output, string error) = Run(variables: null, "--source-root", Path.Join(Captures, root), "--out=" + written);

        Assert.Equal((0, string.Empty), (code, error));
        Assert.Contains("name filter off: COH_BANNED_NAMES is unset and git config user.name is empty, no row withheld", output, StringComparison.Ordinal);
        Assert.Equal(File.ReadAllBytes(Path.Join(Captures, $"expected-{root}-manifest.json")), File.ReadAllBytes(written));
    }

    [Fact]
    public void Rows_take_the_url_from_the_ledger_and_hash_the_body_when_the_capture_records_neither()
    {
        IReadOnlyList<PyObject> rows = SourceManifest.Build(Path.Join(Captures, "source", "community"), TextWriter.Null);

        Assert.Equal(["community/archetypes/scrapper/bio.md", "community/mechanics/claws.md", "community/pre-sunset/mechanics/old.md"], rows.Select(row => (string)row["path"]!), StringComparer.Ordinal);
        Assert.Equal("https://forums.example/topic/2-bio/", rows[0]["url"]);
        Assert.Equal(SourceManifest.ContentHash("# Bio Armor notes\n\nNo stored hash and no URL here; both come from the body and the ledger.\n"), rows[0]["content_hash"]);
        Assert.Equal("https://archive.example/topic/3-old/", rows[2]["url"]);
        Assert.Equal("pre-sunset", rows[2]["era"]);
    }

    [Fact]
    public void A_capture_with_crlf_line_endings_gives_the_row_its_lf_copy_gives()
    {
        using TempTree tree = new();
        string capture = "---\ntitle: \"Kin guide\"\nurl: \"https://forums.example/topic/9-kin/\"\n---\n\n# Kin guide\n\nBody.\n";
        tree.Write("lf/community/a.md", capture);
        tree.Write("crlf/community/a.md", capture.Replace("\n", "\r\n", StringComparison.Ordinal));

        string lfText = SourceManifest.Render(SourceManifest.Build(tree.FullPath("lf/community"), TextWriter.Null));
        string crlf = SourceManifest.Render(SourceManifest.Build(tree.FullPath("crlf/community"), TextWriter.Null));

        Assert.Equal(lfText, crlf);
        Assert.Contains(SourceManifest.ContentHash("# Kin guide\n\nBody.\n"), crlf, StringComparison.Ordinal);
    }

    [Fact]
    public void Captures_without_front_matter_or_title_are_left_out_and_odd_values_read_as_python_reads_them()
    {
        using TempTree tree = new();
        tree.Write("community/notes.md", "# no front matter\n");
        tree.Write("community/open.md", "---\ntitle: never closed\n");
        tree.Write("community/untitled.md", "---\nurl: \"x\"\n---\nbody\n");
        tree.Write("community/odd.md", "---\ntitle: 'Fire/Kin Controllers \u2013 a guide'\ndate_posted: 2024\nurl: null\n---\nbody\n");
        using StringWriter error = new();

        IReadOnlyList<PyObject> rows = SourceManifest.Build(tree.FullPath("community"), error);

        Assert.Equal("E212: untitled.md: front matter has no title; capture skipped; add a title: line to its front matter" + Environment.NewLine, error.ToString());
        PyObject odd = Assert.Single(rows);
        Assert.Equal("Fire/Kin Controllers \u2013 a guide", odd["title"]);
        Assert.Equal("2024", odd["date_posted"]);
        Assert.Equal("None", odd["url"]);
        Assert.Equal("controller", odd["archetype"]);
        Assert.Contains("\\u2013", SourceManifest.Render(rows), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_root_is_reported_and_writes_nothing()
    {
        using TempTree tree = new();

        (int code, _, string error) = Run(variables: null, "--source-root", tree.Root, "--out", tree.FullPath("m.json"));

        Assert.Equal(1, code);
        Assert.StartsWith($"E211: no captures under {Path.Join(tree.Root, "community")}; expected community/**/*.md with front matter", error, StringComparison.Ordinal);
        Assert.EndsWith("or pass --source-root" + Environment.NewLine, error, StringComparison.Ordinal);
        Assert.False(File.Exists(tree.FullPath("m.json")));
    }

    [Fact]
    public void Rows_naming_a_banned_word_are_left_out_and_counted()
    {
        using TempTree tree = new();
        string written = tree.FullPath("manifest.json");

        (int code, string output, _) = Run(new(StringComparer.Ordinal) { ["COH_BANNED_NAMES"] = "claws", ["COH_SOURCE_ROOT"] = Path.Join(Captures, "source") }, "--out", written);

        Assert.Equal(0, code);
        Assert.Contains("2 row(s) withheld by the name filter", output, StringComparison.Ordinal);
        Assert.Contains("1 capture(s) listed in " + written, output, StringComparison.Ordinal);
        Assert.DoesNotContain("claws", File.ReadAllText(written), StringComparison.Ordinal);
    }

    [Fact]
    public void With_the_variable_unset_the_filter_takes_the_git_user_name_and_its_first_word()
    {
        using TempTree tree = new();
        string written = tree.FullPath("manifest.json");

        (int code, string output, _) = RunAs("Claws Keeper", variables: null, "--source-root", Path.Join(Captures, "source"), "--out", written);

        Assert.Equal(0, code);
        Assert.Contains("2 row(s) withheld by the name filter", output, StringComparison.Ordinal);
        Assert.DoesNotContain("claws", File.ReadAllText(written), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_root_whose_every_row_is_withheld_says_so_and_writes_nothing()
    {
        using TempTree tree = new();
        string written = tree.FullPath("manifest.json");

        (int code, _, string error) = Run(new(StringComparer.Ordinal) { ["COH_BANNED_NAMES"] = "claws,bio" }, "--source-root", Path.Join(Captures, "source"), "--out", written);

        Assert.Equal(1, code);
        Assert.StartsWith($"E213: every capture under {Path.Join(Captures, "source", "community")} was withheld by the name filter (3 rows), so nothing was written;", error, StringComparison.Ordinal);
        Assert.False(File.Exists(written));
    }

    [Fact]
    public void A_manifest_that_cannot_be_written_is_a_write_error()
    {
        using TempTree tree = new();
        string folder = Directory.CreateDirectory(tree.FullPath("taken")).FullName;

        (int code, _, string error) = Run(variables: null, "--source-root", Path.Join(Captures, "source"), "--out", folder);

        Assert.Equal(1, code);
        Assert.StartsWith($"E214: cannot write {folder}: ", error, StringComparison.Ordinal);
        Assert.Contains("; check that the path names a file in a folder you can write to", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--out")]
    public void Bad_arguments_print_the_usage_and_exit_2(string arg)
    {
        (int code, _, string error) = Run(variables: null, arg);

        Assert.Equal(2, code);
        Assert.StartsWith(SourceManifest.Usage, error, StringComparison.Ordinal);
    }
}
