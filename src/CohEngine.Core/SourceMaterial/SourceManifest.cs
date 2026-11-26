using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.SourceMaterial;

/// <summary>
/// The <c>source-manifest</c> command: one row per capture under the source root's <c>community/</c> folder (URL, title,
/// date, archetype, content hash, path, era), no post text, sorted by path so a re-run writes the same bytes.
/// </summary>
internal static class SourceManifest
{
    /// <summary>The usage line.</summary>
    public const string Usage = "usage: coh-engine source-manifest [--source-root PATH] [--out OUT]";

    private const string PreSunset = "pre-sunset";

    private const string Live = "live";

    private const string Empty = "\"\"";

    private const string SourceRootOption = "--source-root";

    private const string OutOption = "--out";

    /// <summary>The archetype words a folder or title names, in match order, with the folder each one files under.</summary>
    private static readonly (string Word, string Folder)[] Archetypes =
    [
        ("blaster", "blaster"),
        ("brute", "brute"),
        ("controller", "controller"),
        ("corruptor", "corruptor"),
        ("defender", "defender"),
        ("dominator", "dominator"),
        ("mastermind", "mastermind"),
        ("scrapper", "scrapper"),
        ("sentinel", "sentinel"),
        ("stalker", "stalker"),
        ("tanker", "tanker"),
        ("kheldian", "kheldian"),
        ("peacebringer", "peacebringer"),
        ("warshade", "warshade"),
        ("arachnos", "arachnos"),
        ("veat", "arachnos"),
        ("widow", "arachnos"),
    ];

    /// <summary>Gets the archetype words, which also begin the names of the canonical role folders that hold player powersets.</summary>
    public static IEnumerable<string> ArchetypeWords => Archetypes.Select(pair => pair.Word);

    /// <summary>Runs the command.</summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Where the status lines go.</param>
    /// <param name="error">Where errors go.</param>
    /// <param name="variable">Reads an environment variable.</param>
    /// <param name="gitUserName">Reads the git user name, the name filter's fallback; null when git has none.</param>
    /// <returns>0 when the manifest was written; 1 on an error; 2 on bad arguments.</returns>
    /// <exception cref="EngineException">E210: the source root is not a directory.</exception>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, Func<string, string?> variable, Func<string?> gitUserName)
    {
        if (!CommandOptions.TryParse(args, [SourceRootOption, OutOption], [], out Dictionary<string, List<string>>? options, out string problem))
        {
            error.WriteLine(Usage);
            error.WriteLine("coh-engine source-manifest: error: " + problem);
            return 2;
        }

        string? flag = options.GetValueOrDefault(SourceRootOption)?[^1];
        string outPath = options.GetValueOrDefault(OutOption)?[^1] ?? "data/source-material/manifest.json";
        string community = Path.Join(SourceRoot.Resolve(flag, variable(SourceRoot.EnvironmentVariable), Directory.GetCurrentDirectory()), "community");
        Regex? pattern = BannedNames.Pattern(BannedNames.Names(variable(BannedNames.EnvironmentVariable), gitUserName));
        (IReadOnlyList<PyObject> rows, int withheld) = BannedNames.Withhold(Build(community, error), pattern);
        if (rows.Count == 0 && withheld > 0)
        {
            error.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{ErrorCodes.E213}: every capture under {community} was withheld by the name filter ({withheld} rows), so nothing was written; check the names in {BannedNames.EnvironmentVariable}, or the git user name when it is unset"));
            return 1;
        }

        if (rows.Count == 0)
        {
            error.WriteLine($"{ErrorCodes.E211}: no captures under {community}; expected community/**/*.md with front matter; check the checkout is current, or pass --source-root");
            return 1;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            PyText.WriteText(outPath, Render(rows));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"{ErrorCodes.E214}: cannot write {outPath}: {exception.Message}; check that the path names a file in a folder you can write to");
            return 1;
        }

