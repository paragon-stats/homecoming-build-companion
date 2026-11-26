using CohEngine.Core.Chain;
using CohEngine.Core.PatchNotes;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.PatchNotes;

// tests/fixtures/patch-notes/expected/ holds what `write_entry(parse_entry(...))` in scripts/forum_scrape/patch_notes.py
// wrote for patch_entry_sample.html, with the powerset names of data/canonical.
public sealed class PatchParserTests
{
    public const string Base = FakeSite.Forum;

    public const string EntryUrl = Base + "/patch-notes/issue-28/page-4/october-20-update-r48/";

    private static readonly string Folder = Path.Join(Fixtures.Store.Root, "patch-notes");

    private static readonly HashSet<string> NoSets = [];

    public static string Sample => File.ReadAllText(Path.Join(Folder, "patch_entry_sample.html"));

    public static string H(string label, int fontPx = 0)
    {
        string size = fontPx > 0 ? $" style=\"font-size:{fontPx.ToString(System.Globalization.CultureInfo.InvariantCulture)}px;\"" : string.Empty;
        return $"<p><span{size}><strong>{label}</strong></span></p>";
    }

    [Fact]
    public void The_sample_entry_parses_to_the_json_and_index_row_the_python_wrote()
    {
        using TempTree tree = new();
        PatchEntry entry = PatchParser.ParseEntry(Sample, EntryUrl, PatchNotesCommand.Powersets(Path.Join(Fixtures.Store.RepositoryRoot, "data", "canonical")));

        string name = PatchNotesCommand.WriteEntry(entry, tree.Root);

        Assert.Equal("2026-10-20-october-20-update-r48.json", name);
        Assert.Equal(File.ReadAllBytes(Path.Join(Folder, "expected", name)), File.ReadAllBytes(tree.FullPath(name)));
        Assert.Equal(File.ReadAllBytes(Path.Join(Folder, "expected", "index.jsonl")), File.ReadAllBytes(tree.FullPath("index.jsonl")));
    }

    [Fact]
    public void Categories_and_entries_are_listed_once_in_page_order()
    {
        string categories = $"<a href=\"{Base}/patch-notes/issue-28/page-4/\">x</a><a href=\"{Base}/patch-notes/issue-28/\">x</a><a href=\"{Base}/patch-notes/issue-27/page-7/\">x</a><a href=\"{Base}/patch-notes/issue-28/page-4/\">x</a>";
        string listing = $"<a href='{EntryUrl}'>a</a><a href='{Base}/patch-notes/issue-28/page-4/issue-28-page-4-r47/'>b</a><a href='{EntryUrl}'>c</a>";

        Assert.Equal([$"{Base}/patch-notes/issue-28/page-4/", $"{Base}/patch-notes/issue-27/page-7/"], PatchParser.ListCategories(categories));
        Assert.Equal([EntryUrl, $"{Base}/patch-notes/issue-28/page-4/issue-28-page-4-r47/"], PatchParser.ListEntries(listing));
    }

    [Fact]
    public void An_entry_reads_its_date_hierarchy_and_power_paths()
    {
        PatchEntry entry = PatchParser.ParseEntry(Sample, EntryUrl, NoSets);

        Assert.Equal(("October 20 Update", "2026-10-20T14:05:01Z", "issue-28", "page-4", 48L, "release-notes"), (entry.Title, entry.Published, entry.Issue, entry.Page, entry.Revision, entry.Source));
        Change clarity = entry.Changes.First(change => change.Power is "Clarity");
        Assert.Equal(("Powers", "Adjustments", "All", "Sonic Resonance"), (clarity.Category, clarity.Subsection, clarity.Archetype, clarity.Powerset));
        Assert.True(entry.Changes.Count >= 40);
        PatchEntry empty = PatchParser.ParseEntry("<html><head><title>x - Page 4 - Homecoming</title></head></html>", Base + "/elsewhere/", NoSets);
        Assert.Equal(("x", string.Empty, string.Empty, 0L, 0), (empty.Title, empty.Published, empty.Issue, empty.Revision, empty.Changes.Count));
    }

    [Fact]
    public void Zero_width_marks_go_and_unicode_spaces_fold()
    {
        List<Change> changes = PatchParser.ParseChanges(H("Powers", 18) + "<ul><li>This\ufeff patch,\u00a0fingers\u2009crossed, ships.</li></ul>", NoSets);

        Assert.Equal("This patch, fingers crossed, ships.", changes[0].Text);
    }

    [Fact]
    public void Items_before_any_heading_file_under_general_and_prose_is_no_heading()
    {
        List<Change> changes = PatchParser.ParseChanges("<ul><li>orphan</li></ul><p>prose, not a heading</p><p></p>" + H("System", 18) + "<ul><li>Fixed a thing.</li></ul>", NoSets);

        Assert.Equal([("General", "orphan"), ("System", "Fixed a thing.")], changes.Select(change => (change.Category, change.Text)));
    }

