using System.Collections;

namespace CohEngine.Core.Chain;

/// <summary>An ordered JSON object, as a Python dict is.</summary>
public sealed class PyObject : List<KeyValuePair<string, object?>>
{
    /// <summary>Gets a key's value, as <c>dict[key]</c> does.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The value.</returns>
    /// <exception cref="KeyNotFoundException">The key is absent.</exception>
    public object? this[string key] => IndexOf(key) is var index and >= 0 ? this[index].Value : throw new KeyNotFoundException($"'{key}'");

    /// <summary>Python's truth value of a JSON-shaped value: false for null, false, zero, and an empty string, list or object.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The truth value.</returns>
    public static bool Truthy(object? value)
    {
        return value switch
        {
            null => false,
            bool flag => flag,
            string text => text.Length > 0,
            long or int => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) != 0,
            double number => double.IsNaN(number) || Math.Abs(number) > 0,
            ICollection items => items.Count > 0,
            _ => true,
        };
    }

    /// <summary>Appends a key and value.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value: null, bool, int, long, <see cref="System.Numerics.BigInteger"/>, double, string, <see cref="PyObject"/>, or a sequence of object, int or string.</param>
    public void Add(string key, object? value)
    {
        Add(new KeyValuePair<string, object?>(key, value));
    }

    /// <summary>Gets a key's value, as <c>dict.get(key, fallback)</c> does.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The value for an absent key.</param>
    /// <returns>The value.</returns>
    public object? Get(string key, object? fallback = null)
    {
        int index = IndexOf(key);
        return index >= 0 ? this[index].Value : fallback;
    }

    /// <summary>Sets a key's value, as <c>dict[key] = value</c> does: in place when the key exists, else appended.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public void Set(string key, object? value)
    {
        int index = IndexOf(key);
        if (index >= 0)
        {
            this[index] = new KeyValuePair<string, object?>(key, value);
        }
        else
        {
            Add(key, value);
        }
    }

    private int IndexOf(string key)
    {
        return FindIndex(pair => string.Equals(pair.Key, key, StringComparison.Ordinal));
    }
}
