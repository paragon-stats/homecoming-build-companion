using System.Text.Json;
using System.Text.Json.Serialization;

using CohEngine.Core.Maths;

namespace CohEngine.Core.Data;

/// <summary>
/// Reads a JSON number through <see cref="F32.Round"/>, for the fields the Python loaders wrap in <c>f32()</c>.
/// Fields the Python keeps raw stay plain <see cref="double"/> properties without this converter.
/// </summary>
public sealed class F32JsonConverter : JsonConverter<double>
{
    /// <inheritdoc/>
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return F32.Round(reader.GetDouble());
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value);
    }
}