    [Fact]
    public void The_older_layouts_read_too()
    {
        List<Change> changes = PatchParser.ParseChanges(
            H("Winter Event", 16) + "<ul><li><p>Ambushes spawn at player level.</p><ul><li><p><em><strong>Elemental Guardian:</strong> 20% defense to Fire.</em></p></li></ul></li></ul>"
            + H("Powers", 18)
            + "<ul><li>Arsenal Control &gt; Auto Turret: No longer has teleport protection</li><li>Epic &gt; Arsenal Mastery &gt; LRM: Range reduced from 150ft to 80ft</li>"
            + "<li><strong>Energy Mastery &gt; Energy Torrent: </strong>Fixed hit FX.</li><li></li></ul>",
            NoSets);

        Assert.Equal(5, changes.Count);
        Assert.Equal("Elemental Guardian: 20% defense to Fire.", changes[1].Text);
        Assert.Equal(("Winter Event", string.Empty), (changes[1].Category, changes[1].Subsection));
        Assert.Equal((string.Empty, "Arsenal Control", "Auto Turret"), (changes[2].Archetype, changes[2].Powerset, changes[2].Power));
        Assert.Equal(("Epic", "Arsenal Mastery", "LRM"), (changes[3].Archetype, changes[3].Powerset, changes[3].Power));
        Assert.Equal(("Energy Mastery", "Energy Torrent", "Fixed hit FX."), (changes[4].Powerset, changes[4].Power, changes[4].Text));
    }

