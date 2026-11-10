namespace CohEngine.Cli.Tests.Ask;

/// <summary>Fails every request the way an unreachable server does.</summary>
internal sealed class ThrowingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException("connection refused");
    }
}
