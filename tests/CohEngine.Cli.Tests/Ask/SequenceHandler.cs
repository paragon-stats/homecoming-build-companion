using System.Net;
using System.Text;

namespace CohEngine.Cli.Tests.Ask;

/// <summary>Answers requests with canned bodies in order, repeating the last one, and keeps what was sent; no network.</summary>
internal sealed class SequenceHandler(HttpStatusCode status, params string[] bodies) : HttpMessageHandler
{
    public List<string> Sent { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Sent.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        string body = bodies[Math.Min(Sent.Count, bodies.Length) - 1];
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
