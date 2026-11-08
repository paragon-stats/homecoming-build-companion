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
}
