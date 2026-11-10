namespace CohEngine.Ai.Anthropic;

/// <summary>One dispatched Server-Sent Event: its <c>event:</c> name and its joined <c>data:</c> lines.</summary>
/// <param name="Event">The event name; <c>message</c> when the stream named none.</param>
/// <param name="Data">The data lines joined with line feeds.</param>
internal readonly record struct SseEvent(string Event, string Data);
