using System.Globalization;
using System.Text;
using System.Text.Json;

using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Chain;

/// <summary>A required key missing from an input record; the Python port's <c>KeyError</c>.</summary>
public sealed class MissingKeyException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MissingKeyException"/> class.</summary>
    /// <param name="key">The missing key.</param>
    public MissingKeyException(string key)
        : base(PyText.Repr(key))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MissingKeyException"/> class.</summary>
    public MissingKeyException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MissingKeyException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public MissingKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
