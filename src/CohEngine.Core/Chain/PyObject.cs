using System.Globalization;
using System.Text;

namespace CohEngine.Core.Chain;

/// <summary>An ordered JSON object, as a Python dict is.</summary>
public sealed class PyObject : List<KeyValuePair<string, object?>>
{
    /// <summary>Appends a key and value.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value: null, bool, long, double, string, <see cref="PyObject"/> or a list of those.</param>
    public void Add(string key, object? value)
    {
        Add(new KeyValuePair<string, object?>(key, value));
    }
}
