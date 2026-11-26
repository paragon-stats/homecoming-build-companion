using System.Net;

using CohEngine.Core.Chain;
using CohEngine.Core.PatchNotes;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.PatchNotes;

// The cases of scripts/forum_scrape/tests/test_patch_notes.py, run against a fake network.
public sealed class PatchNotesCommandTests
{
    public const string WikiIndex = """
        <a href="/wiki/Patch_Notes/2005-10-27">Oct 27 2005</a>
        <a href="/wiki/Patch_Notes/2004-05-11">May 11 2004</a>
        <a href="/wiki/Patch_Notes/2012-08-23_Beta">Beta</a>
        <a href="/wiki/Patch_Notes/2005-10-27#Powers">anchor</a>
        <a href="/wiki/Patch_Notes">index</a>
        """;

    public const string WikiPage = """
        <html><head><title>Patch Notes/2005-10-27 - Paragon Wiki Archive</title></head><body>
        <div class="mw-content-ltr mw-parser-output" lang="en">
        <h1><span class="mw-headline" id="x">10/27/05 Patch Notes - Issue 6: Along Came a Spider</span></h1>
        <ul><li>Early note before any section.</li></ul>
        <div id="toc"><h2 id="mw-toc-heading">Contents</h2><ul><li>1.1 PvP Zones:</li><li>1.2 Powers:</li></ul></div>
        <h2><span class="mw-headline" id="PvP_Zones">PvP Zones</span></h2>
        <ul><li>Recluse's Victory opens.</li></ul>
        <h2><span class="mw-headline" id="Powers">Powers</span></h2>
        <h3><span class="mw-headline" id="Tankers">Tankers</span></h3>
        <ul><li>Improved Damage DeBuff of Tanker/Ice Armor/Chilling Embrace.</li>
        <li>Sonic Repulsion and Sonic Dispersion:<ul><li>Switched in pick order.</li></ul></li><li></li></ul>
        <div class="printfooter">Retrieved from</div><ul><li>ignored footer item</li></ul></div></body></html>
        """;

    private const string Base = FakeSite.Forum;
    private const string Wiki = FakeSite.Wiki;
    private const string Beta = Base + "/forum/73-open-beta-patch-notes/";
    private const string Staging = Base + "/forum/100-staging-patch-notes/";
    private const string Rc1 = Base + "/topic/1-beta-patch-notes-for-november-5th-2021-issue-27-second-chances-page-3-build-1/";
    private const string Rc2 = Base + "/topic/2-beta-patch-notes-for-november-24th-2021-issue-27-second-chances-page-3-release-candidate-2/";
    private const string InterimOld = Base + "/topic/3-patch-notes-for-august-11th-2019/";
    private const string InterimNew = Base + "/topic/4-open-beta-patch-notes-for-march-3rd-2026/";
    private const string LiveSeries = Base + "/topic/5-open-beta-patch-notes-for-july-22nd-2024-issue-28-legacy-page-1-release-candidate-2/";
    private const string Repost = Base + "/topic/8-staging-patch-notes-for-november-25th-2021/";
    private const string Categories = $"""
        <a href="{Base}/patch-notes/">Patch Notes</a>
        <a href="{Base}/patch-notes/issue-28/page-4/">Page 4</a>
        <a href="{Base}/patch-notes/issue-28/page-3/">Page 3</a>
        <a href="{Base}/patch-notes/issue-27/page-7/">Page 7</a>
        """;

    private static readonly string Smite = PatchParserTests.H("Powers", 18) + "<ul><li>Dark Melee &gt; Smite: Buffed.</li></ul>";

    public static string Thread(string title, string when, string body)
    {
        return $"<html><head><title>{title} - Open Beta Patch Notes - Homecoming</title></head><body>"
            + $"<article id='elComment_1'><a href='{Base}/profile/1-curator/' title=\"Go to The Curator's profile\"></a>"
            + $"<time datetime='{when}'></time><div data-role='commentContent'>{body}</div></article></body></html>";
    }

