using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.SourceMaterial;

namespace CohEngine.Core.PatchNotes;

/// <summary>
/// The <c>patch-notes</c> command: Homecoming's patch notes from three sources, one JSON file per entry under the output
/// folder and one <c>index.jsonl</c> row per entry. A re-run fetches only entries whose URL the index lacks; every entry
/// page is kept in a page cache, so <c>--reparse</c> rebuilds a source's records and rewrites only those that differ.
/// </summary>
internal sealed class PatchNotesCommand
{
    /// <summary>The usage line.</summary>
    public const string Usage = "usage: coh-engine patch-notes [--out OUT] [--source {release-notes,forum-beta,paragonwiki}] [--delay DELAY] [--user-agent USER_AGENT] [--canonical CANONICAL] [--cache CACHE] [--reparse]";

    private const string IndexFile = "index.jsonl";

    private static readonly string[] Sources = [PatchParser.ReleaseNotes, PatchParser.ForumBeta, PatchParser.ParagonWiki];

    private static readonly string[] BetaSections = [PatchParser.Forum + "/forum/73-open-beta-patch-notes/", PatchParser.Forum + "/forum/100-staging-patch-notes/"];

    private static readonly string CategoriesUrl = PatchParser.Forum + "/patch-notes/?show=categories";

    private static readonly string WikiIndexUrl = PatchParser.Wiki + "/wiki/Patch_Notes";

    private readonly HttpClient http;
    private readonly string agent;
    private readonly double delay;
    private readonly IReadOnlySet<string> powersets;
    private readonly string cache;
    private readonly TextWriter output;
    private readonly TextWriter error;

    private PatchNotesCommand(HttpClient http, string agent, double delay, IReadOnlySet<string> powersets, string cache, TextWriter output, TextWriter error)
    {
        this.http = http;
        this.agent = agent;
        this.delay = delay;
        this.powersets = powersets;
        this.cache = cache;
        this.output = output;
        this.error = error;
    }

    /// <summary>Gets the user agent: a named developer tool, run by a person, built on this engine.</summary>
    public static string DefaultAgent { get; } = $"coh-build-companion/0.1 (+operator-run developer tool; MidsReborn build archiver) coh-engine/{EngineInfo.Version}";

    /// <summary>Runs the command.</summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Where progress goes.</param>
    /// <param name="error">Where failures go.</param>
    /// <param name="handler">The network layer, or null for the real one.</param>
    /// <returns>0 when every source was read; 1 when one failed; 2 on bad arguments.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, HttpMessageHandler? handler)
    {
        if (!TryParse(args, out Dictionary<string, List<string>>? options, out double delay, out string problem))
        {
            error.WriteLine(Usage);
            error.WriteLine("coh-engine patch-notes: error: " + problem);
            return 2;
        }

        string outDir = Last(options, "--out") ?? "data/patches";
        string canonical = Last(options, "--canonical") ?? "data/canonical";
        IReadOnlyList<string> sources = options.GetValueOrDefault("--source") ?? [.. Sources];
        IReadOnlySet<string> names = Powersets(canonical);
        if (names.Count == 0)
        {
            error.WriteLine($"{ErrorCodes.E207}: no powerset names under {canonical}; headings will not fill powerset");
        }

        Directory.CreateDirectory(outDir);
        using HttpClient client = new(handler ?? new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip }) { Timeout = TimeSpan.FromSeconds(30) };
        PatchNotesCommand command = new(client, Last(options, "--user-agent") ?? DefaultAgent, delay, names, Last(options, "--cache") ?? Path.Join(outDir, ".cache"), output, error);
        if (options.ContainsKey("--reparse"))
        {
            return command.Reparse(sources, outDir) ? 1 : 0;
        }

