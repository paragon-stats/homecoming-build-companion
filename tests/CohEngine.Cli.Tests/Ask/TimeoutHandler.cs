namespace CohEngine.Cli.Tests.Ask;

/// <summary>Fails every request the way an HTTP timeout does, without the caller having cancelled.</summary>
internal sealed class TimeoutHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new TaskCanceledException("the request timed out", new TimeoutException());
    }
}
