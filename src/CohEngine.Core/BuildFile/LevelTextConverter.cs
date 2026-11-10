using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CohEngine.Core.BuildFile;

/// <summary>Reads the header <c>Level</c> as text whether the file stores a string or a number; writes a string.</summary>
internal sealed class LevelTextConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.Number
            ? reader.GetInt64().ToString(CultureInfo.InvariantCulture)
            : reader.GetString() ?? throw new JsonException("Level is null");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
