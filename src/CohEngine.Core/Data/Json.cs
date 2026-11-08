using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CohEngine.Core.Data;

/// <summary>Reads one Mids reference dump file through source-generated metadata (no reflection).</summary>
internal static class Json
{
    /// <summary>Deserializes a whole file.</summary>
    /// <typeparam name="T">The file's root type.</typeparam>
    /// <param name="path">The file to read.</param>
    /// <param name="typeInfo">The source-generated metadata for the root type.</param>
    /// <returns>The deserialized root.</returns>
    /// <exception cref="InvalidDataException">The file holds a JSON null.</exception>
    public static T Load<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, typeInfo)
            ?? throw new InvalidDataException($"{path} holds null; expected a Mids reference dump");
    }

    /// <summary>Keys records the way a Python dict comprehension does: a repeated key keeps the last record.</summary>
    /// <typeparam name="TSource">The record type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="source">The records in file order.</param>
    /// <param name="key">The key selector.</param>
    /// <param name="value">The value selector.</param>
    /// <returns>The dictionary.</returns>
    public static Dictionary<TKey, TValue> ToLastWinsDictionary<TSource, TKey, TValue>(this IEnumerable<TSource> source, Func<TSource, TKey> key, Func<TSource, TValue> value)
        where TKey : notnull
    {
        Dictionary<TKey, TValue> result = [];
        foreach (TSource item in source)
        {
            result[key(item)] = value(item);
        }

        return result;
    }

    /// <summary>Keys records the way a Python dict comprehension does: a repeated key keeps the last record.</summary>
    /// <typeparam name="TSource">The record type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="source">The records in file order.</param>
    /// <param name="key">The key selector.</param>
    /// <returns>The dictionary.</returns>
    public static Dictionary<TKey, TSource> ToLastWinsDictionary<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> key)
        where TKey : notnull
    {
        return source.ToLastWinsDictionary(key, item => item);
    }
}
