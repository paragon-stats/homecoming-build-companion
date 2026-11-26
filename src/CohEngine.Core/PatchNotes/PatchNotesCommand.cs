using System.Buffers;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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

    // The keys of index rows and record files.
    private const string ChangesKey = "changes";

    private const string PublishedKey = "published";

    private const string SourceKey = "source";

    private const string IssueKey = "issue";

    /// <summary>The most pages of one beta listing that are read; the real sections hold a handful.</summary>
    private const int MaxListingPages = 100;

    /// <summary>The longest file name NTFS and ext4 hold.</summary>
    private const int MaxNameLength = 255;

    /// <summary>The longest gap <c>--delay</c> takes; past it a run would stall rather than be polite.</summary>
    private const double MaxDelaySeconds = 3600;

    private static readonly string[] Sources = [PatchParser.ReleaseNotes, PatchParser.ForumBeta, PatchParser.ParagonWiki];

    private static readonly string[] ChangeKeys = ["category", "subsection", "archetype", "powerset", "power", "text"];

    private static readonly string[] BetaSections = [PatchParser.Forum + "/forum/73-open-beta-patch-notes/", PatchParser.Forum + "/forum/100-staging-patch-notes/"];

    /// <summary>The characters a Windows file name cannot hold: the controls, <c>"*/:&lt;&gt;?\|</c>.</summary>
    private static readonly SearchValues<char> RefusedNameChars = SearchValues.Create([.. Enumerable.Range(0, 32).Select(code => (char)code), '"', '*', '/', ':', '<', '>', '?', '\\', '|']);

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
            error.WriteLine($"warning: {ErrorCodes.E207}: no powerset names under {canonical}; headings will not fill powerset; pass --canonical with the folder that holds powers/");
        }

        using HttpClient client = new(handler ?? new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip, AllowAutoRedirect = false }, disposeHandler: handler is null) { Timeout = TimeSpan.FromSeconds(30) };
        PatchNotesCommand command = new(client, Last(options, "--user-agent") ?? DefaultAgent, delay, names, Last(options, "--cache") ?? Path.Join(outDir, ".cache"), output, error);
        try
        {
            Directory.CreateDirectory(outDir);
            if (options.ContainsKey("--reparse"))
            {
                return command.Reparse(sources, outDir) ? 1 : 0;
            }

            (int total, IReadOnlySet<string> incomplete) = command.Collect(sources, outDir);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{total} entries written to {outDir}"));
            return incomplete.Count > 0 ? 1 : 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"error: {ErrorCodes.E224}: cannot read or write under {outDir}: {exception.Message}; check that the folder and its cache can be written and the disk has room");
            return 1;
        }
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
    /// <exception cref="EngineException">E225: a line is not a JSON object with a <c>url</c>.</exception>
    public static List<PyObject> Index(string outDir)
    {
        string index = Path.Join(outDir, IndexFile);
        if (!File.Exists(index))
        {
            return [];
        }

        IReadOnlyList<string> lines = PyText.SplitLines(Readable(index));
        List<PyObject> rows = [];
        for (int number = 1; number <= lines.Count; number++)
        {
            if (PyText.Strip(lines[number - 1]).Length > 0)
            {
                rows.Add(Parsed(lines[number - 1]) is PyObject row && row.Get("url") is string
                    ? row
                    : throw new EngineException(string.Create(CultureInfo.InvariantCulture, $"{ErrorCodes.E225}: line {number} of {index} is not an index row (a JSON object with a url); correct or remove that line, then re-run")));
            }
        }

        return rows;
    }

    /// <summary>Writes <c>&lt;date&gt;-&lt;slug&gt;.json</c> and appends the entry to the index.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="outDir">The folder.</param>
    /// <returns>The file name.</returns>
    /// <exception cref="EngineException">E209: the date or slug, taken from the page and its URL, would not make a plain file name inside the folder.</exception>
    public static string WriteEntry(PatchEntry entry, string outDir)
    {
        Directory.CreateDirectory(outDir);
        string date = entry.Published[..Math.Min(10, entry.Published.Length)];
        string name = $"{(date.Length > 0 ? date : "undated")}-{PatchParser.Slug(entry)}.json";
        if (!IsPlainName(name))
        {
            throw new EngineException($"{ErrorCodes.E209}: entry {entry.Url} would be written as '{name}', which is not a plain file name inside {outDir}; it was skipped; check the date in the page and the last segment of its URL");
        }

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
    /// <param name="error">Where an index row that names a file outside the folder is reported.</param>
    /// <param name="doomed">Receives the records no page produces any more; the caller deletes them once the new index is written.</param>
    /// <returns>The changed, unchanged, removed and kept counts.</returns>
    public static (int Changed, int Unchanged, int Removed, int Kept) Sync(string fresh, string outDir, string source, TextWriter error, ICollection<string> doomed)
    {
        (int changed, int unchanged, int removed, int kept) = (0, 0, 0, 0);

        // Names compare as the file system does, so an old row differing only in case never removes the record just written.
        HashSet<string> produced = new(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        IEnumerable<string> names = Index(fresh)
            .Where(row => string.Equals(SourceOf(row), source, StringComparison.Ordinal) && PyObject.Truthy(row.Get("file")))
            .Select(row => PyText.Str(row["file"]));
        foreach (string name in names)
        {
            produced.Add(name);
            string text = PyText.ReadText(Path.Join(fresh, name));
            string target = Path.Join(outDir, name);
            string? current = File.Exists(target) ? PyText.ReadText(target) : null;
            if (string.Equals(current, text, StringComparison.Ordinal))
            {
                unchanged++;
            }
            else if (current is not null && !HasChanges(Record(Path.Join(fresh, name))) && HasChanges(Record(target)))
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

        foreach (PyObject row in Index(outDir).Where(row => string.Equals(SourceOf(row), source, StringComparison.Ordinal) && FileOf(row).Length > 0 && !produced.Contains(FileOf(row))))
        {
            string name = FileOf(row);
            if (!IsPlainName(name))
            {
                error.WriteLine($"warning: {ErrorCodes.E209}: index row {PyText.Str(row.Get("url", string.Empty))} names '{name}', which is not a plain file name inside {outDir}; the file was left alone; correct the row's file name");
                continue;
            }

            doomed.Add(Path.Join(outDir, name));
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
        IEnumerable<string> paths = Index(outDir)
            .Where(row => string.Equals(SourceOf(row), source, StringComparison.Ordinal) && PyObject.Truthy(row.Get("file")) && IsPlainName(FileOf(row)))
            .OrderBy(row => PyText.Str(row.Get(PublishedKey, string.Empty)), StringComparer.Ordinal)
            .ThenBy(row => PyText.Str(row["url"]), StringComparer.Ordinal)
            .Select(row => Path.Join(outDir, PyText.Str(row["file"])));
        foreach (string path in paths)
        {
            PyObject document = Record(path);
            List<object?> changes = (List<object?>)document[ChangesKey]!;

            // A line goes when an earlier record, or an earlier line of this one, already holds it.
            List<object?> kept = [.. changes.Where(change => seen.Add(LineOf((PyObject)change!)))];

            if (kept.Count > 0 && kept.Count < changes.Count)
            {
                document.Set(ChangesKey, kept);
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
    /// <returns>The files written, and false when an entry was refused.</returns>
    public static (int Count, bool Complete) WriteDated(IReadOnlyList<PatchEntry> entries, string outDir, IReadOnlyList<PyObject> rows, ISet<string> written, TextWriter output, TextWriter error)
    {
        if (entries.Count == 0)
        {
            return (0, true);
        }

        Dictionary<Change, (string File, string Published)> seen = SeenLines(outDir, rows, entries[0].Source);
        int count = 0;
        bool complete = true;
        foreach (PatchEntry entry in entries.OrderBy(entry => entry.Published, StringComparer.Ordinal).ThenBy(entry => entry.Url, StringComparer.Ordinal))
        {
            if (entry.Changes.Count == 0)
            {
                error.WriteLine($"warning: {ErrorCodes.E206}: no change lines parsed from {entry.Url}; check the post's layout");
            }

            string origin = DedupeLines(entry, seen);
            if (entry.Changes.Count > 0 || origin.Length == 0)
            {
                if (TryWrite(entry, outDir, error) is not { } name)
                {
                    complete = false;
                    continue;
                }

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

        return (count, complete);
    }

    /// <summary>
    /// Whether a name from an index row or a page is one plain file name, so joining it to the folder stays inside the
    /// folder. The characters Windows refuses are refused on every system, so a record written on Linux can be checked out on Windows.
    /// </summary>
    private static bool IsPlainName(string name)
    {
        return name is not ("." or "..") && name.Length <= MaxNameLength && string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal) && name.AsSpan().IndexOfAny(RefusedNameChars) < 0;
    }

    /// <summary>Writes the entry, or reports why its name was refused and returns null.</summary>
    private static string? TryWrite(PatchEntry entry, string outDir, TextWriter error)
    {
        try
        {
            return WriteEntry(entry, outDir);
        }
        catch (EngineException refused)
        {
            error.WriteLine("error: " + refused.Message);
            return null;
        }
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
        else if (delay is not (>= 0 and <= MaxDelaySeconds))
        {
            problem = string.Create(CultureInfo.InvariantCulture, $"argument --delay: not a number of seconds from 0 to {MaxDelaySeconds}: '{given}'");
        }

        return problem.Length == 0;
    }

    private static PyObject Row(PatchEntry entry, string file)
    {
        return new PyObject
        {
            { "url", entry.Url },
            { PublishedKey, entry.Published },
            { "revision", entry.Revision },
            { SourceKey, entry.Source },
            { IssueKey, entry.Issue },
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

    private static bool IsOf(PyObject row, string source)
    {
        return string.Equals(SourceOf(row), source, StringComparison.Ordinal);
    }

    /// <summary>A row's source; rows written before the column existed are release notes.</summary>
    private static string SourceOf(PyObject row)
    {
        return PyText.Str(row.Get(SourceKey, PatchParser.ReleaseNotes));
    }

    private static string FileOf(PyObject row)
    {
        object? file = row.Get("file");
        return PyObject.Truthy(file) ? PyText.Str(file) : string.Empty;
    }

    private static bool HasChanges(PyObject document)
    {
        return PyObject.Truthy(document[ChangesKey]);
    }

    /// <summary>A record file: a JSON object whose <c>changes</c> list holds change objects of six strings.</summary>
    /// <exception cref="EngineException">E225: the file is not such a record.</exception>
    private static PyObject Record(string path)
    {
        return Parsed(Readable(path)) is PyObject document && document.Get(ChangesKey) is List<object?> changes && changes.TrueForAll(change => change is PyObject line && ChangeKeys.All(key => line.Get(key) is string))
            ? document
            : throw new EngineException($"{ErrorCodes.E225}: {path} is not a patch-notes record (a JSON object whose changes list holds objects with category, subsection, archetype, powerset, power and text strings); restore it from git, or delete it with its index row and run --reparse");
    }

    /// <summary>A file of the output folder as text.</summary>
    /// <exception cref="EngineException">E225: the file is not valid UTF-8.</exception>
    private static string Readable(string path)
    {
        try
        {
            return PyText.ReadText(path);
        }
        catch (DecoderFallbackException invalid)
        {
            throw new EngineException($"{ErrorCodes.E225}: {path} is not valid UTF-8; restore it from git, or delete it with its index row and run --reparse", invalid);
        }
    }

    /// <summary>The JSON value of a text, or null when it is not JSON.</summary>
    private static object? Parsed(string text)
    {
        try
        {
            return PyJson.Loads(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Change LineOf(PyObject change)
    {
        return new Change(PyText.Str(change["category"]), PyText.Str(change["subsection"]), PyText.Str(change["archetype"]), PyText.Str(change["powerset"]), PyText.Str(change["power"]), PyText.Str(change["text"]));
    }

    /// <summary>The issue and page a live row covers; rows written before those columns existed carry them in the URL.</summary>
    private static (string Issue, string Page) LiveSeries(PyObject row)
    {
        if (PyObject.Truthy(row.Get(IssueKey)) || PyObject.Truthy(row.Get("page")))
        {
            return (PyText.Str(row.Get(IssueKey, string.Empty)), PyText.Str(row.Get("page", string.Empty)));
        }

        Match match = PatchParser.Entry.Match($"\"{PyText.Str(row["url"])}\"");
        return match.Success && match.Index == 0 ? (match.Groups["issue"].Value, match.Groups["page"].Value) : (string.Empty, string.Empty);
    }

    private static Dictionary<Change, (string File, string Published)> SeenLines(string outDir, IReadOnlyList<PyObject> rows, string source)
    {
        Dictionary<Change, (string File, string Published)> seen = [];
        IEnumerable<PyObject> held = rows
            .Where(row => row.Get(SourceKey) is string rowSource && string.Equals(rowSource, source, StringComparison.Ordinal) && PyObject.Truthy(row.Get("file")))
            .OrderBy(row => PyText.Str(row.Get(PublishedKey, string.Empty)), StringComparer.Ordinal);
        foreach (PyObject row in held)
        {
            string name = PyText.Str(row["file"]);
            PyObject document = Record(Path.Join(outDir, name));
            foreach (object? change in (List<object?>)document[ChangesKey]!)
            {
                seen.TryAdd(LineOf((PyObject)change!), (name, PyText.Str(row.Get(PublishedKey, string.Empty))));
            }
        }

        return seen;
    }

    /// <summary>The topics of a forum section, the listing pages that could not be read, and whether every page read named a thread.</summary>
    private static (IReadOnlyList<string> Topics, IReadOnlyList<string> Failed, bool AllNamed) WalkListing(PageFetcher fetcher, string forumUrl, TextWriter error)
    {
        string? first = fetcher.Get(forumUrl);
        if (first is null)
        {
            return ([], [forumUrl], true);
        }

        string listingBase = HtmlText.ListingBase(first, forumUrl);
        List<string> topics = [.. HtmlText.TopicUrls(first)];
        bool allNamed = NamesLinks(forumUrl, topics.Count, "beta thread", error);
        List<string> failed = [];
        long declared = HtmlText.LastPageNumber(first);
        if (declared > MaxListingPages)
        {
            allNamed = false;
            error.WriteLine(string.Create(CultureInfo.InvariantCulture, $"error: {ErrorCodes.E226}: {forumUrl} declares {declared} listing pages, more than the {MaxListingPages} read, so its threads past page {MaxListingPages} were not read and its source counts as not read completely; check the listing in a browser, and raise MaxListingPages in PatchNotesCommand if the section really is that long"));
        }

        for (long number = 2; number <= Math.Min(declared, MaxListingPages); number++)
        {
            string url = string.Create(CultureInfo.InvariantCulture, $"{listingBase}/page/{number}/");
            if (fetcher.Get(url) is { } listing)
            {
                List<string> found = [.. HtmlText.TopicUrls(listing)];
                allNamed &= NamesLinks(url, found.Count, "beta thread", error);
                topics.AddRange(found.Where(topic => !topics.Contains(topic)));
            }
            else
            {
                failed.Add(url);
            }
        }

        return (topics, failed, allNamed);
    }

    /// <summary>Whether a listing that answered named any link; one that named none, such as a maintenance page, is reported.</summary>
    private static bool NamesLinks(string url, int links, string what, TextWriter error)
    {
        if (links > 0)
        {
            return true;
        }

        error.WriteLine($"error: {ErrorCodes.E227}: {url} answered but names no {what}, so its source counts as not read completely; open it in a browser to check the site is up, then re-run");
        return false;
    }

    /// <summary>Collects the requested sources into <paramref name="outDir"/>; returns the entries written and the sources not read completely.</summary>
    private (int Total, IReadOnlySet<string> Incomplete) Collect(IReadOnlyList<string> sources, string outDir)
    {
        List<PyObject> rows = Index(outDir);
        HashSet<string> written = [.. rows.Select(row => PyText.Str(row["url"]))];
        int total = 0;
        HashSet<string> incomplete = new(StringComparer.Ordinal);
        PageFetcher? forum = null;
        foreach (string source in Sources.Where(source => sources.Contains(source, StringComparer.Ordinal)))
        {
            (int count, bool complete) = (0, false);
            try
            {
                (count, complete) = source switch
                {
                    PatchParser.ReleaseNotes => CollectReleaseNotes(forum ??= new PageFetcher(http, agent, delay, PatchParser.Forum, error), written, outDir),
                    PatchParser.ForumBeta => CollectForumBeta(forum ??= new PageFetcher(http, agent, delay, PatchParser.Forum, error), written, outDir, rows),
                    _ => CollectWiki(new PageFetcher(http, agent, delay, PatchParser.Wiki, error), written, outDir, rows),
                };
            }
            catch (RegexMatchTimeoutException)
            {
                error.WriteLine($"error: {ErrorCodes.E223}: a page from {source} took too long to parse (over 1 s for one pattern), so {source} stopped there; entries written before it stay; look for a very large or broken page among those fetched last");
            }

            total += count;
            if (!complete)
            {
                incomplete.Add(source);
                error.WriteLine($"error: {ErrorCodes.E208}: {source} was not read completely, so its records on disk were left as they are; re-run when every page named above can be read");
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
            error.WriteLine($"error: {ErrorCodes.E203}: could not read the patch-notes category browser at {CategoriesUrl}; no release notes were read; re-run when the forum answers");
            return (0, false);
        }

        int count = 0;
        IReadOnlyList<string> categoryUrls = PatchParser.ListCategories(categories);
        bool complete = NamesLinks(CategoriesUrl, categoryUrls.Count, "patch-notes category", error);
        foreach (string categoryUrl in categoryUrls)
        {
            string? listing = fetcher.Get(categoryUrl);
            IReadOnlyList<string> entryUrls = PatchParser.ListEntries(listing ?? string.Empty);
            complete &= listing is not null && NamesLinks(categoryUrl, entryUrls.Count, "patch-notes entry", error);
            foreach (string entryUrl in entryUrls.Where(url => !written.Contains(url)))
            {
                if (Cached(fetcher, entryUrl) is not { } page)
                {
                    complete = false;
                    continue;
                }

                PatchEntry entry = PatchParser.ParseEntry(page, entryUrl, powersets);
                if (entry.Changes.Count == 0)
                {
                    error.WriteLine($"warning: {ErrorCodes.E206}: no change lines parsed from {entryUrl}; check the post's layout");
                }

                if (TryWrite(entry, outDir, error) is not { } name)
                {
                    complete = false;
                    continue;
                }

                output.WriteLine("wrote " + name);
                written.Add(entryUrl);
                count++;
            }
        }

        return (count, complete);
    }

    /// <summary>Beta threads the live application does not cover; returns the entries written and whether every listing page was read.</summary>
    private (int Count, bool Complete) CollectForumBeta(PageFetcher fetcher, HashSet<string> written, string outDir, IReadOnlyList<PyObject> rows)
    {
        List<PyObject> live = [.. rows.Where(row => row.Get(SourceKey, PatchParser.ReleaseNotes) is PatchParser.ReleaseNotes)];
        HashSet<(string, string)> liveSeries = [.. live.Select(LiveSeries)];
        string liveFrom = live.Count > 0 ? live.Select(row => PyText.Str(row[PublishedKey])).Min(StringComparer.Ordinal)! : "9999";
        List<string> listing = [];
        List<string> gaps = [];
        bool allNamed = true;
        foreach (string section in BetaSections)
        {
            (IReadOnlyList<string> topics, IReadOnlyList<string> failed, bool named) = WalkListing(fetcher, section, error);
            gaps.AddRange(failed);
            allNamed &= named;
            listing.AddRange(topics.Where(topic => !listing.Contains(topic)));
        }

        foreach (string url in gaps)
        {
            error.WriteLine($"error: {ErrorCodes.E205}: beta listing page unavailable: {url}; re-run to pick up its threads");
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

        (int count, bool complete) = WriteDated(entries, outDir, rows, written, output, error);
        return (count, complete && allNamed && gaps.Count == 0);
    }

    private (int Count, bool Complete) CollectWiki(PageFetcher fetcher, HashSet<string> written, string outDir, IReadOnlyList<PyObject> rows)
    {
        string? index = fetcher.Get(WikiIndexUrl);
        if (index is null)
        {
            error.WriteLine($"error: {ErrorCodes.E204}: could not read the wiki patch-notes index at {WikiIndexUrl}; no wiki pages were read; re-run when the wiki answers");
            return (0, false);
        }

        List<PatchEntry> entries = [];
        IReadOnlyList<string> pageUrls = PatchParser.WikiPages(index);
        bool complete = NamesLinks(WikiIndexUrl, pageUrls.Count, "dated patch-notes page", error);
        foreach (string pageUrl in pageUrls.Where(url => !written.Contains(url)))
        {
            string? page = Cached(fetcher, pageUrl);
            complete &= page is not null;
            if (page is not null)
            {
                entries.Add(PatchParser.ParseWikiPage(page, pageUrl));
            }
        }

        (int count, bool allWritten) = WriteDated(entries, outDir, rows, written, output, error);
        return (count, complete && allWritten);
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
        List<string> doomed = [];
        bool failed;
        try
        {
            // Other sources' rows stay in view: the beta collector needs the live coverage.
            WriteIndex(scratch.FullName, oldRows.Where(row => !sources.Contains(SourceOf(row), StringComparer.Ordinal)));
            (_, IReadOnlySet<string> incomplete) = Collect(sources, scratch.FullName);
            failed = incomplete.Count > 0;
            List<PyObject> newRows = Index(scratch.FullName);
            foreach (string source in sources.Where(source => !incomplete.Contains(source)))
            {
                // A source that now yields nothing while the folder holds its records was not really read.
                if (!newRows.Exists(row => IsOf(row, source)) && oldRows.Exists(row => IsOf(row, source)))
                {
                    error.WriteLine($"error: {ErrorCodes.E208}: {source} was not read completely, so its records on disk were left as they are; it gave no records while the folder holds some; re-run when its pages list entries again");
                    failed = true;
                    continue;
                }

                counts[source] = Sync(scratch.FullName, outDir, source, error, doomed);
            }

            // The index is written before any record goes, so a failure up to here leaves no row naming a deleted file.
            WriteIndex(outDir, Sources.SelectMany(source => (counts.ContainsKey(source) ? newRows : oldRows).Where(row => IsOf(row, source))));
            doomed.ForEach(File.Delete);
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

        return failed;
    }
}
