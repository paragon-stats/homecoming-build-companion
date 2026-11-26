using System.Net;

using CohEngine.Core.Chain;
using CohEngine.Core.PatchNotes;
using CohEngine.Core.Tests.Diagnostics;
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

        Assert.Equal(1, code);
        Assert.Contains("error: E208: release-notes was not read completely", error, StringComparison.Ordinal);
        Assert.Contains("3 entries written", output, StringComparison.Ordinal);
        Assert.Contains($"error: E206: no change lines parsed from {blank}", error, StringComparison.Ordinal);
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
        Assert.Contains("error: E203: could not read the patch-notes category browser", error, StringComparison.Ordinal);
        Assert.Contains($"error: E207: no powerset names under {tree.Root}; headings will not fill powerset; pass --canonical with the folder that holds powers/", error, StringComparison.Ordinal);
        Assert.Contains("; no release notes were read; re-run when the forum answers", error, StringComparison.Ordinal);
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
        Assert.Contains($"error: E205: beta listing page unavailable: {Beta}page/3/", error, StringComparison.Ordinal);
        const string Rc2File = "2021-11-24-beta-beta-patch-notes-for-november-24th-2021-issue-27-second-chances-page-3-release-candidate-2.json";
        Assert.Contains(Rc2File, Json(tree.Root), StringComparer.Ordinal);
        Assert.Contains("2019-08-11-beta-patch-notes-for-august-11th-2019.json", Json(tree.Root), StringComparer.Ordinal);
        Assert.Equal(0, site.Count(Rc1));
        Assert.Equal(["Siphon Life", "Touch of Fear"], Changes(tree.FullPath(Rc2File)).Select(change => change["power"]));
        PyObject repost = Index(tree.Root).Single(row => Equals(row["url"], Repost));
        Assert.Equal((string.Empty, Rc2File), (repost["file"], repost["repeat_of"]));

        string late = Base + "/topic/9-patch-notes-for-january-4th-2020/";
        site.Routes[Beta + "page/3/"] = Row(InterimOld, "2019-08-11T00:00:00Z");
        site.Routes.Remove(Staging);
        site.Routes[Beta] = pagination + Row(Rc2, "2021-11-24T00:00:00Z") + Row(late, "2020-01-04T00:00:00Z");
        site.Routes[late] = Thread("Jan 2020", "2020-01-04T10:00:00Z", Smite);
        (code, output, error) = Run(site, args);
        Assert.Equal(1, code);
        Assert.Contains("0 entries written", output, StringComparison.Ordinal);
        Assert.Contains($"error: E205: beta listing page unavailable: {Staging}", error, StringComparison.Ordinal);
        Assert.Equal("2019-08-11-beta-patch-notes-for-august-11th-2019.json", Index(tree.Root).Single(row => Equals(row["url"], late))["repeat_of"]);

        site.Routes[Staging] = Row(Repost, "2021-11-25T00:00:00Z");
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
        Assert.Contains("error: E204: could not read the wiki patch-notes index at https://archive.paragonwiki.com/wiki/Patch_Notes; no wiki pages were read; re-run when the wiki answers", error, StringComparison.Ordinal);
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
    public void Reparse_leaves_a_source_it_could_not_read_completely_as_it_was()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        string[] pages = WikiRoutes(site);
        string outDir = tree.FullPath("patches");
        Assert.Equal(0, Run(site, "--out", outDir, "--delay", "0", "--source", "paragonwiki").Code);
        string index = File.ReadAllText(Path.Join(outDir, "index.jsonl"));
        List<string> records = Json(outDir);
        Directory.Delete(Path.Join(outDir, ".cache"), recursive: true);
        site.Routes.Remove(pages[0]);

        (int code, string output, string error) = Run(site, "--out", outDir, "--delay", "0", "--source", "paragonwiki", "--reparse");

        Assert.Equal(1, code);
        Assert.Contains("error: E208: paragonwiki was not read completely", error, StringComparison.Ordinal);
        Assert.DoesNotContain("paragonwiki:", output, StringComparison.Ordinal);
        Assert.Equal(index, File.ReadAllText(Path.Join(outDir, "index.jsonl")));
        Assert.Equal(records, Json(outDir));
    }

    [Fact]
    public void Reparse_keeps_the_release_notes_when_the_category_browser_cannot_be_read()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        tree.Write("index.jsonl", "{\"url\": \"u\", \"published\": \"2024-01-01\", \"source\": \"release-notes\", \"file\": \"a.json\"}\n");
        tree.Write("a.json", "{\"changes\": []}\n");
        site.Routes[Base + "/patch-notes/?show=categories"] = HttpStatusCode.ServiceUnavailable;

        (int code, _, string error) = Run(site, "--out", tree.Root, "--delay", "0", "--source", "release-notes", "--reparse");

        Assert.Equal(1, code);
        Assert.Contains("error: E208: release-notes was not read completely", error, StringComparison.Ordinal);
        Assert.True(File.Exists(tree.FullPath("a.json")));
        Assert.Single(Index(tree.Root));
    }

    [Theory]
    [InlineData("release-notes", Base + "/patch-notes/?show=categories")]
    [InlineData("paragonwiki", Wiki + "/wiki/Patch_Notes")]
    public void Reparse_keeps_a_source_whose_listing_answers_with_no_links(string source, string listing)
    {
        using TempTree tree = new();
        using FakeSite site = new();
        site.Routes[Wiki + "/robots.txt"] = "User-agent: *\nAllow: /\n";
        site.Routes[listing] = "<html><body>Down for maintenance</body></html>";
        string index = $"{{\"url\": \"u\", \"published\": \"2024-01-01\", \"source\": \"{source}\", \"file\": \"a.json\"}}\n";
        tree.Write("index.jsonl", index);
        tree.Write("a.json", "{\"changes\": []}\n");

        (int code, _, string error) = Run(site, "--out", tree.Root, "--delay", "0", "--source", source, "--reparse");

        Assert.Equal(1, code);
        Assert.Contains($"error: E227: {listing} answered but names no", error, StringComparison.Ordinal);
        Assert.Contains($"error: E208: {source} was not read completely", error, StringComparison.Ordinal);
        Assert.True(File.Exists(tree.FullPath("a.json")));
        Assert.Equal(index, File.ReadAllText(tree.FullPath("index.jsonl")));
    }

    [Fact]
    public void A_listing_that_declares_too_many_pages_is_read_to_the_cap_only()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        site.Routes[Beta] = $"<ul class='ipsPagination' data-pages='100000'><li><a href='{Beta}page/2/'>2</a></li></ul>" + Row(InterimOld, "2019-08-11T00:00:00Z");
        site.Routes[Staging] = Row(InterimOld, "2019-08-11T00:00:00Z");
        site.Routes[InterimOld] = Thread("Aug 2019", "2019-08-11T10:00:00Z", Smite);

        (int code, _, string error) = Run(site, "--out", tree.Root, "--delay", "0", "--source", "forum-beta");

        Assert.Equal(1, code);
        Assert.Contains($"error: E226: {Beta} declares 100000 listing pages, more than the 100 read", error, StringComparison.Ordinal);
        Assert.Contains(site.Calls, call => call.EndsWith("/page/100/", StringComparison.Ordinal));
        Assert.DoesNotContain(site.Calls, call => call.EndsWith("/page/101/", StringComparison.Ordinal));
    }

    [Fact]
    public void Beta_sections_that_list_no_threads_fail_the_run()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        site.Routes[Beta] = "<html><body>Down for maintenance</body></html>";
        site.Routes[Staging] = "<html><body>Down for maintenance</body></html>";

        (int code, _, string error) = Run(site, "--out", tree.Root, "--delay", "0", "--source", "forum-beta");

        Assert.Equal(1, code);
        Assert.Contains($"error: E227: {Beta} answered but names no", error, StringComparison.Ordinal);
        Assert.Contains("error: E208: forum-beta was not read completely", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Reparse_keeps_a_source_whose_fresh_records_are_empty_while_the_folder_holds_some()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        site.Routes[Beta] = Row(Rc2, "2021-11-24T00:00:00Z");
        site.Routes[Staging] = Row(Rc2, "2021-11-24T00:00:00Z");
        site.Routes[Rc2] = Thread("RC2", "2021-11-24T10:00:00Z", Smite);
        string index = "{\"url\": \"live\", \"published\": \"2020-01-01\", \"source\": \"release-notes\", \"issue\": \"issue-27\", \"page\": \"page-3\", \"file\": \"live.json\"}\n"
            + "{\"url\": \"old\", \"published\": \"2019-01-01\", \"source\": \"forum-beta\", \"file\": \"b.json\"}\n";
        tree.Write("index.jsonl", index);
        tree.Write("live.json", "{\"changes\": []}\n");
        tree.Write("b.json", "{\"changes\": []}\n");

        (int code, _, string error) = Run(site, "--out", tree.Root, "--delay", "0", "--source", "forum-beta", "--reparse");

        Assert.Equal(1, code);
        Assert.Contains("error: E208: forum-beta was not read completely", error, StringComparison.Ordinal);
        Assert.True(File.Exists(tree.FullPath("b.json")));
        Assert.Equal(index, File.ReadAllText(tree.FullPath("index.jsonl")));
    }

    [Fact]
    public void A_failure_syncing_a_later_source_deletes_no_file_the_index_still_names()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        WikiRoutes(site);
        site.Routes[Base + "/patch-notes/?show=categories"] = $"<a href=\"{Base}/patch-notes/issue-28/page-4/\">x</a>";
        site.Routes[Base + "/patch-notes/issue-28/page-4/"] = $"<a href='{PatchParserTests.EntryUrl}'>a</a>";
        site.Routes[PatchParserTests.EntryUrl] = PatchParserTests.Sample;
        string stray = tree.Write("2003-01-01-paragonwiki-gone.json", "{\"changes\": []}\n");
        string index = "{\"url\": \"gone\", \"published\": \"2003-01-01\", \"source\": \"paragonwiki\", \"file\": \"2003-01-01-paragonwiki-gone.json\"}\n";
        tree.Write("index.jsonl", index);
        Directory.CreateDirectory(tree.FullPath("2026-10-20-october-20-update-r48.json"));

        (int code, _, string error) = Run(site, "--out", tree.Root, "--delay", "0", "--source", "paragonwiki", "--source", "release-notes", "--reparse");

        Assert.Equal(1, code);
        Assert.Contains("error: E224", error, StringComparison.Ordinal);
        Assert.Equal(index, File.ReadAllText(tree.FullPath("index.jsonl")));
        Assert.True(File.Exists(stray));
    }

    [Fact]
    public void A_listing_or_entry_page_that_cannot_be_read_fails_the_run()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        site.Routes[Base + "/patch-notes/?show=categories"] = Categories;
        site.Routes[Base + "/patch-notes/issue-28/page-4/"] = $"<a href='{PatchParserTests.EntryUrl}'>a</a>";

        (int code, string output, string error) = Run(site, "--out", tree.Root, "--delay", "0", "--source", "release-notes");

        Assert.Equal(1, code);
        Assert.Contains("0 entries written", output, StringComparison.Ordinal);
        Assert.Contains("error: E208: release-notes was not read completely", error, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_whose_name_would_leave_the_output_folder_is_refused()
    {
        using TempTree tree = new();
        string outDir = Directory.CreateDirectory(tree.FullPath("patches")).FullName;
        PatchEntry entry = Entry(Base + "/topic/7-x/", "..\\..\\escape");

        Assert.StartsWith("E209: entry", Assert.Throws<CohEngine.Core.Diagnostics.EngineException>(() => PatchNotesCommand.WriteEntry(entry, outDir)).Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(tree.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void A_beta_thread_dated_with_a_path_is_skipped_and_the_rest_is_written()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        string outDir = Directory.CreateDirectory(tree.FullPath("patches")).FullName;
        site.Routes[Beta] = Row(InterimOld, "2019-08-11T00:00:00Z") + Row(InterimNew, "2020-03-03T00:00:00Z");
        site.Routes[Staging] = Row(InterimNew, "2020-03-03T00:00:00Z");
        site.Routes[InterimOld] = Thread("Aug 2019", "../../escape", Smite);
        site.Routes[InterimNew] = Thread("Mar 2020", "2020-03-03T10:00:00Z", Smite);

        (int code, string output, string error) = Run(site, "--out", outDir, "--delay", "0", "--source", "forum-beta");

        Assert.Equal(1, code);
        Assert.Contains("error: E209: entry " + InterimOld, error, StringComparison.Ordinal);
        Assert.Contains("1 entries written", output, StringComparison.Ordinal);
        Assert.Equal(["2020-03-03-beta-open-beta-patch-notes-for-march-3rd-2026.json"], Json(outDir));
        Assert.Empty(Directory.GetFiles(tree.Root, "*.json"));
    }

    [Fact]
    public void A_release_note_dated_with_a_path_is_skipped()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        string outDir = Directory.CreateDirectory(tree.FullPath("patches")).FullName;
        site.Routes[Base + "/patch-notes/?show=categories"] = $"<a href=\"{Base}/patch-notes/issue-28/page-4/\">x</a>";
        site.Routes[Base + "/patch-notes/issue-28/page-4/"] = $"<a href='{PatchParserTests.EntryUrl}'>a</a>";
        site.Routes[PatchParserTests.EntryUrl] = "<html><head><title>x - Page 4 - Homecoming</title></head><body><time datetime='..\\..\\escape'></time></body></html>";

        (int code, string output, string error) = Run(site, "--out", outDir, "--delay", "0", "--source", "release-notes");

        Assert.Equal(1, code);
        Assert.Contains("error: E209: entry " + PatchParserTests.EntryUrl, error, StringComparison.Ordinal);
        Assert.Contains("0 entries written", output, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(tree.Root, "*.json", SearchOption.AllDirectories));
    }

    [Fact]
    public void Reparse_removes_no_file_an_index_row_names_outside_the_output_folder()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        WikiRoutes(site);
        string keep = tree.Write("keep.json", "{}\n");
        tree.Write("patches/index.jsonl", "{\"url\": \"gone\", \"published\": \"2003-01-01\", \"source\": \"paragonwiki\", \"file\": \"..\\\\keep.json\"}\n");

        (int code, _, string error) = Run(site, "--out", tree.FullPath("patches"), "--delay", "0", "--source", "paragonwiki", "--reparse");

        Assert.Equal(0, code);
        Assert.True(File.Exists(keep));
        Assert.Contains($"error: E209: index row gone names '..\\keep.json', which is not a plain file name inside {tree.FullPath("patches")}; the row was dropped from the index and the file it names was not deleted; delete that file by hand if it is no longer wanted" + Environment.NewLine, error, StringComparison.Ordinal);
        Assert.DoesNotContain("\"gone\"", File.ReadAllText(tree.FullPath("patches/index.jsonl")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_too_slow_to_parse_stops_its_source_with_a_coded_error()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        site.Routes[Base + "/patch-notes/?show=categories"] = $"<a href=\"{Base}/patch-notes/issue-28/page-4/\">x</a>";
        site.Routes[Base + "/patch-notes/issue-28/page-4/"] = $"<a href='{PatchParserTests.EntryUrl}'>a</a>";
        site.Routes[PatchParserTests.EntryUrl] = "<section class='ipsType_richText'>" + string.Concat(Enumerable.Repeat("<p>", 200_000)) + "</section>";
        site.Routes[Wiki + "/robots.txt"] = HttpStatusCode.Forbidden;

        (int code, _, string error) = Run(site, "--out", tree.Root, "--delay", "0", "--source", "release-notes", "--source", "paragonwiki");

        Assert.Equal(1, code);
        Assert.Contains("error: E223: a page from release-notes took too long to parse", error, StringComparison.Ordinal);
        Assert.Contains("error: E204", error, StringComparison.Ordinal);
    }

    [Fact]
    public void An_output_folder_that_cannot_be_written_is_a_write_error()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        string file = tree.Write("taken", "x");

        (int code, _, string error) = Run(site, "--out", file, "--delay", "0", "--source", "release-notes");

        Assert.Equal(1, code);
        Assert.Contains($"error: E224: cannot read or write under {file}: ", error, StringComparison.Ordinal);
        Assert.Contains("; check that the folder and its cache can be written and the disk has room", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_network_layer_the_caller_passes_in_is_not_disposed()
    {
        using TempTree tree = new();
        using FakeSite site = new();

        Run(site, "--out", tree.Root, "--delay", "0", "--source", "release-notes");

        Assert.False(site.Disposed);
    }

    [Theory]
    [InlineData("[1, 2]")]
    [InlineData("{\"file\": \"a.json\"}")]
    [InlineData("{not json")]
    public void An_index_line_that_is_not_a_row_is_a_coded_error_naming_the_file(string line)
    {
        using TempTree tree = new();
        tree.Write("index.jsonl", "{\"url\": \"u\"}\n" + line + "\n");

        string message = Assert.Throws<CohEngine.Core.Diagnostics.EngineException>(() => PatchNotesCommand.Index(tree.Root)).Message;

        Assert.StartsWith($"E225: line 2 of {Path.Join(tree.Root, "index.jsonl")} is not an index row", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"changes\": [1]}")]
    [InlineData("{\"changes\": \"none\"}")]
    [InlineData("[]")]
    [InlineData("{\"changes\": [{\"category\": \"Powers\"}]}")]
    public void A_record_that_is_not_a_patch_entry_is_a_coded_error_naming_the_file(string record)
    {
        using TempTree tree = new();
        tree.Write("index.jsonl", "{\"url\": \"u\", \"published\": \"2024-01-01\", \"source\": \"paragonwiki\", \"file\": \"a.json\"}\n");
        tree.Write("a.json", record + "\n");

        string message = Assert.Throws<CohEngine.Core.Diagnostics.EngineException>(() => PatchNotesCommand.Redate(tree.Root, "paragonwiki")).Message;

        Assert.StartsWith($"E225: {Path.Join(tree.Root, "a.json")} is not a patch-notes record", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_that_is_not_utf8_is_a_coded_error_naming_the_file()
    {
        using TempTree tree = new();
        tree.Write("index.jsonl", "{\"url\": \"u\", \"published\": \"2024-01-01\", \"source\": \"paragonwiki\", \"file\": \"a.json\"}\n");
        File.WriteAllBytes(tree.FullPath("a.json"), [(byte)'{', 0xff, (byte)'}']);

        string message = Assert.Throws<CohEngine.Core.Diagnostics.EngineException>(() => PatchNotesCommand.Redate(tree.Root, "paragonwiki")).Message;

        Assert.StartsWith($"E225: {tree.FullPath("a.json")} is not valid UTF-8", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(':')]
    [InlineData('*')]
    [InlineData('?')]
    [InlineData('"')]
    [InlineData('<')]
    [InlineData('>')]
    [InlineData('|')]
    [InlineData('\u0001')]
    public void A_slug_with_a_character_windows_refuses_is_refused_on_every_system(char letter)
    {
        using TempTree tree = new();
        PatchEntry entry = Entry(Base + "/topic/7-a" + letter + "b/", "2021-01-01");

        Assert.StartsWith("E209: entry", Assert.Throws<CohEngine.Core.Diagnostics.EngineException>(() => PatchNotesCommand.WriteEntry(entry, tree.Root)).Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(tree.Root));
    }

    [Fact]
    public void A_slug_too_long_for_a_file_name_is_refused()
    {
        using TempTree tree = new();
        PatchEntry entry = Entry(Base + "/topic/" + new string('a', 300) + "/", "2021-01-01");

        Assert.StartsWith("E209: entry", Assert.Throws<CohEngine.Core.Diagnostics.EngineException>(() => PatchNotesCommand.WriteEntry(entry, tree.Root)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reparse_does_not_remove_a_record_it_just_wrote_under_a_name_differing_only_in_case()
    {
        using TempTree tree = new();
        using FakeSite site = new();
        WikiRoutes(site);
        string written = tree.FullPath("2005-10-27-paragonwiki-2005-10-27.json");
        tree.Write("2005-10-27-PARAGONWIKI-2005-10-27.json", "{\"changes\": []}\n");
        tree.Write("index.jsonl", $"{{\"url\": \"{Wiki}/wiki/Patch_Notes/2005-10-27\", \"published\": \"2005-10-27\", \"source\": \"paragonwiki\", \"file\": \"2005-10-27-PARAGONWIKI-2005-10-27.json\"}}\n");

        Assert.Equal(0, Run(site, "--out", tree.Root, "--delay", "0", "--source", "paragonwiki", "--reparse").Code);

        Assert.True(File.Exists(written));
        Assert.Contains("Recluse's Victory opens.", File.ReadAllText(written), StringComparison.Ordinal);
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

        Assert.Equal((1, true), PatchNotesCommand.WriteDated([Entry("u", "2021-01-01")], tree.Root, [], new HashSet<string>(StringComparer.Ordinal), TextWriter.Null, error));
        Assert.Equal((0, true), PatchNotesCommand.WriteDated([], tree.Root, [], new HashSet<string>(StringComparer.Ordinal), TextWriter.Null, error));
        Assert.Empty(Changes(Directory.GetFiles(tree.Root, "2021-01-01-*.json").Single()));
        Assert.Contains("error: E206: no change lines parsed from u", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--source", "nowhere", "argument --source: invalid choice: 'nowhere' (choose from release-notes, forum-beta, paragonwiki)")]
    [InlineData("--delay", "soon", "argument --delay: invalid float value: 'soon'")]
    [InlineData("--delay", "Infinity", "argument --delay: not a number of seconds from 0 to 60: 'Infinity'")]
    [InlineData("--delay", "NaN", "argument --delay: not a number of seconds from 0 to 60: 'NaN'")]
    [InlineData("--delay", "1e300", "argument --delay: not a number of seconds from 0 to 60: '1e300'")]
    [InlineData("--delay", "3e6", "argument --delay: not a number of seconds from 0 to 60: '3e6'")]
    [InlineData("--delay", "-1", "argument --delay: not a number of seconds from 0 to 60: '-1'")]
    [InlineData("--delay", "60.0001", "argument --delay: not a number of seconds from 0 to 60: '60.0001'")]
    [InlineData("--reparse=yes", "", "unrecognized arguments: --reparse=yes")]
    public void Bad_arguments_print_the_usage_and_exit_2(string name, string value, string problem)
    {
        using FakeSite site = new();

        (int code, _, string error) = Run(site, value.Length > 0 ? [name, value] : [name]);

        Assert.Equal(2, code);
        Assert.Equal(PatchNotesCommand.Usage + Environment.NewLine + "coh-engine patch-notes: error: " + problem + Environment.NewLine, error);
        Assert.Empty(site.Calls);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("60")]
    public void A_delay_from_0_to_60_seconds_is_accepted(string delay)
    {
        using TempTree tree = new();
        using FakeSite site = new();

        // One request only, robots.txt refused, so no gap is ever waited out.
        site.Routes[Wiki + "/robots.txt"] = HttpStatusCode.Forbidden;

        (int code, _, string error) = Run(site, "--out", tree.Root, "--delay", delay, "--source", "paragonwiki");

        Assert.Equal(1, code);
        Assert.DoesNotContain("usage:", error, StringComparison.Ordinal);
        Assert.Contains("error: E201: robots.txt refused (403)", error, StringComparison.Ordinal);
        Assert.Single(site.Calls);
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
        Assert.Contains("error: E203", error, StringComparison.Ordinal);
        Assert.Contains("error: E204", error, StringComparison.Ordinal);
        Assert.StartsWith("coh-build-companion/", PatchNotesCommand.DefaultAgent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_coded_line_opens_with_error(bool robotsRefused)
    {
        using TempTree tree = new();
        using FakeSite site = new();
        if (robotsRefused)
        {
            site.Routes[Base + "/robots.txt"] = HttpStatusCode.Forbidden;
            site.Routes[Wiki + "/robots.txt"] = HttpStatusCode.Forbidden;
        }

        (_, _, string error) = Run(site, "--out", tree.Root, "--delay", "0", "--canonical", tree.FullPath("none"));

        Assert.True(CodedOutput.AssertLabelled(error).Length >= 5, error);
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
