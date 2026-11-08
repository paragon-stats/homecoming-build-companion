namespace CohEngine.Core.Chain;

/// <summary>An input or model error carrying its <c>E&lt;NN&gt;</c> message, the Python port's <c>ValueError</c>.</summary>
public sealed class ChainException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ChainException"/> class.</summary>
    /// <param name="message">The full message, starting with the error code.</param>
    public ChainException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ChainException"/> class.</summary>
    public ChainException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ChainException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public ChainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
