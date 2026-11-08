namespace CohEngine.Ai.Anthropic;

/// <summary>An error the Messages API returned, as a non-2xx response or as an <c>error</c> stream event.</summary>
public sealed class AnthropicApiException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AnthropicApiException"/> class.</summary>
    public AnthropicApiException()
        : this(0, "unknown_error", "The Messages API returned an error.", null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AnthropicApiException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public AnthropicApiException(string message)
        : this(0, "unknown_error", message, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AnthropicApiException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public AnthropicApiException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorType = "unknown_error";
    }

    /// <summary>Initializes a new instance of the <see cref="AnthropicApiException"/> class.</summary>
    /// <param name="status">The HTTP status; 200 for an error that arrived inside a stream.</param>
    /// <param name="errorType">The API's error type, such as <c>authentication_error</c>.</param>
    /// <param name="message">The API's error message.</param>
    /// <param name="requestId">The <c>request-id</c> response header, for a support request.</param>
    public AnthropicApiException(int status, string errorType, string message, string? requestId)
        : base($"Anthropic API {status} {errorType}: {message}")
    {
        Status = status;
        ErrorType = errorType;
        RequestId = requestId;
    }

    /// <summary>Gets the HTTP status.</summary>
    public int Status { get; }

    /// <summary>Gets the API's error type.</summary>
    public string ErrorType { get; }

    /// <summary>Gets the <c>request-id</c> response header, when the API sent one.</summary>
    public string? RequestId { get; }
}