    [Fact]
    public void Beta_headings_name_powersets_only_when_the_canonical_tree_does()
    {
        string body = H("RELEASE CANDIDATE 3 CHANGES:", 20) + H("Powers", 18) + H("Adjustments", 16) + H("Light Affinity,", 16) + H("Radiance")
            + "<ul><li>Recharge reduced to 8s.</li></ul>" + H("Blinding Beam") + "<ul><li>Now a cone.</li></ul>"
            + H("Travel Power Stacking", 16) + "<ul><li>Travel powers no longer stack.</li></ul>" + H("Fixes", 16) + "<ul><li>Fixed a typo.</li></ul>"
            + H("Costumes", 18) + H("New Costume Parts (62)", 16) + "<ul><li>Male &gt; Head &gt; Detail 1 &gt; Blackwing</li></ul>";

        List<Change> changes = PatchParser.ParseChanges(body, new HashSet<string>(["light affinity"], StringComparer.Ordinal));

        Assert.Equal(("Powers", "Adjustments", "Light Affinity", "Radiance", "Recharge reduced to 8s."), (changes[0].Category, changes[0].Subsection, changes[0].Powerset, changes[0].Power, changes[0].Text));
        Assert.Equal(("Light Affinity", "Blinding Beam"), (changes[1].Powerset, changes[1].Power));
        Assert.Equal(("Travel Power Stacking", string.Empty), (changes[2].Subsection, changes[2].Powerset));
        Assert.Equal("Fixes", changes[3].Subsection);
        Assert.Equal(("Costumes", "New Costume Parts (62)"), (changes[4].Category, changes[4].Subsection));
        Assert.Equal([string.Empty], PatchParser.ParseChanges(body, NoSets).Select(change => change.Powerset).Distinct(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void A_beta_series_keeps_its_latest_thread_and_each_run_of_candidates_its_last()
    {
        string rc1 = Base + "/topic/1-beta-patch-notes-for-november-5th-2021-issue-27-second-chances-page-3-build-1/";
        string rc2 = Base + "/topic/2-beta-patch-notes-for-november-24th-2021-issue-27-second-chances-page-3-release-candidate-2/";
        string interim = Base + "/topic/3-patch-notes-for-august-11th-2019/";
        string rc0 = Base + "/topic/0-staging-patch-notes-issue-27-page-3-build-0/";
        string[] runA = [Base + "/topic/11-patch-notes-for-august-21th-2019-release-candidate-1/", Base + "/topic/12-patch-notes-for-august-22th-2019-release-candidate-2/", Base + "/topic/13-patch-notes-for-august-23th-2019-release-candidate-3/"];
        string[] runB = [Base + "/topic/21-patch-notes-for-february-11th-2020-release-candidate-1/", Base + "/topic/22-patch-notes-for-february-12th-2020-release-candidate-2/"];
        string lone = Base + "/topic/odd-patch-notes-release-candidate-2/";

        Assert.Equal(("issue-27", "page-3"), PatchParser.SeriesOf(rc2));
        Assert.Equal((string.Empty, string.Empty), PatchParser.SeriesOf(interim));
        Assert.Equal([rc2, lone, runA[2], runB[1], interim], PatchParser.BetaCandidates([rc1, rc2, interim, rc0, .. runA, .. runB, lone]));
    }

    [Fact]
    public void A_beta_thread_reads_its_opener()
    {
        string rc2 = Base + "/topic/2-beta-patch-notes-for-november-24th-2021-issue-27-second-chances-page-3-release-candidate-2/";
        PatchEntry entry = PatchParser.ParseBetaThread(PatchNotesCommandTests.Thread("Beta notes", "2021-11-24T10:00:00Z", H("Powers", 18) + "<ul><li>Dark Melee &gt; Smite: Buffed.</li></ul>"), rc2, NoSets);

        Assert.Equal(("forum-beta", "issue-27", "page-3", "2021-11-24T10:00:00Z", "Smite"), (entry.Source, entry.Issue, entry.Page, entry.Published, entry.Changes[0].Power));
        Assert.Empty(PatchParser.ParseBetaThread("<html><head><title>e - s - Homecoming</title></head></html>", rc2, NoSets).Changes);
        Assert.Equal("beta-beta-patch-notes-for-november-24th-2021-issue-27-second-chances-page-3-release-candidate-2", PatchParser.Slug(entry));
    }

    [Fact]
    public void Wiki_pages_are_listed_once_oldest_first()
    {
        Assert.Equal(
            [FakeSite.Wiki + "/wiki/Patch_Notes/2004-05-11", FakeSite.Wiki + "/wiki/Patch_Notes/2005-10-27", FakeSite.Wiki + "/wiki/Patch_Notes/2012-08-23_Beta"],
            PatchParser.WikiPages(PatchNotesCommandTests.WikiIndex));
    }

    [Fact]
    public void A_wiki_page_reads_its_headings_items_and_issue()
    {
        PatchEntry entry = PatchParser.ParseWikiPage(PatchNotesCommandTests.WikiPage, FakeSite.Wiki + "/wiki/Patch_Notes/2005-10-27");

        Assert.Equal(("paragonwiki", "2005-10-27", "issue-6", "Patch Notes/2005-10-27"), (entry.Source, entry.Published, entry.Issue, entry.Title));
        Assert.Equal(("General", string.Empty, "Early note before any section."), Row(entry.Changes[0]));
        Assert.Equal(("PvP Zones", string.Empty, "Recluse's Victory opens."), Row(entry.Changes[1]));
        Assert.Equal(("Powers", "Tankers", "Improved Damage DeBuff of Tanker/Ice Armor/Chilling Embrace."), Row(entry.Changes[2]));
        Assert.Equal("Sonic Repulsion and Sonic Dispersion:", entry.Changes[3].Text);
        Assert.Equal("Switched in pick order.", entry.Changes[4].Text);
        Assert.Equal(5, entry.Changes.Count);
        Assert.Equal("paragonwiki-2005-10-27", PatchParser.Slug(entry));
        Assert.Empty(PatchParser.ParseWikiPage("<html><head></head><body>no body</body></html>", FakeSite.Wiki + "/wiki/Patch_Notes/2004-05-11").Changes);
    }

    [Fact]
    public void A_wiki_page_takes_its_issue_from_a_section_and_opens_a_category_at_any_level()
    {
        string sectioned = PatchNotesCommandTests.WikiPage.Replace("Issue 6: Along Came a Spider", "Combined", StringComparison.Ordinal).Replace(">PvP Zones</span></h2>", ">Issue 14: Architect</span></h2>", StringComparison.Ordinal);
        const string H3Only = "<html><head><title>Patch Notes/2011-11-10 - Paragon Wiki Archive</title></head><body><div class=\"mw-parser-output\"><div id=\"toc\"><h2>Contents</h2><ul><li>1 build</li></ul></div>"
            + "<h1><span class=\"mw-headline\">10.10.11 build 2150</span></h1><h3><span class=\"mw-headline\">Incarnate Trials</span></h3>"
            + "<h4><span class=\"mw-headline\">Keyes Island</span></h4><ul><li>Six terminals instead of ten.</li></ul><div class=\"printfooter\"></div></div></body></html>";

        Assert.Equal("issue-14", PatchParser.ParseWikiPage(sectioned, FakeSite.Wiki + "/wiki/Patch_Notes/2009-04-08").Issue);
        Assert.Equal([("Incarnate Trials", "Keyes Island", "Six terminals instead of ten.")], PatchParser.ParseWikiPage(H3Only, FakeSite.Wiki + "/x/2011-11-10").Changes.Select(Row));
    }

    [Fact]
    public void An_entry_serializes_its_fields_in_order()
    {
        PatchEntry entry = new() { Title = "t", Url = "u", Published = "p", Issue = "i", Page = "g", Changes = [new Change("c", "s", "a", "set", "pow", "x")] };

        Assert.Equal(
            "{\"title\": \"t\", \"url\": \"u\", \"published\": \"p\", \"issue\": \"i\", \"page\": \"g\", \"revision\": 0, \"source\": \"release-notes\", \"changes\": [{\"category\": \"c\", \"subsection\": \"s\", \"archetype\": \"a\", \"powerset\": \"set\", \"power\": \"pow\", \"text\": \"x\"}]}",
            PyJson.Dumps(entry.ToObject(), indented: false, ensureAscii: false));
    }

    private static (string Category, string Subsection, string Text) Row(Change change)
    {
        return (change.Category, change.Subsection, change.Text);
    }
}