    [Fact]
    public void Release_notes_walk_the_categories_and_skip_written_entries()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        string blank = Base + "/patch-notes/issue-27/page-7/placeholder-r1/";
        site.Routes[Base + "/patch-notes/?show=categories"] = Categories;
        site.Routes[Base + "/patch-notes/issue-28/page-4/"] = $"<a href='{PatchParserTests.EntryUrl}'>a</a><a href='{Base}/patch-notes/issue-28/page-4/issue-28-page-4-r47/'>b</a>";
        site.Routes[Base + "/patch-notes/issue-28/page-3/"] = $"<a href='{Base}/patch-notes/issue-28/page-3/gone-r1/'>x</a>";
        site.Routes[Base + "/patch-notes/issue-27/page-7/"] = $"<a href='{blank}'>x</a>";
        site.Routes[blank] = "<html><head><title>placeholder - Page 7 - Homecoming</title></head><body></body></html>";
        site.Routes[PatchParserTests.EntryUrl] = PatchParserTests.Sample;
        site.Routes[Base + "/patch-notes/issue-28/page-4/issue-28-page-4-r47/"] = PatchParserTests.Sample;
        string[] args = ["--out", tree.Root, "--delay", "0", "--source", "release-notes"];

        (int code, string output, string error) = Run(site, args);

