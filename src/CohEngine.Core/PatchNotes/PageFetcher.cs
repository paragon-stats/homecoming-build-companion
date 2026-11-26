using System.Diagnostics;
using System.Globalization;
using System.Text;

using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.PatchNotes;

/// <summary>
/// A paced GET of one site's pages that honours its robots.txt. <c>delay</c> seconds separate the start of one request
/// from the start and from the end of the last; a site whose rules could not be read is not fetched from at all, and a
/// link or a redirect to another host is not followed.
/// </summary>
internal sealed class PageFetcher
{
    private const int MaxRedirects = 10;

    private readonly HttpClient http;
    private readonly string agent;
    private readonly TimeSpan delay;
    private readonly TextWriter error;
    private readonly Uri site;
    private readonly RobotsRules rules;
    private readonly bool blocked;
    private long? last;

    /// <summary>Initializes a new instance of the <see cref="PageFetcher"/> class and reads the site's robots.txt.</summary>
    /// <param name="http">The HTTP client; it must not follow redirects itself.</param>
    /// <param name="agent">The user agent.</param>
    /// <param name="delay">The least gap between requests, in seconds.</param>
    /// <param name="site">The site's base URL, with no trailing slash.</param>
    /// <param name="error">Where failures are reported.</param>
    public PageFetcher(HttpClient http, string agent, double delay, string site, TextWriter error)
    {
        this.http = http;
        this.agent = agent;
        this.delay = delay > 0 ? TimeSpan.FromSeconds(delay) : TimeSpan.Zero;
        this.error = error;
        this.site = new Uri(site);
        string robots = site + "/robots.txt";
        IReadOnlyList<string> lines = [];
        try
        {
            (int status, byte[] body, _) = Raw(new Uri(robots));
            if (status is 401 or 403)
            {
                blocked = true;
                error.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{ErrorCodes.E201}: robots.txt refused ({status}) at {robots}; nothing will be fetched; ask the site's operator for access, or leave this source out with --source"));
            }
            else if (status >= 500)
            {
                blocked = true;
                error.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{ErrorCodes.E202}: robots.txt unreadable ({status}) at {robots}; re-run when the site recovers"));
            }
            else if (status < 300)
            {
                lines = PyText.SplitLines(Encoding.UTF8.GetString(body));
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            blocked = true;
            error.WriteLine($"{ErrorCodes.E202}: robots.txt unreadable at {robots}: {exception.Message}; re-run when the site recovers");
        }

        rules = new RobotsRules(lines);
    }

    /// <summary>The page at <paramref name="url"/>, decoded as UTF-8 with bad bytes replaced, or null when it is not on the site, robots.txt disallows it, or the fetch fails.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>The page text, or null.</returns>
    public string? Get(string url)
    {
        // System.Uri resolves backslashes and dot segments before sending, so robots.txt judges the URI it will send.
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? target))
        {
            error.WriteLine($"{ErrorCodes.E200}: fetch failed for {url}: not a valid absolute URL; check the link on the page that named it");
            return null;
        }

        if (!OnSite(target))
        {
            error.WriteLine($"{ErrorCodes.E222}: {url} is not on {site.GetLeftPart(UriPartial.Authority)}; it was not fetched; only links to that site are followed");
            return null;
        }

        string robots = site.GetLeftPart(UriPartial.Authority) + "/robots.txt";
        if (blocked)
        {
            error.WriteLine($"{ErrorCodes.E221}: {url} was not fetched because {robots} could not be read (see above); re-run when the site answers");
            return null;
        }

        if (!rules.CanFetch(agent, target.AbsoluteUri))
        {
            error.WriteLine($"{ErrorCodes.E220}: {robots} disallows {url}; it was not fetched; leave the page out, or ask the site's operator to allow it");
            return null;
        }

        string reason;
        try
        {
            (int status, byte[] body, Uri answered) = Raw(target);
            if (status < 300)
            {
                return Encoding.UTF8.GetString(body);
            }

            if (!OnSite(answered))
            {
                error.WriteLine($"{ErrorCodes.E222}: {url} redirects to {answered}, which is not on {site.GetLeftPart(UriPartial.Authority)}; it was not followed");
                return null;
            }

            reason = string.Create(CultureInfo.InvariantCulture, $"HTTP Error {status}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            reason = exception.Message;
        }

        error.WriteLine($"{ErrorCodes.E200}: fetch failed for {url}: {reason}; re-run when the site answers, or check the link on the page that named it");
        return null;
    }

    private static bool IsRedirect(int status)
    {
        return status is 301 or 302 or 303 or 307 or 308;
    }

    private bool OnSite(Uri url)
    {
        return string.Equals(url.Scheme, site.Scheme, StringComparison.OrdinalIgnoreCase) && string.Equals(url.Host, site.Host, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>One request, following redirects that stay on the site: the last status, its body, and the URL it answered for or redirects to.</summary>
    private (int Status, byte[] Body, Uri At) Raw(Uri url)
    {
        Uri answered = url;
        for (int hop = 0; ; hop++)
        {
            Pace();
            using HttpRequestMessage request = new(HttpMethod.Get, answered);
            request.Headers.TryAddWithoutValidation("User-Agent", agent);
            using HttpResponseMessage response = http.Send(request);
            int status = (int)response.StatusCode;
            if (hop < MaxRedirects && IsRedirect(status) && response.Headers.Location is { } location)
            {
                answered = new Uri(answered, location);
                if (OnSite(answered))
                {
                    continue;
                }

                return (status, [], answered);
            }

            using MemoryStream body = new();
            response.Content.ReadAsStream().CopyTo(body);
            last = Stopwatch.GetTimestamp();
            return (status, body.ToArray(), answered);
        }
    }

    /// <summary>Waits until <c>delay</c> has passed since the last request, then stamps this one.</summary>
    private void Pace()
    {
        TimeSpan wait = last is long previous ? delay - Stopwatch.GetElapsedTime(previous) : TimeSpan.Zero;
        if (wait > TimeSpan.Zero)
        {
            Thread.Sleep(wait);
        }

        last = Stopwatch.GetTimestamp();
    }
}
