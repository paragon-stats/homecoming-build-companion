using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace CohEngine.Core.Chain;

/// <summary>A writer for <c>json.dumps(value, indent=2)</c>.</summary>
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
        StringBuilder builder = new();
        Write(builder, value, 0, pythonReprNonFinite);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, object? value, int depth, bool pythonReprNonFinite)
    {
        string pad = new(' ', (depth + 1) * 2);
        string close = new(' ', depth * 2);
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
                builder.Append(double.IsFinite(real) || pythonReprNonFinite ? PyFloat.Repr(real) : JsonNonFinite(real));
                break;
            case string text:
                WriteString(builder, text);
                break;
            case PyObject map:
                if (map.Count == 0)
                {
                    builder.Append("{}");
                    break;
                }

                builder.Append("{\n");
                for (int i = 0; i < map.Count; i++)
                {
                    builder.Append(pad);
                    WriteString(builder, map[i].Key);
                    builder.Append(": ");
                    Write(builder, map[i].Value, depth + 1, pythonReprNonFinite);
                    builder.Append(i < map.Count - 1 ? ",\n" : "\n");
                }

                builder.Append(close).Append('}');
                break;
            case IEnumerable<object?> or IEnumerable<int> or IEnumerable<string>:
                List<object?> list = [.. ((IEnumerable)value).Cast<object?>()];
                if (list.Count == 0)
                {
                    builder.Append("[]");
                    break;
                }

                builder.Append("[\n");
                for (int i = 0; i < list.Count; i++)
                {
                    builder.Append(pad);
                    Write(builder, list[i], depth + 1, pythonReprNonFinite);
                    builder.Append(i < list.Count - 1 ? ",\n" : "\n");
                }

                builder.Append(close).Append(']');
                break;
            default:
                throw new ArgumentException($"no JSON form for {value.GetType().Name}", nameof(value));
        }
    }

    private static string JsonNonFinite(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        return value > 0 ? "Infinity" : "-Infinity";
    }

    private static void WriteString(StringBuilder builder, string text)
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
                    if (letter < ' ' || letter > '~')
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
}
