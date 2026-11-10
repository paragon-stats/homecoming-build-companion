namespace CohEngine.Ai;

/// <summary>The provider rejected a request. The message never carries the response body or the key.</summary>
public sealed class ChatProviderException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ChatProviderException"/> class.</summary>
    /// <param name="status">The HTTP status the provider returned.</param>
    /// <param name="message">The error text.</param>
    public ChatProviderException(int status, string message)
        : base(message)
    {
        Status = status;
    }

    /// <summary>Gets the HTTP status the provider returned.</summary>
    public int Status { get; }
}
