using System.Net;
using System.Text;

namespace CohEngine.Ai.Tests;

internal sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public List<(HttpMethod Method, Uri? Uri, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string sent = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Requests.Add((request.Method, request.RequestUri, sent));
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
