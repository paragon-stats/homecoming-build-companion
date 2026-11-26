using System.Net;
using System.Text;

namespace CohEngine.Core.Tests.PatchNotes;

/// <summary>A network that answers from a table of URL to body, so no test reaches a real site; every request is recorded.</summary>
internal sealed class FakeSite : HttpMessageHandler
{
    public const string Forum = "https://forums.homecomingservers.com";

    public const string Wiki = "https://archive.paragonwiki.com";

    public FakeSite()
    {
        Routes[Forum + "/robots.txt"] = "User-agent: *\nAllow: /\nDisallow: /search/\n";
    }

    /// <summary>Gets the answers: a body, a status code, a redirect target, or an exception to throw. An unrouted URL is a 404.</summary>
    public Dictionary<string, object> Routes { get; } = new(StringComparer.Ordinal);

    public List<string> Calls { get; } = [];

    public bool Disposed { get; private set; }

    public int Count(string url)
    {
        return Calls.Count(call => string.Equals(call, url, StringComparison.Ordinal));
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Answer(request);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Answer(request));
    }

    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        string url = request.RequestUri!.OriginalString;
        Calls.Add(url);
        return Routes.GetValueOrDefault(url) switch
        {
            string body => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) },
            HttpStatusCode code => new HttpResponseMessage(code) { Content = new ByteArrayContent([]) },
            Uri location => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = location }, Content = new ByteArrayContent([]) },
            Exception exception => throw exception,
            _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new ByteArrayContent([]) },
        };
    }
}
