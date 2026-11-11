using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CohEngine.Core.BuildFile;

/// <summary>Reads the header <c>Level</c> as text whether the file stores a string, a number or a bool; it does not write.</summary>
internal sealed class LevelTextConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType is JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False
            ? Encoding.UTF8.GetString(reader.ValueSpan)
            : reader.GetString() ?? throw new JsonException("Level is null");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("A .mbd file is written by Mbd.Serialize, not by this converter.");
    }
}
