namespace CohEngine.Core.Diagnostics;

/// <summary>An input the engine refuses; the message already carries its <c>E&lt;NN&gt;</c> code (the Python <c>ValueError</c>).</summary>
public sealed class EngineException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="EngineException"/> class.</summary>
    public EngineException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EngineException"/> class.</summary>
    /// <param name="message">The message, starting with its error code.</param>
    public EngineException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EngineException"/> class.</summary>
    /// <param name="message">The message, starting with its error code.</param>
    /// <param name="innerException">The cause.</param>
    public EngineException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
