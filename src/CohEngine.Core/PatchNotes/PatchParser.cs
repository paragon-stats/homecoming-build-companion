using System.Text.RegularExpressions;

using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.PatchNotes;

/// <summary>Reads the three sources' pages: the Release Notes application, the beta forum threads and the Paragon Wiki Archive.</summary>
internal static partial class PatchParser
{
    /// <summary>The live Release Notes application's source name.</summary>
    public const string ReleaseNotes = "release-notes";

    /// <summary>The beta forum sections' source name.</summary>
    public const string ForumBeta = "forum-beta";

    /// <summary>The wiki archive's source name.</summary>
    public const string ParagonWiki = "paragonwiki";

    /// <summary>The forum's base URL.</summary>
    public const string Forum = "https://forums.homecomingservers.com";

    /// <summary>The wiki archive's base URL.</summary>
    public const string Wiki = "https://archive.paragonwiki.com";

    private const int CategoryFontPx = 18;

    private const string WikiContents = "Contents";

    private const string General = "General";

    /// <summary>Gets the pattern of a Release Notes entry URL in quotes.</summary>
    [GeneratedRegex(@"['""](?<url>https://forums\.homecomingservers\.com/patch-notes/(?<issue>issue-\d+)/(?<page>page-\d+)/[a-z0-9-]+-r(?<revision>\d+)/)['""]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Entry { get; }

    [GeneratedRegex(@"['""](?<url>https://forums\.homecomingservers\.com/patch-notes/issue-\d+/page-\d+/)['""]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PageCategory { get; }

    [GeneratedRegex(@"<section\s[^>]*class=['""][^'""]*ipsType_richText[^'""]*['""][^>]*>(?<body>.*?)</section>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Body { get; }

    [GeneratedRegex(@"issue-(?<issue>\d+).*?page-(?<page>\d+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Series { get; }

    [GeneratedRegex(@"release-candidate-(?<number>\d+)$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Candidate { get; }

    [GeneratedRegex(@"href=['""](?<path>/wiki/Patch_Notes/(?<date>\d{4}-\d{2}-\d{2})[^'""#?]*)['""]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WikiPage { get; }

    [GeneratedRegex(@"class=['""][^'""]*mw-parser-output[^'""]*['""][^>]*>(?<body>.*)", RegexOptions.Singleline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WikiBody { get; }

    [GeneratedRegex(@"<h(?<level>[1-6])\b[^>]*>(?<inner>.*?)</h(?-i:\k<level>)>|<li\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WikiToken { get; }

    [GeneratedRegex(@"<span\s[^>]*class=['""]mw-headline['""][^>]*>(?<label>.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WikiHeadline { get; }

    [GeneratedRegex(@"issue\s*(?<number>\d+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WikiIssue { get; }

    [GeneratedRegex(@"<p\b[^>]*>(?<para>.*?)</p>|<li\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Token { get; }

    [GeneratedRegex(@"<li\b|</[uo]l>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ItemEnd { get; }

    [GeneratedRegex(@"font-size:\s*(?<px>\d+)px", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FontSize { get; }

    [GeneratedRegex(@"<(?:strong|b)\b[^>]*>(?<label>.*?)</(?:strong|b)>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Bold { get; }

    /// <summary>Gets the power path at a line's head: one to three <c>&gt;</c> separators, then <c>-</c> or <c>:</c>.</summary>
    [GeneratedRegex(@"^(?<path>[^:>\n]{1,80}(?:\s*>\s*[^:>\n]{1,80}){1,3}?)\s*[-:]\s+(?<text>.+)$", RegexOptions.Singleline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PowerPath { get; }

    /// <summary>Gets the zero-width marks pasted into forum posts; a JSON linter rejects them and they carry nothing.</summary>
    [GeneratedRegex("[\u200b\u200c\u200d\u2060\ufeff]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ZeroWidth { get; }

    /// <summary>Every Issue &gt; Page category URL the category browser links, once each, in page order.</summary>
    /// <param name="html">The category browser page.</param>
    /// <returns>The URLs.</returns>
    public static IReadOnlyList<string> ListCategories(string html)
    {
        return [.. PageCategory.Matches(html).Select(match => match.Groups["url"].Value).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Every entry URL a page-category listing links, once each, in listing order.</summary>
    /// <param name="html">The listing page.</param>
    /// <returns>The URLs.</returns>
    public static IReadOnlyList<string> ListEntries(string html)
    {
        return [.. Entry.Matches(html).Select(match => match.Groups["url"].Value).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The change lines of a forum-side body. A bold-only paragraph is a heading: 18px and up a category, 16px a
    /// subsection or, inside a Powers category, a powerset when it names a canonical set; unsized under a powerset, the power.
    /// </summary>
    /// <param name="html">The body.</param>
    /// <param name="powersets">The canonical set names in lower case.</param>
    /// <returns>The lines.</returns>
    public static List<Change> ParseChanges(string html, IReadOnlySet<string> powersets)
    {
        List<Change> changes = [];
        string category = string.Empty;
        string subsection = string.Empty;
        string powerset = string.Empty;
        string power = string.Empty;
        for (Match token = Token.Match(html); token.Success; token = token.NextMatch())
        {
            Group paragraph = token.Groups["para"];
            if (!paragraph.Success)
            {
                string text = ItemText(html, token);
                if (text.Length > 0)
                {
                    changes.Add(ChangeOf(text, category.Length > 0 ? category : General, subsection, powerset, power));
                }

                continue;
            }

            string label = Heading(paragraph.Value);
            if (label.Length == 0)
            {
                continue;
            }

            Match size = FontSize.Match(paragraph.Value);
            long fontPx = size.Success ? PyText.Number(size.Groups["px"].Value) : 0;
            string bare = label.TrimEnd(',', ':');
            if (fontPx >= CategoryFontPx || category.Length == 0)
            {
                (category, subsection, powerset, power) = (label, string.Empty, string.Empty, string.Empty);
            }
            else if (fontPx == 0 && powerset.Length > 0)
            {
                power = bare;
            }
            else if (category.Contains("power", StringComparison.OrdinalIgnoreCase) && powersets.Contains(bare.ToLowerInvariant()))
            {
                (powerset, power) = (bare, string.Empty);
            }
            else
            {
                (subsection, powerset, power) = (label, string.Empty, string.Empty);
            }
        }

        return changes;
    }

    /// <summary>One Release Notes application entry page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="url">The entry's URL.</param>
    /// <param name="powersets">The canonical set names in lower case.</param>
    /// <returns>The entry.</returns>
    public static PatchEntry ParseEntry(string page, string url, IReadOnlySet<string> powersets)
    {
        Match match = Entry.Match($"\"{url}\"");
        bool named = match.Success && match.Index == 0;
        Match published = HtmlText.Time.Match(page);
        Match body = Body.Match(page);
        return new PatchEntry
        {
            Title = HtmlText.PageTitle(page),
            Url = url,
            Published = published.Success ? published.Groups["when"].Value : string.Empty,
            Issue = named ? match.Groups["issue"].Value : string.Empty,
            Page = named ? match.Groups["page"].Value : string.Empty,
            Revision = named ? PyText.Number(match.Groups["revision"].Value) : 0,
            Changes = body.Success ? ParseChanges(body.Groups["body"].Value, powersets) : [],
        };
    }

    /// <summary>The issue and page a beta thread's slug names, or two empty strings for an interim patch.</summary>
    /// <param name="topicUrl">The thread URL.</param>
    /// <returns>For example <c>("issue-27", "page-3")</c>.</returns>
    public static (string Issue, string Page) SeriesOf(string topicUrl)
    {
        Match match = Series.Match(Tail(topicUrl));
        return match.Success ? ("issue-" + match.Groups["issue"].Value, "page-" + match.Groups["page"].Value) : (string.Empty, string.Empty);
    }

    /// <summary>
    /// The beta threads worth reading: the last-posted thread of each labelled series, the last of each run of unlabelled
    /// numbered release candidates (a number that does not rise starts a new run), and every other interim patch. Order is
    /// the topic id, which rises as threads are posted.
    /// </summary>
    /// <param name="topics">The listed thread URLs.</param>
    /// <returns>The URLs to read.</returns>
    public static IReadOnlyList<string> BetaCandidates(IEnumerable<string> topics)
    {
        List<((string Issue, string Page) Series, long Id, string Url)> latest = [];
        List<(long Id, long Number, string Url)> numbered = [];
        List<string> interim = [];
        foreach (string topic in topics)
        {
            string slug = Tail(topic);
            string head = slug.Split('-', 2)[0];
            long topicId = head.Length > 0 && head.All(char.IsAsciiDigit) ? PyText.Number(head) : 0;
            (string Issue, string Page) series = SeriesOf(topic);
            Match candidate = Candidate.Match(slug);
            if (series != (string.Empty, string.Empty))
            {
                int index = latest.FindIndex(row => row.Series == series);
                if (index < 0)
                {
                    latest.Add((series, topicId, topic));
                }
                else if (topicId > latest[index].Id)
                {
                    latest[index] = (series, topicId, topic);
                }
            }
            else if (candidate.Success)
            {
                numbered.Add((topicId, PyText.Number(candidate.Groups["number"].Value), topic));
            }
            else
            {
                interim.Add(topic);
            }
        }

        List<string> finals = [];
        (long Number, string Url)? last = null;
        foreach ((_, long number, string url) in numbered.OrderBy(row => row.Id).ThenBy(row => row.Number).ThenBy(row => row.Url, StringComparer.Ordinal))
        {
            if (last is { } previous && number <= previous.Number)
            {
                finals.Add(previous.Url);
            }

            last = (number, url);
        }

        if (last is { } final)
        {
            finals.Add(final.Url);
        }

        return [.. latest.Select(row => row.Url), .. finals, .. interim];
    }

    /// <summary>A beta patch-notes thread's opening post.</summary>
    /// <param name="page">The thread page.</param>
    /// <param name="url">The thread URL.</param>
    /// <param name="powersets">The canonical set names in lower case.</param>
    /// <returns>The entry.</returns>
    public static PatchEntry ParseBetaThread(string page, string url, IReadOnlySet<string> powersets)
    {
        (string issue, string pageName) = SeriesOf(url);
        (string Html, string PostedAt)? opener = HtmlText.Opener(page);
        return new PatchEntry
        {
            Title = HtmlText.PageTitle(page),
            Url = url,
            Published = opener?.PostedAt ?? string.Empty,
            Issue = issue,
            Page = pageName,
            Source = ForumBeta,
            Changes = opener is { } post ? ParseChanges(post.Html, powersets) : [],
        };
    }

    /// <summary>Every dated <c>Patch_Notes/YYYY-MM-DD</c> page the wiki index links, once each, oldest first.</summary>
    /// <param name="html">The index page.</param>
    /// <returns>The page URLs.</returns>
    public static IReadOnlyList<string> WikiPages(string html)
    {
        IEnumerable<string> pages = WikiPage.Matches(html)
            .Select(match => (Path: match.Groups["path"].Value, Date: match.Groups["date"].Value))
            .DistinctBy(page => page.Path, StringComparer.Ordinal)
            .OrderBy(page => page.Date, StringComparer.Ordinal)
            .ThenBy(page => page.Path, StringComparer.Ordinal)
            .Select(page => Wiki + page.Path);
        return [.. pages];
    }

    /// <summary>One archived wiki patch-notes page: h2 headings are categories, deeper ones subsections.</summary>
    /// <param name="page">The page.</param>
    /// <param name="url">The page URL.</param>
    /// <returns>The entry.</returns>
    public static PatchEntry ParseWikiPage(string page, string url)
    {
        string tail = Tail(url);
        string? rawTitle = HtmlText.RawTitle(page);
        PatchEntry entry = new()
        {
            Title = rawTitle is null ? string.Empty : PyText.Fold(rawTitle).Split(" - ")[0],
            Url = url,
            Published = tail[..Math.Min(10, tail.Length)],
            Issue = string.Empty,
            Page = string.Empty,
            Source = ParagonWiki,
        };
        Match body = WikiBody.Match(page);
        if (!body.Success)
        {
            return entry;
        }

        string html = body.Groups["body"].Value;
        int footer = html.IndexOf("printfooter", StringComparison.Ordinal);
        html = footer >= 0 ? html[..footer] : html;
        Match titleIssue = WikiIssue.Match(entry.Title);
        entry.Issue = titleIssue.Success ? "issue-" + titleIssue.Groups["number"].Value : string.Empty;
        (string Category, string Subsection) headings = (string.Empty, string.Empty);
        for (Match token = WikiToken.Match(html); token.Success; token = token.NextMatch())
        {
            if (token.Groups["level"].Success)
            {
                headings = WikiHeading(entry, token, headings);
                continue;
            }

            string text = headings.Category is WikiContents ? string.Empty : ItemText(html, token);
            if (text.Length > 0)
            {
                entry.Changes.Add(ChangeOf(text, headings.Category.Length > 0 ? headings.Category : General, headings.Subsection, string.Empty, string.Empty));
            }
        }

        return entry;
    }

    /// <summary>The file name's slug: the application entry's, <c>beta-</c> plus the thread's, or <c>paragonwiki-</c> plus the page date.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The slug.</returns>
    public static string Slug(PatchEntry entry)
    {
        string tail = Tail(entry.Url);
        return entry.Source switch
        {
            ParagonWiki => "paragonwiki-" + tail.ToLowerInvariant().Replace('_', '-'),
            ForumBeta => "beta-" + tail.Split('-', 2)[^1],
            _ => tail,
        };
    }

    /// <summary>
    /// The category and subsection after a wiki heading: h1 (the page heading) clears both, h2 opens a category, a deeper
    /// one a subsection, or the first category when none is open. The first heading that names an Issue fills the entry's.
    /// </summary>
    private static (string Category, string Subsection) WikiHeading(PatchEntry entry, Match token, (string Category, string Subsection) headings)
    {
        Match headline = WikiHeadline.Match(token.Groups["inner"].Value);
        string label = Clean(headline.Success ? headline.Groups["label"].Value : token.Groups["inner"].Value);
        Match headingIssue = WikiIssue.Match(label);
        if (headingIssue.Success && entry.Issue.Length == 0)
        {
            entry.Issue = "issue-" + headingIssue.Groups["number"].Value;
        }

        int level = token.Groups["level"].Value[0] - '0';
        if (level == 1)
        {
            return (string.Empty, string.Empty);
        }

        return level == 2 || headings.Category.Length == 0 ? (label, string.Empty) : (headings.Category, label);
    }

    /// <summary>Plain text with zero-width marks dropped and every run of white space folded to one space.</summary>
    private static string Clean(string fragment)
    {
        return PyText.Fold(ZeroWidth.Replace(HtmlText.Strip(fragment), string.Empty));
    }

    /// <summary>The bold label a paragraph holds when it holds nothing else, or empty for prose.</summary>
    private static string Heading(string inner)
    {
        string text = Clean(inner);
        Match bold = Bold.Match(inner);
        return text.Length > 0 && bold.Success && string.Equals(Clean(bold.Groups["label"].Value), text, StringComparison.Ordinal) ? text : string.Empty;
    }

    /// <summary>A list item's text: from its tag to the next item or the end of its list.</summary>
    private static string ItemText(string html, Match token)
    {
        int start = token.Index + token.Length;
        Match end = ItemEnd.Match(html, start);
        return Clean(html[start..(end.Success ? end.Index : html.Length)]);
    }

    private static Change ChangeOf(string text, string category, string subsection, string powerset, string power)
    {
        Match path = PowerPath.Match(text);
        if (!path.Success)
        {
            return new Change(category, subsection, string.Empty, powerset, power, text);
        }

        string[] parts = [.. path.Groups["path"].Value.Split('>').Select(PyText.Fold)];
        return new Change(category, subsection, string.Join(" > ", parts[..^2]), parts[^2], parts[^1], PyText.Fold(path.Groups["text"].Value));
    }

    /// <summary>The last path segment of a URL, a trailing slash ignored.</summary>
    private static string Tail(string url)
    {
        return url.TrimEnd('/').Split('/')[^1];
    }
}
