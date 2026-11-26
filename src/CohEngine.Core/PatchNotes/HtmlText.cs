using System.Globalization;
using System.Net;
using System.Numerics;
using System.Text.RegularExpressions;

using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.PatchNotes;

/// <summary>Forum and wiki page text: tags stripped, entities decoded as Python's <c>html.unescape</c> does, titles and the opening post found.</summary>
internal static partial class HtmlText
{
    /// <summary>The named references Python decodes without a closing semicolon (the HTML legacy set).</summary>
    private static readonly HashSet<string> LegacyNames = new(
        "Aacute aacute Acirc acirc acute AElig aelig Agrave agrave AMP amp Aring aring Atilde atilde Auml auml brvbar Ccedil ccedil cedil cent COPY copy curren deg divide Eacute eacute Ecirc ecirc Egrave egrave ETH eth Euml euml frac12 frac14 frac34 GT gt Iacute iacute Icirc icirc iexcl Igrave igrave iquest Iuml iuml laquo LT lt macr micro middot nbsp not Ntilde ntilde Oacute oacute Ocirc ocirc Ograve ograve ordf ordm Oslash oslash Otilde otilde Ouml ouml para plusmn pound QUOT quot raquo REG reg sect shy sup1 sup2 sup3 szlig THORN thorn times Uacute uacute Ucirc ucirc Ugrave ugrave uml Uuml uuml Yacute yacute yen yuml".Split(' '),
        StringComparer.Ordinal);

    /// <summary>The Windows-1252 characters a numeric reference in 0x80 to 0x9F stands for, and the two other remapped numbers.</summary>
    private static readonly Dictionary<int, string> RemappedNumbers = new()
    {
        [0x00] = "\ufffd",
        [0x0d] = "\r",
        [0x80] = "\u20ac",
        [0x81] = "\u0081",
        [0x82] = "\u201a",
        [0x83] = "\u0192",
        [0x84] = "\u201e",
        [0x85] = "\u2026",
        [0x86] = "\u2020",
        [0x87] = "\u2021",
        [0x88] = "\u02c6",
        [0x89] = "\u2030",
        [0x8a] = "\u0160",
        [0x8b] = "\u2039",
        [0x8c] = "\u0152",
        [0x8d] = "\u008d",
        [0x8e] = "\u017d",
        [0x8f] = "\u008f",
        [0x90] = "\u0090",
        [0x91] = "\u2018",
        [0x92] = "\u2019",
        [0x93] = "\u201c",
        [0x94] = "\u201d",
        [0x95] = "\u2022",
        [0x96] = "\u2013",
        [0x97] = "\u2014",
        [0x98] = "\u02dc",
        [0x99] = "\u2122",
        [0x9a] = "\u0161",
        [0x9b] = "\u203a",
        [0x9c] = "\u0153",
        [0x9d] = "\u009d",
        [0x9e] = "\u017e",
        [0x9f] = "\u0178",
    };