        output.WriteLine(BannedNames.Report(pattern, withheld));
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{rows.Count} capture(s) listed in {outPath}"));
        return 0;
    }

    /// <summary>One row per capture under <paramref name="community"/>, sorted by path, keys in the order the rows are built.</summary>
    /// <param name="community">The <c>community/</c> folder.</param>
    /// <param name="error">Where a skipped capture is reported.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<PyObject> Build(string community, TextWriter error)
    {
        if (!Directory.Exists(community))
        {
            return [];
        }

        Dictionary<string, Dictionary<string, string>> ledgers = new(StringComparer.Ordinal)
        {
            [Live] = LedgerUrls(community),
            [PreSunset] = LedgerUrls(Path.Join(community, PreSunset)),
        };
        List<PyObject> rows = [];
        foreach (string file in Directory.EnumerateFiles(community, "*.md", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(community, file).Replace('\\', '/');
            (Dictionary<string, string> keys, string body) = Split(PyText.ReadText(file));
            if (keys.Count == 0)
            {
                continue;
            }

            if (!keys.TryGetValue("title", out string? rawTitle))
            {
                error.WriteLine($"{ErrorCodes.E212}: {relative}: front matter has no title; capture skipped; add a title: line to its front matter");
                continue;
            }

            string era = relative.StartsWith(PreSunset + "/", StringComparison.Ordinal) ? PreSunset : Live;
            string inner = era is PreSunset ? relative[(PreSunset.Length + 1)..] : relative;
            string title = Unquote(rawTitle);
            string url = Unquote(keys.GetValueOrDefault("url", Empty));
            string hash = Unquote(keys.GetValueOrDefault("content_hash", Empty));
            rows.Add(new PyObject
            {
                { "url", url.Length > 0 ? url : ledgers[era].GetValueOrDefault(inner, string.Empty) },
                { "title", title },
                { "date_posted", Unquote(keys.GetValueOrDefault("date_posted", Empty)) },
                { "archetype", ArchetypeOf(inner, title) },
                { "content_hash", hash.Length > 0 ? hash : ContentHash(body) },
                { "path", "community/" + relative },
                { "era", era },
            });
        }

        return [.. rows.OrderBy(row => (string)row["path"]!, StringComparer.Ordinal)];
    }

    /// <summary>The manifest file's text: keys sorted, two-space indent, every non-ASCII character escaped.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(IReadOnlyList<PyObject> rows)
    {
        List<PyObject> sorted = [.. rows.Select<PyObject, PyObject>(row => [.. row.OrderBy(pair => pair.Key, StringComparer.Ordinal)])];
        return PyJson.Dumps(sorted, indented: true, ensureAscii: true) + "\n";
    }

    /// <summary>The front matter's keys and raw values, and the body; a file without front matter is all body.</summary>
    /// <param name="capture">The capture text.</param>
    /// <returns>The keys and the body.</returns>
    public static (Dictionary<string, string> Keys, string Body) Split(string capture)
    {
        Dictionary<string, string> keys = new(StringComparer.Ordinal);
        int end = capture.StartsWith("---\n", StringComparison.Ordinal) ? capture.IndexOf("\n---", 4, StringComparison.Ordinal) : -1;
        if (end < 0)
        {
            return (keys, capture);
        }

        foreach (string line in PyText.SplitLines(capture[4..end]))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon >= 0)
            {
                keys[PyText.Strip(line[..colon])] = PyText.Strip(line[(colon + 1)..]);
            }
        }

        return (keys, capture[(end + 4)..].TrimStart('\n'));
    }

    /// <summary>A front-matter value: Python's text of the JSON value it holds, else the raw text without quotes and spaces at its ends.</summary>
    /// <param name="raw">The raw value.</param>
    /// <returns>The value.</returns>
    public static string Unquote(string raw)
    {
        try
        {
            return PyText.Str(PyJson.Loads(raw));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return raw.Trim('"', '\'', ' ');
        }
    }

    /// <summary>The first 16 hex digits of the SHA-256 of the text's UTF-8 bytes.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The digest.</returns>
    public static string ContentHash(string text)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    /// <summary>The archetype a capture files under: its <c>archetypes/&lt;name&gt;/</c> folder, else the first archetype word its title names.</summary>
    /// <param name="inner">The capture's path below its era's root.</param>
    /// <param name="title">The capture's title.</param>
    /// <returns>The archetype folder, or empty.</returns>
    public static string ArchetypeOf(string inner, string title)
    {
        string[] folders = inner.Split('/')[..^1];
        if (folders.Length > 1 && folders[0] is "archetypes")
        {
            return folders[1];
        }

        string lowered = title.ToLowerInvariant();
        foreach ((string word, string folder) in Archetypes)
        {
            if (Regex.IsMatch(lowered, $@"\b{word}s?\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                return folder;
            }
        }

        return string.Empty;
    }

    /// <summary>Per capture file, the topic URL the folder's <c>guides.jsonl</c> ledger recorded; the last row of a topic wins.</summary>
    private static Dictionary<string, string> LedgerUrls(string root)
    {
        string path = Path.Join(root, "guides.jsonl");
        PyObject topics = [];
        if (File.Exists(path))
        {
            foreach (string line in PyText.SplitLines(PyText.ReadText(path)).Where(line => PyText.Strip(line).Length > 0))
            {
                PyObject row = (PyObject)PyJson.Loads(line)!;
                topics.Set(PyText.Str(row["topic_url"]), row);
            }
        }

        Dictionary<string, string> urls = new(StringComparer.Ordinal);
        foreach ((string topic, object? row) in topics.Where(pair => PyObject.Truthy(((PyObject)pair.Value!).Get("file"))))
        {
            urls[PyText.Str(((PyObject)row!)["file"])] = topic;
        }

        return urls;
    }
}
