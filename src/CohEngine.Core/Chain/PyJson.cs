using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace CohEngine.Core.Chain;

/// <summary>A writer for <c>json.dumps</c> and a reader for <c>json.loads</c>, keeping Python's key order and number kinds.</summary>
public static class PyJson
{
    /// <summary>Serializes a value tree the way Python's <c>json.dumps(indent=2)</c> does.</summary>
    /// <param name="value">null, bool, int, long, <see cref="BigInteger"/>, double, string, <see cref="PyObject"/>, or a sequence of object, int or string.</param>
    /// <param name="pythonReprNonFinite">
    /// True writes a non-finite float as Python's <c>repr</c> (<c>nan</c>, <c>inf</c>, <c>-inf</c>), as the diagnostics output
    /// always has; false writes the JSON-style <c>NaN</c>, <c>Infinity</c>, <c>-Infinity</c> that <c>json.dumps</c> writes.
    /// </param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="ArgumentException">The value has no JSON form.</exception>
    public static string Dumps(object? value, bool pythonReprNonFinite = false)
    {
        return Dumps(value, new Style(Indented: true, EnsureAscii: true, pythonReprNonFinite));
    }

    /// <summary>Serializes a value tree the way <c>json.dumps(value, ensure_ascii=..., indent=2 or None)</c> does.</summary>
    /// <param name="value">As for <see cref="Dumps(object?, bool)"/>.</param>
    /// <param name="indented">True for <c>indent=2</c>; false for one line with <c>", "</c> and <c>": "</c> separators.</param>
    /// <param name="ensureAscii">True escapes every character past <c>~</c>; false writes them as they are.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="ArgumentException">The value has no JSON form.</exception>
    public static string Dumps(object? value, bool indented, bool ensureAscii)
    {
        return Dumps(value, new Style(indented, ensureAscii, ReprNonFinite: false));
    }

    /// <summary>Parses JSON the way <c>json.loads</c> does: an object is a <see cref="PyObject"/> in key order, a whole number a <see cref="long"/>, an array a list.</summary>
    /// <param name="text">The JSON text.</param>
    /// <returns>The value tree.</returns>
    /// <exception cref="JsonException">The text is not JSON.</exception>
    public static object? Loads(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text);
        return Value(document.RootElement);
    }

    private static string Dumps(object? value, Style style)
    {
        StringBuilder builder = new();
        Write(builder, value, 0, style);
        return builder.ToString();
    }

    private static object? Value(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                PyObject map = [];
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    map.Set(property.Name, Value(property.Value));
                }

                return map;
            case JsonValueKind.Array:
                return element.EnumerateArray().Select(Value).ToList();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                return Number(element);
            case JsonValueKind.True or JsonValueKind.False:
                return element.GetBoolean();
            default:
                return null;
        }
    }

    /// <summary>A JSON number as Python reads it: an integer of any size, else a float, infinite past the double range.</summary>
    private static object Number(JsonElement element)
    {
        string text = element.GetRawText();
        if (element.TryGetInt64(out long whole))
        {
            return whole;
        }

        if (text.IndexOfAny(['.', 'e', 'E']) < 0)
        {
            return BigInteger.Parse(text, CultureInfo.InvariantCulture);
        }

        if (element.TryGetDouble(out double real))
        {
            return real;
        }

        return text[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
    }

    private static void Write(StringBuilder builder, object? value, int depth, Style style)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;
            case bool flag:
                builder.Append(flag ? "true" : "false");
                break;
            case int or long or BigInteger:
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
            case double real:
                builder.Append(double.IsFinite(real) || style.ReprNonFinite ? PyFloat.Repr(real) : JsonNonFinite(real));
                break;
            case string text:
                WriteString(builder, text, style.EnsureAscii);
                break;
            case PyObject map:
                WriteItems(builder, map.Count, '{', '}', depth, style, (i, inner) =>
                {
                    WriteString(builder, map[i].Key, style.EnsureAscii);
                    builder.Append(": ");
                    Write(builder, map[i].Value, inner, style);
                });
                break;
            case IEnumerable<object?> or IEnumerable<int> or IEnumerable<string>:
                List<object?> list = [.. ((IEnumerable)value).Cast<object?>()];
                WriteItems(builder, list.Count, '[', ']', depth, style, (i, inner) => Write(builder, list[i], inner, style));
                break;
            default:
                throw new ArgumentException($"no JSON form for {value.GetType().Name}", nameof(value));
        }
    }

    private static void WriteItems(StringBuilder builder, int count, char open, char close, int depth, Style style, Action<int, int> item)
    {
        if (count == 0)
        {
            builder.Append(open).Append(close);
            return;
        }

        string pad = style.Indented ? "\n" + new string(' ', (depth + 1) * 2) : string.Empty;
        builder.Append(open);
        for (int i = 0; i < count; i++)
        {
            builder.Append(i == 0 || style.Indented ? string.Empty : " ").Append(pad);
            item(i, depth + 1);
            builder.Append(i < count - 1 ? "," : string.Empty);
        }

        builder.Append(style.Indented ? "\n" + new string(' ', depth * 2) : string.Empty).Append(close);
    }

    private static string JsonNonFinite(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        return value > 0 ? "Infinity" : "-Infinity";
    }

    private static void WriteString(StringBuilder builder, string text, bool ensureAscii)
    {
        builder.Append('"');
        foreach (char letter in text)
        {
            switch (letter)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                default:
                    if (letter < ' ' || (ensureAscii && letter > '~'))
                    {
                        builder.Append("\\u").Append(((int)letter).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(letter);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    /// <param name="Indented">True for <c>indent=2</c>.</param>
    /// <param name="EnsureAscii">True for <c>ensure_ascii=True</c>.</param>
    /// <param name="ReprNonFinite">True writes non-finite floats as Python's <c>repr</c>.</param>
    private readonly record struct Style(bool Indented, bool EnsureAscii, bool ReprNonFinite);
}
