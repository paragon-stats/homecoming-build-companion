using System.Net;
using System.Text;

namespace CohEngine.Ai.Tests.Anthropic;

/// <summary>Answers every request with one canned response and keeps what was sent; no network.</summary>
internal sealed class FakeAnthropicHandler(HttpStatusCode status, string body, string mediaType = "application/json") : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> Bodies { get; } = [];

    public string? RequestId { get; init; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        HttpResponseMessage response = new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
        if (RequestId is not null)
        {
            response.Headers.Add("request-id", RequestId);
        }

        return response;
    }
}