        Assert.Equal(0, code);
        Assert.Contains("3 entries written", output, StringComparison.Ordinal);
        Assert.Contains($"E206: no change lines parsed from {blank}", error, StringComparison.Ordinal);
        Assert.Equal(["2026-10-20-issue-28-page-4-r47.json", "2026-10-20-october-20-update-r48.json", "undated-placeholder-r1.json"], Json(tree.Root));
        int fetched = site.Count(PatchParserTests.EntryUrl);
        Assert.Contains("0 entries written", Run(site, args).Output, StringComparison.Ordinal);
        Assert.Equal(fetched, site.Count(PatchParserTests.EntryUrl));
    }

    [Fact]
    public void An_unreachable_browser_and_an_empty_canonical_tree_are_reported()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        site.Routes[Base + "/patch-notes/?show=categories"] = new HttpRequestException("boom");

        (int code, _, string error) = Run(site, "--out", tree.Root, "--delay", "0", "--source", "release-notes", "--canonical", tree.Root);

        Assert.Equal(1, code);
        Assert.Contains("E203: could not read the patch-notes category browser", error, StringComparison.Ordinal);
        Assert.Contains($"E207: no powerset names under {tree.Root}", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Beta_threads_keep_final_candidates_outside_live_coverage_and_date_lines_by_first_appearance()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        string pagination = BetaRoutes(site, tree);
        string[] args = ["--out", tree.Root, "--delay", "0", "--source", "forum-beta"];

        (int code, string output, string error) = Run(site, args);

        Assert.Equal(1, code);
        Assert.Contains("2 entries written", output, StringComparison.Ordinal);
        Assert.Contains($"E205: beta listing page unavailable: {Beta}page/3/", error, StringComparison.Ordinal);
        const string Rc2File = "2021-11-24-beta-beta-patch-notes-for-november-24th-2021-issue-27-second-chances-page-3-release-candidate-2.json";
        Assert.Contains(Rc2File, Json(tree.Root), StringComparer.Ordinal);
        Assert.Contains("2019-08-11-beta-patch-notes-for-august-11th-2019.json", Json(tree.Root), StringComparer.Ordinal);
        Assert.Equal(0, site.Count(Rc1));
        Assert.Equal(["Siphon Life", "Touch of Fear"], Changes(tree.FullPath(Rc2File)).Select(change => change["power"]));
        PyObject repost = Index(tree.Root).Single(row => Equals(row["url"], Repost));
        Assert.Equal((string.Empty, Rc2File), (repost["file"], repost["repeat_of"]));

        string late = Base + "/topic/9-patch-notes-for-january-4th-2020/";
        site.Routes[Beta + "page/3/"] = string.Empty;
        site.Routes.Remove(Staging);
        site.Routes[Beta] = pagination + Row(Rc2, "2021-11-24T00:00:00Z") + Row(late, "2020-01-04T00:00:00Z");
        site.Routes[late] = Thread("Jan 2020", "2020-01-04T10:00:00Z", Smite);
        (code, output, error) = Run(site, args);
        Assert.Equal(1, code);
        Assert.Contains("0 entries written", output, StringComparison.Ordinal);
        Assert.Contains($"E205: beta listing page unavailable: {Staging}", error, StringComparison.Ordinal);
        Assert.Equal("2019-08-11-beta-patch-notes-for-august-11th-2019.json", Index(tree.Root).Single(row => Equals(row["url"], late))["repeat_of"]);

        site.Routes[Staging] = string.Empty;
        int fetched = site.Count(Rc2);
        Assert.Equal(0, Run(site, args).Code);
        Assert.Equal(fetched, site.Count(Rc2));
    }

    [Fact]
    public void Wiki_pages_are_written_once_each_and_a_missing_index_is_reported()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        site.Routes[Wiki + "/robots.txt"] = "User-agent: *\nAllow: /\n";
        site.Routes[Wiki + "/wiki/Patch_Notes"] = WikiIndex;
        site.Routes[Wiki + "/wiki/Patch_Notes/2005-10-27"] = WikiPage;
        site.Routes[Wiki + "/wiki/Patch_Notes/2004-05-11"] = WikiPage.Replace("2005-10-27", "2004-05-11", StringComparison.Ordinal).Replace("Recluse's Victory opens.", "Grandville opens.", StringComparison.Ordinal);
        string[] args = ["--out", tree.Root, "--delay", "0", "--source", "paragonwiki"];

        Assert.Contains("2 entries written", Run(site, args).Output, StringComparison.Ordinal);
        Assert.Equal(["2004-05-11-paragonwiki-2004-05-11.json", "2005-10-27-paragonwiki-2005-10-27.json"], Json(tree.Root));
        Assert.Equal(["Recluse's Victory opens."], Changes(tree.FullPath("2005-10-27-paragonwiki-2005-10-27.json")).Select(change => change["text"]));
        Assert.Contains("0 entries written", Run(site, args).Output, StringComparison.Ordinal);

        site.Routes[Wiki + "/wiki/Patch_Notes"] = new HttpRequestException("down");
        (int code, _, string error) = Run(site, args);
        Assert.Equal(1, code);
        Assert.Contains("E204", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Reparse_rewrites_only_records_that_differ_and_reads_pages_from_the_cache()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        string[] pages = WikiRoutes(site);
        string outDir = tree.FullPath("patches");
        string[] args = ["--out", outDir, "--delay", "0", "--source", "paragonwiki"];
        Assert.Equal(0, Run(site, args).Code);
        string early = Path.Join(outDir, "2004-05-11-paragonwiki-2004-05-11.json");
        string later = Path.Join(outDir, "2005-10-27-paragonwiki-2005-10-27.json");
        PyObject record = (PyObject)PyJson.Loads(File.ReadAllText(early))!;
        record.Set("changes", new List<object?> { new Change("PvP Zones", string.Empty, string.Empty, string.Empty, string.Empty, "Recluse's Victory opens.").ToObject() });
        File.WriteAllText(early, PyJson.Dumps(record, indented: true, ensureAscii: false) + "\n");
        string stray = Path.Join(outDir, "2003-01-01-paragonwiki-gone.json");
        File.WriteAllText(stray, "{\"changes\": []}\n");
        File.AppendAllText(Path.Join(outDir, "index.jsonl"), "{\"url\": \"gone\", \"published\": \"2003-01-01\", \"source\": \"paragonwiki\", \"file\": \"2003-01-01-paragonwiki-gone.json\"}\n");
        foreach (string page in pages)
        {
            site.Routes.Remove(page);
        }

        int before = site.Calls.Count;
        (int code, string output, _) = Run(site, [.. args, "--reparse"]);

        Assert.Equal(0, code);
        Assert.DoesNotContain(site.Calls.Skip(before), pages.Contains);
        Assert.Contains("paragonwiki: 0 changed, 2 unchanged, 1 removed, 1 kept, 1 re-dated", output, StringComparison.Ordinal);
        Assert.False(File.Exists(stray));
        Assert.Equal(["Recluse's Victory opens."], Changes(early).Select(change => change["text"]));
        Assert.DoesNotContain("Recluse's Victory opens.", File.ReadAllText(later), StringComparison.Ordinal);
        Assert.Equal([pages[1], pages[0], pages[2]], Index(outDir).Select(row => row["url"]));
    }

    [Fact]
    public void Re_dating_leaves_live_release_notes_whole()
    {
        using TempTree tree = new();
        const string Line = "{\"category\": \"Powers\", \"subsection\": \"\", \"archetype\": \"\", \"powerset\": \"\", \"power\": \"\", \"text\": \"Same.\"}";
        tree.Write("a.json", $"{{\"changes\": [{Line}]}}\n");
        tree.Write("b.json", $"{{\"changes\": [{Line}]}}\n");
        tree.Write("index.jsonl", "{\"url\": \"a.json\", \"published\": \"2024-01-01\", \"source\": \"release-notes\", \"file\": \"a.json\"}\n{\"url\": \"b.json\", \"published\": \"2024-02-01\", \"source\": \"release-notes\", \"file\": \"b.json\"}\n");

        Assert.Equal(0, PatchNotesCommand.Redate(tree.Root, "release-notes"));
        Assert.Single(Changes(tree.FullPath("b.json")));
    }

    [Fact]
    public void Reparse_of_one_source_leaves_the_others_and_their_index_rows()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        WikiRoutes(site);
        string outDir = tree.FullPath("patches");
        tree.Write("patches/index.jsonl", $"{{\"url\": \"{Base}/patch-notes/issue-28/page-1/x-r22/\", \"published\": \"2023-04-25\", \"source\": \"release-notes\", \"file\": \"live.json\"}}\n");
        tree.Write("patches/live.json", "{}\n");

        (int code, string output, _) = Run(site, "--out", outDir, "--delay", "0", "--source", "paragonwiki", "--reparse");

        Assert.Equal(0, code);
        Assert.Contains("paragonwiki: 3 changed, 0 unchanged, 0 removed, 0 kept", output, StringComparison.Ordinal);
        Assert.Equal("{}\n", File.ReadAllText(Path.Join(outDir, "live.json")));
        Assert.Equal(["release-notes", "paragonwiki", "paragonwiki", "paragonwiki"], Index(outDir).Select(row => row["source"]));
    }

    [Fact]
    public void A_line_is_dated_by_its_first_appearance_and_its_origin_named()
    {
        Change first = new("Powers", string.Empty, string.Empty, "Dark Melee", "Smite", "Buffed.");
        Change fresh = first with { Power = "Siphon Life", Text = "Heals more." };
        PatchEntry entry = Entry("u", "2021-01-01", first, first, fresh);
        Dictionary<Change, (string File, string Published)> seen = new() { [first] = ("a.json", "2021-01-01") };

        Assert.Equal(string.Empty, PatchNotesCommand.DedupeLines(entry, new Dictionary<Change, (string File, string Published)>()));
        Assert.Equal([first, fresh], entry.Changes);
        PatchEntry later = Entry("u2", "2021-02-01", first, fresh);
        Assert.Equal("a.json", PatchNotesCommand.DedupeLines(later, seen));
        Assert.Equal([fresh], later.Changes);
        PatchEntry older = Entry("u3", "2020-12-01", first);
        Assert.Equal(string.Empty, PatchNotesCommand.DedupeLines(older, seen));
        Assert.Equal([first], older.Changes);
        seen[fresh] = ("b.json", "2021-01-01");
        seen[first with { Text = "x" }] = ("b.json", "2021-01-01");
        Assert.Equal("b.json", PatchNotesCommand.DedupeLines(Entry("u4", "2021-03-01", first, fresh, first with { Text = "x" }), seen));
    }

    [Fact]
    public void An_entry_that_parsed_to_nothing_is_recorded_and_reported()
    {
        using TempTree tree = new();
        using StringWriter error = new();

        Assert.Equal(1, PatchNotesCommand.WriteDated([Entry("u", "2021-01-01")], tree.Root, [], new HashSet<string>(StringComparer.Ordinal), TextWriter.Null, error));
        Assert.Equal(0, PatchNotesCommand.WriteDated([], tree.Root, [], new HashSet<string>(StringComparer.Ordinal), TextWriter.Null, error));
        Assert.Empty(Changes(Directory.GetFiles(tree.Root, "2021-01-01-*.json").Single()));
        Assert.Contains("E206: no change lines parsed from u", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--source", "nowhere", "argument --source: invalid choice: 'nowhere' (choose from release-notes, forum-beta, paragonwiki)")]
    [InlineData("--delay", "soon", "argument --delay: invalid float value: 'soon'")]
    [InlineData("--reparse=yes", "", "unrecognized arguments: --reparse=yes")]
    public void Bad_arguments_print_the_usage_and_exit_2(string name, string value, string problem)
    {
        using FakeSite site = new();

        (int code, _, string error) = Run(site, value.Length > 0 ? [name, value] : [name]);

        Assert.Equal(2, code);
        Assert.Equal(PatchNotesCommand.Usage + Environment.NewLine + "coh-engine patch-notes: error: " + problem + Environment.NewLine, error);
        Assert.Empty(site.Calls);
    }

    [Fact]
    public void Every_source_is_read_by_default_with_the_default_agent()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        site.Routes[Base + "/robots.txt"] = HttpStatusCode.Forbidden;
        site.Routes[Wiki + "/robots.txt"] = HttpStatusCode.Forbidden;

        (int code, string output, string error) = Run(site, "--out", tree.Root, "--cache", tree.FullPath("cache"), "--canonical", Path.Join(Fixtures.Store.RepositoryRoot, "data", "canonical"));

        Assert.Equal(1, code);
        Assert.Equal($"0 entries written to {tree.Root}" + Environment.NewLine, output);
        Assert.Contains("E203", error, StringComparison.Ordinal);
        Assert.Contains("E204", error, StringComparison.Ordinal);
        Assert.StartsWith("coh-build-companion/", PatchNotesCommand.DefaultAgent, StringComparison.Ordinal);
    }

    private static (int Code, string Output, string Error) Run(FakeSite site, params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int code = PatchNotesCommand.Run(args, output, error, site);
        return (code, output.ToString(), error.ToString());
    }

    private static string Row(string topic, string when)
    {
        return $"<li class='ipsDataItem'><a href='{topic}'>t</a><time datetime='{when}'></time><time datetime='{when}'></time></li>";
    }

    /// <summary>Two beta listings, a staging repost of the final candidate, and a live index from April 2023 that covers issue-28 page-1, one row in the shape written before the source and series columns.</summary>
    private static string BetaRoutes(FakeSite site, TempTree tree)
    {
        string bodyRc2 = PatchParserTests.H("Powers", 18) + "<ul><li>Dark Melee &gt; Smite: Buffed.</li><li>Dark Melee &gt; Siphon Life: Heals more.</li><li>Dark Melee &gt; Touch of Fear: Wider cone.</li></ul>";
        string gone = Base + "/topic/6-patch-notes-for-june-8th-2019/";
        string pagination = $"<ul class='ipsPagination' data-pages='3'><li><a href='{Beta}page/2/'>2</a></li></ul>";
        site.Routes[Beta] = pagination + Row(Rc1, "2021-11-05T00:00:00Z") + Row(Rc2, "2021-11-24T00:00:00Z") + Row(gone, "2019-06-08T00:00:00Z");
        site.Routes[Beta + "page/2/"] = Row(InterimOld, "2019-08-11T00:00:00Z") + Row(InterimNew, "2026-03-03T00:00:00Z") + Row(LiveSeries, "2024-07-22T00:00:00Z");
        site.Routes[Staging] = Row(Repost, "2021-11-25T00:00:00Z");
        site.Routes[Repost] = Thread("Staging repost", "2021-11-25T10:00:00Z", bodyRc2);
        site.Routes[Rc2] = Thread("RC2", "2021-11-24T10:00:00Z", bodyRc2);
        site.Routes[InterimOld] = Thread("Aug 2019", "2019-08-11T10:00:00Z", Smite);
        site.Routes[InterimNew] = Thread("Mar 2026", "2026-03-03T10:00:00Z", Smite);
        site.Routes[LiveSeries] = Thread("i28p1", "2024-07-22T10:00:00Z", Smite);
        tree.Write("index.jsonl", $"{{\"url\": \"{Base}/patch-notes/issue-28/page-1/x-r22/\", \"published\": \"2023-04-25T00:00:00Z\", \"revision\": 22, \"file\": \"x\"}}\n");
        PatchNotesCommand.WriteEntry(new PatchEntry { Title = "live2", Url = Base + "/patch-notes/issue-27/page-7/y-r11/", Published = "2024-02-20T00:00:00Z", Issue = "issue-27", Page = "page-7", Revision = 11 }, tree.Root);
        return pagination;
    }

    private static string[] WikiRoutes(FakeSite site)
    {
        site.Routes[Wiki + "/robots.txt"] = "User-agent: *\nAllow: /\n";
        site.Routes[Wiki + "/wiki/Patch_Notes"] = WikiIndex;
        string[] pages = [Wiki + "/wiki/Patch_Notes/2005-10-27", Wiki + "/wiki/Patch_Notes/2004-05-11", Wiki + "/wiki/Patch_Notes/2012-08-23_Beta"];
        site.Routes[pages[0]] = WikiPage;
        site.Routes[pages[1]] = "<html><head><title>Patch Notes/2004-05-11</title></head><body></body></html>";
        site.Routes[pages[2]] = "<html><head><title>Patch Notes/2012-08-23 Beta</title></head><body></body></html>";
        return pages;
    }

    private static PatchEntry Entry(string url, string published, params Change[] changes)
    {
        return new PatchEntry { Title = "t", Url = url, Published = published, Issue = string.Empty, Page = string.Empty, Source = "forum-beta", Changes = [.. changes] };
    }

    private static List<string> Json(string folder)
    {
        return [.. Directory.GetFiles(folder, "*.json").Select(file => Path.GetFileName(file)).Order(StringComparer.Ordinal)];
    }

    private static List<PyObject> Index(string folder)
    {
        return PatchNotesCommand.Index(folder);
    }

    private static IEnumerable<PyObject> Changes(string file)
    {
        return ((List<object?>)((PyObject)PyJson.Loads(File.ReadAllText(file))!)["changes"]!).Cast<PyObject>();
    }
}
