using System.Text.Json;
using System.Text.Json.Serialization;

using CohEngine.Core.Maths;

namespace CohEngine.Core.Data;

/// <summary>Reads a JSON number array through <see cref="F32.Round"/>, for the lists the Python loaders quantize cell by cell.</summary>
public sealed class F32ListJsonConverter : JsonConverter<IReadOnlyList<double>>
{
    /// <inheritdoc/>
    public override IReadOnlyList<double> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"expected a number array, found {reader.TokenType}");
        }

        List<double> cells = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            cells.Add(F32.Round(reader.GetDouble()));
        }

        return cells;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<double> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartArray();
        foreach (double cell in value)
        {
            writer.WriteNumberValue(cell);
        }

        writer.WriteEndArray();
    }
}
