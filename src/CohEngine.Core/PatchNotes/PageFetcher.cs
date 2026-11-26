using System.Diagnostics;
using System.Globalization;
using System.Text;

using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.PatchNotes;

/// <summary>
/// A paced GET that honours the site's robots.txt. <c>delay</c> seconds separate the start of one request from the start
/// and from the end of the last; a site whose rules could not be read is not fetched from at all.
/// </summary>
internal sealed class PageFetcher
{
    private readonly HttpClient http;
    private readonly string agent;
    private readonly TimeSpan delay;
    private readonly TextWriter error;
    private readonly RobotsRules rules;
    private readonly bool blocked;
    private long? last;

    /// <summary>Initializes a new instance of the <see cref="PageFetcher"/> class and reads the site's robots.txt.</summary>
    /// <param name="http">The HTTP client.</param>
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
        string robots = site + "/robots.txt";
        IReadOnlyList<string> lines = [];
        try
        {
            (int status, byte[] body) = Raw(new Uri(robots));
            if (status is 401 or 403)
            {
                blocked = true;
                error.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{ErrorCodes.E201}: robots.txt refused ({status}) at {robots}; nothing will be fetched"));
            }
            else if (status >= 500)
            {
                blocked = true;
                error.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{ErrorCodes.E202}: robots.txt unreadable ({status}) at {robots}; re-run when the site recovers"));
            }
            else if (status < 400)
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

    /// <summary>The page at <paramref name="url"/>, decoded as UTF-8 with bad bytes replaced, or null when robots.txt disallows it or the fetch fails.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>The page text, or null.</returns>
    public string? Get(string url)
    {
        // System.Uri resolves backslashes and dot segments before sending, so robots.txt judges the path it will send.
        Uri target = new(url);
        if (blocked || !rules.CanFetch(agent, target.PathAndQuery))
        {
            error.WriteLine("skip (robots.txt disallows): " + url);
            return null;
        }

        string reason;
        try
        {
            (int status, byte[] body) = Raw(target);
            if (status < 400)
            {
                return Encoding.UTF8.GetString(body);
            }

            reason = string.Create(CultureInfo.InvariantCulture, $"HTTP Error {status}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            reason = exception.Message;
        }

        error.WriteLine($"{ErrorCodes.E200}: fetch failed for {url}: {reason}");
        return null;
    }

    /// <summary>One request: its status code and body.</summary>
    private (int Status, byte[] Body) Raw(Uri url)
    {
        Pace();
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", agent);
        using HttpResponseMessage response = http.Send(request);
        using MemoryStream body = new();
        response.Content.ReadAsStream().CopyTo(body);
        last = Stopwatch.GetTimestamp();
        return ((int)response.StatusCode, body.ToArray());
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