        (int total, IReadOnlySet<string> incomplete) = command.Collect(sources, outDir);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{total} entries written to {outDir}"));
        return incomplete.Count > 0 ? 1 : 0;
    }

    /// <summary>The lower-case names of the player powersets the canonical tree has a folder for.</summary>
    /// <param name="canonical">The canonical data folder.</param>
    /// <returns>The names, with underscores read as spaces.</returns>
    public static IReadOnlySet<string> Powersets(string canonical)
    {
        string powers = Path.Join(canonical, "powers");
        if (!Directory.Exists(powers))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        string[] prefixes = [.. SourceManifest.ArchetypeWords, "pool", "epic"];
        return Directory.EnumerateDirectories(powers)
            .Where(role => prefixes.Any(prefix => Path.GetFileName(role).StartsWith(prefix, StringComparison.Ordinal)))
            .SelectMany(Directory.EnumerateDirectories)
            .Select(set => Path.GetFileName(set).Replace('_', ' '))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The index rows of a folder, in file order.</summary>
    /// <param name="outDir">The folder.</param>
    /// <returns>The rows; none when the folder has no index.</returns>
    public static List<PyObject> Index(string outDir)
    {
        string index = Path.Join(outDir, IndexFile);
        return File.Exists(index)
            ? [.. PyText.SplitLines(PyText.ReadText(index)).Where(line => PyText.Strip(line).Length > 0).Select(line => (PyObject)PyJson.Loads(line)!)]
            : [];
    }

    /// <summary>Writes <c>&lt;date&gt;-&lt;slug&gt;.json</c> and appends the entry to the index.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="outDir">The folder.</param>
    /// <returns>The file name.</returns>
    public static string WriteEntry(PatchEntry entry, string outDir)
    {
        Directory.CreateDirectory(outDir);
        string date = entry.Published[..Math.Min(10, entry.Published.Length)];
        string name = $"{(date.Length > 0 ? date : "undated")}-{PatchParser.Slug(entry)}.json";
        PyText.WriteText(Path.Join(outDir, name), PyJson.Dumps(entry.ToObject(), indented: true, ensureAscii: false) + "\n");
        AppendRow(outDir, Row(entry, name));
        return name;
    }

    /// <summary>Keeps only the lines no earlier entry recorded, so a change is dated by its first appearance; repeats inside the entry go too.</summary>
    /// <param name="entry">The entry; its lines are replaced by the kept ones.</param>
    /// <param name="seen">Each recorded line, with its file and date.</param>
    /// <returns>The file most dropped lines first appeared in, or empty when none was dropped.</returns>
    public static string DedupeLines(PatchEntry entry, IReadOnlyDictionary<Change, (string File, string Published)> seen)
    {
        List<Change> kept = [];
        Dictionary<string, int> origins = new(StringComparer.Ordinal);
        List<string> order = [];
        HashSet<Change> local = [];
        foreach (Change change in entry.Changes)
        {
            if (!local.Add(change))
            {
                continue;
            }

            if (seen.TryGetValue(change, out (string File, string Published) first) && string.CompareOrdinal(first.Published, entry.Published) <= 0)
            {
                if (!origins.TryAdd(first.File, 1))
                {
                    origins[first.File]++;
                }
                else
                {
                    order.Add(first.File);
                }

                continue;
            }

            kept.Add(change);
        }

        entry.Changes = kept;
        return order.Count > 0 ? order.Aggregate((best, next) => origins[next] > origins[best] ? next : best) : string.Empty;
    }

    /// <summary>Brings one source's records in <paramref name="outDir"/> to those in <paramref name="fresh"/>, writing only files that differ.</summary>
    /// <param name="fresh">The rebuilt records.</param>
    /// <param name="outDir">The records on disk.</param>
    /// <param name="source">The source.</param>
    /// <returns>The changed, unchanged, removed and kept counts.</returns>
    public static (int Changed, int Unchanged, int Removed, int Kept) Sync(string fresh, string outDir, string source)
    {
        (int changed, int unchanged, int removed, int kept) = (0, 0, 0, 0);
        HashSet<string> produced = new(StringComparer.Ordinal);
        foreach (PyObject row in Index(fresh).Where(row => string.Equals(SourceOf(row), source, StringComparison.Ordinal) && PyObject.Truthy(row.Get("file"))))
        {
            string name = PyText.Str(row["file"]);
            produced.Add(name);
            string text = PyText.ReadText(Path.Join(fresh, name));
            string target = Path.Join(outDir, name);
            string? current = File.Exists(target) ? PyText.ReadText(target) : null;
            if (string.Equals(current, text, StringComparison.Ordinal))
            {
                unchanged++;
            }
            else if (current is not null && !HasChanges(text) && HasChanges(current))
            {
                // A page that now parses to nothing keeps the lines filled in by hand.
                kept++;
            }
            else
            {
                PyText.WriteText(target, text);
                changed++;
            }
        }

        foreach (string name in Index(outDir).Where(row => string.Equals(SourceOf(row), source, StringComparison.Ordinal)).Select(FileOf).Where(name => name.Length > 0 && !produced.Contains(name)))
        {
            File.Delete(Path.Join(outDir, name));
            removed++;
        }

        return (changed, unchanged, removed, kept);
    }

    /// <summary>Keeps each change line only in its source's earliest record; a record that would lose every line stays as it is. Live release notes are never re-dated.</summary>
    /// <param name="outDir">The records.</param>
    /// <param name="source">The source.</param>
    /// <returns>The records rewritten.</returns>
    public static int Redate(string outDir, string source)
    {
        if (source is PatchParser.ReleaseNotes)
        {
            return 0;
        }

        HashSet<Change> seen = [];
        int rewritten = 0;
        IEnumerable<PyObject> held = Index(outDir)
            .Where(row => string.Equals(SourceOf(row), source, StringComparison.Ordinal) && PyObject.Truthy(row.Get("file")))
            .OrderBy(row => PyText.Str(row.Get("published", string.Empty)), StringComparer.Ordinal)
            .ThenBy(row => PyText.Str(row["url"]), StringComparer.Ordinal);
        foreach (PyObject row in held)
        {
            string path = Path.Join(outDir, PyText.Str(row["file"]));
            PyObject document = (PyObject)PyJson.Loads(PyText.ReadText(path))!;
            List<object?> changes = (List<object?>)document["changes"]!;
            List<object?> kept = [];
            foreach (object? change in changes)
            {
                if (seen.Add(LineOf((PyObject)change!)))
                {
                    kept.Add(change);
                }
            }

            if (kept.Count > 0 && kept.Count < changes.Count)
            {
                document.Set("changes", kept);
                PyText.WriteText(path, PyJson.Dumps(document, indented: true, ensureAscii: false) + "\n");
                rewritten++;
            }
        }

        return rewritten;
    }

    /// <summary>The entries to write, oldest first, each stripped of the lines an earlier entry of its source holds.</summary>
    /// <param name="entries">The entries, all of one source.</param>
    /// <param name="outDir">The folder.</param>
    /// <param name="rows">The index rows already on disk.</param>
    /// <param name="written">The URLs indexed so far; the entries' are added.</param>
    /// <param name="output">Where each file written is named.</param>
    /// <param name="error">Where an entry with no lines is reported.</param>
    /// <returns>The files written.</returns>
    public static int WriteDated(IReadOnlyList<PatchEntry> entries, string outDir, IReadOnlyList<PyObject> rows, ISet<string> written, TextWriter output, TextWriter error)
    {
        if (entries.Count == 0)
        {
            return 0;
        }

        Dictionary<Change, (string File, string Published)> seen = SeenLines(outDir, rows, entries[0].Source);
        int count = 0;
        foreach (PatchEntry entry in entries.OrderBy(entry => entry.Published, StringComparer.Ordinal).ThenBy(entry => entry.Url, StringComparer.Ordinal))
        {
            if (entry.Changes.Count == 0)
            {
                error.WriteLine($"{ErrorCodes.E206}: no change lines parsed from {entry.Url}; check the post's layout");
            }

            string origin = DedupeLines(entry, seen);
            if (entry.Changes.Count > 0 || origin.Length == 0)
            {
                string name = WriteEntry(entry, outDir);
                foreach (Change change in entry.Changes)
                {
                    seen.TryAdd(change, (name, entry.Published));
                }

                output.WriteLine("wrote " + name);
                count++;
            }
            else
            {
                PyObject row = Row(entry, string.Empty);
                row.Add("repeat_of", origin);
                AppendRow(outDir, row);
                output.WriteLine($"repeat of {origin}: {entry.Url}");
            }

            written.Add(entry.Url);
        }

        return count;
    }

    private static string? Last(Dictionary<string, List<string>> options, string name)
    {
        return options.GetValueOrDefault(name)?[^1];
    }

    private static bool TryParse(IReadOnlyList<string> args, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Dictionary<string, List<string>>? options, out double delay, out string problem)
    {
        delay = 2.0;
        if (!CommandOptions.TryParse(args, ["--out", "--source", "--delay", "--user-agent", "--canonical", "--cache"], ["--reparse"], out options, out problem))
        {
            return false;
        }

        string? invalid = options.GetValueOrDefault("--source")?.Find(source => !Sources.Contains(source, StringComparer.Ordinal));
        string? given = Last(options, "--delay");
        if (invalid is not null)
        {
            problem = $"argument --source: invalid choice: '{invalid}' (choose from {string.Join(", ", Sources)})";
        }
        else if (given is not null && !double.TryParse(given, NumberStyles.Float, CultureInfo.InvariantCulture, out delay))
        {
            problem = $"argument --delay: invalid float value: '{given}'";
        }

        return problem.Length == 0;
    }

    private static PyObject Row(PatchEntry entry, string file)
    {
        return new PyObject
        {
            { "url", entry.Url },
            { "published", entry.Published },
            { "revision", entry.Revision },
            { "source", entry.Source },
            { "issue", entry.Issue },
            { "page", entry.Page },
            { "file", file },
        };
    }

    private static void AppendRow(string outDir, PyObject row)
    {
        File.AppendAllText(Path.Join(outDir, IndexFile), PyJson.Dumps(row, indented: false, ensureAscii: false) + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteIndex(string outDir, IEnumerable<PyObject> rows)
    {
        string text = string.Concat(rows.Select(row => PyJson.Dumps(row, indented: false, ensureAscii: false) + "\n"));
        string index = Path.Join(outDir, IndexFile);
        if (!File.Exists(index) || !string.Equals(PyText.ReadText(index), text, StringComparison.Ordinal))
        {
            PyText.WriteText(index, text);
        }
    }

    /// <summary>A row's source; rows written before the column existed are release notes.</summary>
    private static string SourceOf(PyObject row)
    {
        return PyText.Str(row.Get("source", PatchParser.ReleaseNotes));
    }

    private static string FileOf(PyObject row)
    {
        object? file = row.Get("file");
        return PyObject.Truthy(file) ? PyText.Str(file) : string.Empty;
    }

    private static bool HasChanges(string document)
    {
        return PyObject.Truthy(((PyObject)PyJson.Loads(document)!)["changes"]);
    }

    private static Change LineOf(PyObject change)
    {
        return new Change(PyText.Str(change["category"]), PyText.Str(change["subsection"]), PyText.Str(change["archetype"]), PyText.Str(change["powerset"]), PyText.Str(change["power"]), PyText.Str(change["text"]));
    }

    /// <summary>The issue and page a live row covers; rows written before those columns existed carry them in the URL.</summary>
    private static (string Issue, string Page) LiveSeries(PyObject row)
    {
        if (PyObject.Truthy(row.Get("issue")) || PyObject.Truthy(row.Get("page")))
        {
            return (PyText.Str(row.Get("issue", string.Empty)), PyText.Str(row.Get("page", string.Empty)));
        }

        System.Text.RegularExpressions.Match match = PatchParser.Entry.Match($"\"{PyText.Str(row["url"])}\"");
        return match.Success && match.Index == 0 ? (match.Groups["issue"].Value, match.Groups["page"].Value) : (string.Empty, string.Empty);
    }

    private static Dictionary<Change, (string File, string Published)> SeenLines(string outDir, IReadOnlyList<PyObject> rows, string source)
    {
        Dictionary<Change, (string File, string Published)> seen = [];
        IEnumerable<PyObject> held = rows
            .Where(row => row.Get("source") is string rowSource && string.Equals(rowSource, source, StringComparison.Ordinal) && PyObject.Truthy(row.Get("file")))
            .OrderBy(row => PyText.Str(row.Get("published", string.Empty)), StringComparer.Ordinal);
        foreach (PyObject row in held)
        {
            string name = PyText.Str(row["file"]);
            PyObject document = (PyObject)PyJson.Loads(PyText.ReadText(Path.Join(outDir, name)))!;
            foreach (object? change in (List<object?>)document["changes"]!)
            {
                seen.TryAdd(LineOf((PyObject)change!), (name, PyText.Str(row.Get("published", string.Empty))));
            }
        }

        return seen;
    }

    /// <summary>The topics of a forum section, and the listing pages that could not be read.</summary>
    private static (IReadOnlyList<string> Topics, IReadOnlyList<string> Failed) WalkListing(PageFetcher fetcher, string forumUrl)
    {
        string? first = fetcher.Get(forumUrl);
        if (first is null)
        {
            return ([], [forumUrl]);
        }

        string listingBase = HtmlText.ListingBase(first, forumUrl);
        List<string> topics = [.. HtmlText.TopicUrls(first)];
        List<string> failed = [];
        for (long number = 2; number <= HtmlText.LastPageNumber(first); number++)
        {
            string url = string.Create(CultureInfo.InvariantCulture, $"{listingBase}/page/{number}/");
            if (fetcher.Get(url) is { } listing)
            {
                topics.AddRange(HtmlText.TopicUrls(listing).Where(topic => !topics.Contains(topic)));
            }
            else
            {
                failed.Add(url);
            }
        }

        return (topics, failed);
    }

    /// <summary>Collects the requested sources into <paramref name="outDir"/>; returns the entries written and the sources not read completely.</summary>
    private (int Total, IReadOnlySet<string> Incomplete) Collect(IReadOnlyList<string> sources, string outDir)
    {
        List<PyObject> rows = Index(outDir);
        HashSet<string> written = [.. rows.Select(row => PyText.Str(row["url"]))];
        int total = 0;
        HashSet<string> incomplete = new(StringComparer.Ordinal);
        PageFetcher forum = new(http, agent, delay, PatchParser.Forum, error);
        foreach (string source in Sources.Where(source => sources.Contains(source, StringComparer.Ordinal)))
        {
            (int count, bool complete) = source switch
            {
                PatchParser.ReleaseNotes => CollectReleaseNotes(forum, written, outDir),
                PatchParser.ForumBeta => CollectForumBeta(forum, written, outDir, rows),
                _ => CollectWiki(new PageFetcher(http, agent, delay, PatchParser.Wiki, error), written, outDir, rows),
            };
            total += count;
            if (!complete)
            {
                incomplete.Add(source);
                error.WriteLine($"{ErrorCodes.E208}: {source} was not read completely, so its records on disk were left as they are; re-run when every page named above can be read");
            }

            rows = Index(outDir);
        }

        return (total, incomplete);
    }

    private (int Count, bool Complete) CollectReleaseNotes(PageFetcher fetcher, HashSet<string> written, string outDir)
    {
        string? categories = fetcher.Get(CategoriesUrl);
        if (categories is null)
        {
            error.WriteLine($"{ErrorCodes.E203}: could not read the patch-notes category browser at {CategoriesUrl}");
            return (0, false);
        }

        int count = 0;
        bool complete = true;
        foreach (string categoryUrl in PatchParser.ListCategories(categories))
        {
            string? listing = fetcher.Get(categoryUrl);
            complete &= listing is not null;
            foreach (string entryUrl in PatchParser.ListEntries(listing ?? string.Empty).Where(url => !written.Contains(url)))
            {
                if (Cached(fetcher, entryUrl) is not { } page)
                {
                    complete = false;
                    continue;
                }

                PatchEntry entry = PatchParser.ParseEntry(page, entryUrl, powersets);
                if (entry.Changes.Count == 0)
                {
                    error.WriteLine($"{ErrorCodes.E206}: no change lines parsed from {entryUrl}; check the post's layout");
                }

                output.WriteLine("wrote " + WriteEntry(entry, outDir));
                written.Add(entryUrl);
                count++;
            }
        }

        return (count, complete);
    }

    /// <summary>Beta threads the live application does not cover; returns the entries written and whether every listing page was read.</summary>
    private (int Count, bool Complete) CollectForumBeta(PageFetcher fetcher, HashSet<string> written, string outDir, IReadOnlyList<PyObject> rows)
    {
        List<PyObject> live = [.. rows.Where(row => row.Get("source", PatchParser.ReleaseNotes) is PatchParser.ReleaseNotes)];
        HashSet<(string, string)> liveSeries = [.. live.Select(LiveSeries)];
        string liveFrom = live.Count > 0 ? live.Select(row => PyText.Str(row["published"])).Min(StringComparer.Ordinal)! : "9999";
        List<string> listing = [];
        List<string> gaps = [];
        foreach (string section in BetaSections)
        {
            (IReadOnlyList<string> topics, IReadOnlyList<string> failed) = WalkListing(fetcher, section);
            gaps.AddRange(failed);
            listing.AddRange(topics.Where(topic => !listing.Contains(topic)));
        }

        foreach (string url in gaps)
        {
            error.WriteLine($"{ErrorCodes.E205}: beta listing page unavailable: {url}; re-run to pick up its threads");
        }

        List<PatchEntry> entries = [];
        foreach (string topic in PatchParser.BetaCandidates(listing).Where(topic => !written.Contains(topic)))
        {
            if (Cached(fetcher, topic) is not { } page)
            {
                gaps.Add(topic);
                continue;
            }

            PatchEntry entry = PatchParser.ParseBetaThread(page, topic, powersets);
            bool labelled = entry.Issue.Length > 0;
            if ((labelled && liveSeries.Contains((entry.Issue, entry.Page))) || (!labelled && string.CompareOrdinal(entry.Published, liveFrom) >= 0))
            {
                continue;
            }

            entries.Add(entry);
        }

        return (WriteDated(entries, outDir, rows, written, output, error), gaps.Count == 0);
    }

    private (int Count, bool Complete) CollectWiki(PageFetcher fetcher, HashSet<string> written, string outDir, IReadOnlyList<PyObject> rows)
    {
        string? index = fetcher.Get(WikiIndexUrl);
        if (index is null)
        {
            error.WriteLine($"{ErrorCodes.E204}: could not read the wiki patch-notes index at {WikiIndexUrl}");
            return (0, false);
        }

        List<PatchEntry> entries = [];
        bool complete = true;
        foreach (string pageUrl in PatchParser.WikiPages(index).Where(url => !written.Contains(url)))
        {
            string? page = Cached(fetcher, pageUrl);
            complete &= page is not null;
            if (page is not null)
            {
                entries.Add(PatchParser.ParseWikiPage(page, pageUrl));
            }
        }

        return (WriteDated(entries, outDir, rows, written, output, error), complete);
    }

    /// <summary>The cached page for <paramref name="url"/>, else the fetched one, cached for next time. Listing and index pages never go through it.</summary>
    private string? Cached(PageFetcher fetcher, string url)
    {
        string path = Path.Join(cache, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..32] + ".html");
        if (File.Exists(path))
        {
            return PyText.ReadText(path);
        }

        string? page = fetcher.Get(url);
        if (page is not null)
        {
            Directory.CreateDirectory(cache);
            PyText.WriteText(path, page);
        }

        return page;
    }

    /// <summary>Rebuilds the sources' records in a scratch folder, then changes only what differs in <paramref name="outDir"/>.</summary>
    private bool Reparse(IReadOnlyList<string> sources, string outDir)
    {
        List<PyObject> oldRows = Index(outDir);
        Dictionary<string, (int Changed, int Unchanged, int Removed, int Kept)> counts = new(StringComparer.Ordinal);
        DirectoryInfo scratch = Directory.CreateTempSubdirectory("coh-patch-notes-");
        IReadOnlySet<string> incomplete;
        try
        {
            // Other sources' rows stay in view: the beta collector needs the live coverage.
            WriteIndex(scratch.FullName, oldRows.Where(row => !sources.Contains(SourceOf(row), StringComparer.Ordinal)));
            (_, incomplete) = Collect(sources, scratch.FullName);
            foreach (string source in sources.Where(source => !incomplete.Contains(source)))
            {
                counts[source] = Sync(scratch.FullName, outDir, source);
            }

            List<PyObject> newRows = Index(scratch.FullName);
            WriteIndex(outDir, Sources.SelectMany(source => (counts.ContainsKey(source) ? newRows : oldRows).Where(row => string.Equals(SourceOf(row), source, StringComparison.Ordinal))));
        }
        finally
        {
            scratch.Delete(recursive: true);
        }

        foreach (string source in Sources.Where(counts.ContainsKey))
        {
            (int changed, int unchanged, int removed, int kept) = counts[source];
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{source}: {changed} changed, {unchanged} unchanged, {removed} removed, {kept} kept, {Redate(outDir, source)} re-dated"));
        }

        return incomplete.Count > 0;
    }
}