    /// <summary>Gets the first <c>&lt;time datetime=...&gt;</c> value.</summary>
    [GeneratedRegex(@"<time\s[^>]*datetime=['""](?<when>[^'""]+)['""]", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Time { get; }

    [GeneratedRegex(@"<br\s*/?>|</(?:p|li|div|tr|h[1-6]|pre)\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LineBreak { get; }

    [GeneratedRegex("<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Tag { get; }

    [GeneratedRegex(@"&(?<ref>#[0-9]+;?|#[xX][0-9a-fA-F]+;?|[^\t\n\f <&#;]{1,32};?)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CharacterReference { get; }

    [GeneratedRegex("<title>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Title { get; }

    [GeneratedRegex(@"\s*-\s*[^-]+\s*-\s*(?:Homecoming|Invision Community)$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TitleSuffix { get; }

    [GeneratedRegex(@"<article\s[^>]*id=['""]elComment_\d+['""]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Article { get; }

    [GeneratedRegex(@"<div\s[^>]*data-role=['""]commentContent['""][^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Content { get; }

    [GeneratedRegex(@"<(?<close>/?)div\b", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DivTag { get; }

    [GeneratedRegex(@"['""](?<url>https://[a-z0-9.-]+/(?:index\.php\?/)?topic/\d+-[^'""#?/]*/)['""]", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TopicLink { get; }

    [GeneratedRegex(@"(?=<li[^>]*class=['""](?:[^'""]*\s)?ipsDataItem[\s'""])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ListingRow { get; }

    [GeneratedRegex(@"^<li[^>]*(?:data-forumid=|cForumRow)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ForumCard { get; }

    [GeneratedRegex(@"<ul[^>]*class=['""]ipsPagination['""][^>]*data-pages=['""](?<pages>\d+)['""]", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PaginationCount { get; }

    [GeneratedRegex(@"class=['""]ipsPagination['""].{0,4000}?href=['""](?<base>https://[a-z0-9.-]+/(?:index\.php\?/)?forum/\d+-[^'""]*?)/page/\d+/['""]", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PaginationHref { get; }

    /// <summary>An HTML fragment as plain text: block closers and <c>&lt;br&gt;</c> become line breaks, other tags go, entities are decoded and each line is stripped.</summary>
    /// <param name="fragment">The HTML.</param>
    /// <returns>The text.</returns>
    public static string Strip(string fragment)
    {
        string text = Unescape(Tag.Replace(LineBreak.Replace(fragment, "\n"), string.Empty));
        text = text.Replace('\u00a0', ' ').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return string.Join('\n', text.Split('\n').Select(PyText.Strip));
    }

    /// <summary>Decodes character references as Python's <c>html.unescape</c> does.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The decoded text.</returns>
    public static string Unescape(string text)
    {
        return text.Contains('&', StringComparison.Ordinal) ? CharacterReference.Replace(text, match => Decode(match.Groups["ref"].Value)) : text;
    }

    /// <summary>The thread title from <c>&lt;title&gt;</c>, decoded, with the section and site suffix trimmed.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The title, or empty.</returns>
    public static string PageTitle(string page)
    {
        Match match = Title.Match(page);
        return match.Success ? PyText.Strip(TitleSuffix.Replace(PyText.Strip(Unescape(match.Groups["title"].Value)), string.Empty)) : string.Empty;
    }

    /// <summary>The raw <c>&lt;title&gt;</c> text, or null when the page has none.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The title text.</returns>
    public static string? RawTitle(string page)
    {
        Match match = Title.Match(page);
        return match.Success ? match.Groups["title"].Value : null;
    }

    /// <summary>The first post of a topic page: its inner HTML and its time, or null when the page has no post.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The opening post.</returns>
    public static (string Html, string PostedAt)? Opener(string page)
    {
        Match first = Article.Match(page);
        if (!first.Success)
        {
            return null;
        }

        Match next = first.NextMatch();
        string article = page[first.Index..(next.Success ? next.Index : page.Length)];
        Match posted = Time.Match(article);
        Match content = Content.Match(article);
        return (content.Success ? InnerDiv(article, content.Index + content.Length) : string.Empty, posted.Success ? posted.Groups["when"].Value : string.Empty);
    }

    /// <summary>Each topic a forum listing's rows link, once each, in listing order; a sub-forum card is not a topic.</summary>
    /// <param name="listing">The listing page.</param>
    /// <returns>The topic URLs.</returns>
    public static IEnumerable<string> TopicUrls(string listing)
    {
        List<int> starts = [.. ListingRow.Matches(listing).Select(match => match.Index)];
        return starts
            .Select((start, index) => listing[start..(index + 1 < starts.Count ? starts[index + 1] : listing.Length)])
            .Where(chunk => !ForumCard.IsMatch(chunk))
            .Select(chunk => TopicLink.Match(chunk))
            .Where(topic => topic.Success)
            .Select(topic => topic.Groups["url"].Value)
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>The page count a listing's pagination declares, or 1.</summary>
    /// <param name="page">The listing page.</param>
    /// <returns>The page count.</returns>
    public static long LastPageNumber(string page)
    {
        Match declared = PaginationCount.Match(page);
        return declared.Success ? PyText.Number(declared.Groups["pages"].Value) : 1;
    }

    /// <summary>The URL a listing's own pagination links build on, else the requested URL without its trailing slash.</summary>
    /// <param name="page">The listing page.</param>
    /// <param name="requested">The URL that was fetched.</param>
    /// <returns>The base URL.</returns>
    public static string ListingBase(string page, string requested)
    {
        Match match = PaginationHref.Match(page);
        return match.Success ? match.Groups["base"].Value : requested.TrimEnd('/');
    }

    /// <summary>The inner HTML of the <c>&lt;div&gt;</c> whose opening tag ends at <paramref name="start"/>, counting nested divs.</summary>
    private static string InnerDiv(string page, int start)
    {
        int depth = 1;
        for (Match tag = DivTag.Match(page, start); tag.Success; tag = tag.NextMatch())
        {
            depth += tag.Groups["close"].Length > 0 ? -1 : 1;
            if (depth == 0)
            {
                return page[start..tag.Index];
            }
        }

        return page[start..];
    }

    private static string Decode(string reference)
    {
        if (reference[0] != '#')
        {
            return Named(reference);
        }

        bool hex = reference[1] is 'x' or 'X';
        string digits = reference[(hex ? 2 : 1)..].TrimEnd(';');
        BigInteger number = hex ? BigInteger.Parse("0" + digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture) : BigInteger.Parse(digits, CultureInfo.InvariantCulture);
        if (number <= 0x9f && RemappedNumbers.TryGetValue((int)number, out string? remapped))
        {
            return remapped;
        }

        if ((number >= 0xd800 && number <= 0xdfff) || number > 0x10ffff)
        {
            return "\ufffd";
        }

        return IsInvalidCodePoint((int)number) ? string.Empty : char.ConvertFromUtf32((int)number);
    }

    private static bool IsInvalidCodePoint(int number)
    {
        return number is (>= 0x1 and <= 0x8) or 0xb or (>= 0xe and <= 0x1f) or (>= 0x7f and <= 0x9f) or (>= 0xfdd0 and <= 0xfdef) || (number & 0xfffe) == 0xfffe;
    }

    /// <summary>A named reference: the whole name when it is known, else its longest legacy prefix, else the text unchanged.</summary>
    private static string Named(string name)
    {
        // shortcut: .NET knows only the HTML 4 names, so an HTML 5-only one such as &NewLine; stays as text, carry the HTML 5 table if a page uses one.
        if (name.EndsWith(';') && WebUtility.HtmlDecode("&" + name) is var decoded && !string.Equals(decoded, "&" + name, StringComparison.Ordinal))
        {
            return decoded;
        }

        // Every legacy name is also a name with its semicolon.
        string bare = name.EndsWith(';') ? name[..^1] : name;
        if (LegacyNames.Contains(bare))
        {
            return Legacy(bare);
        }

        for (int length = name.Length - 1; length > 1; length--)
        {
            if (LegacyNames.Contains(name[..length]))
            {
                return Legacy(name[..length]) + name[length..];
            }
        }

        return "&" + name;
    }

    private static string Legacy(string name)
    {
        return WebUtility.HtmlDecode("&" + (name is "AMP" or "COPY" or "GT" or "LT" or "QUOT" or "REG" ? name.ToLowerInvariant() : name) + ";");
    }
}
