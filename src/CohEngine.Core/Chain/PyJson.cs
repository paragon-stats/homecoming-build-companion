using System.Globalization;
using System.Text;

namespace CohEngine.Core.Chain;

/// <summary>A writer for <c>json.dumps(value, indent=2)</c>.</summary>
public static class PyJson
{
    /// <summary>Serializes a value tree the way Python's <c>json.dumps(indent=2)</c> does.</summary>
    /// <param name="value">null, bool, long, double, string, <see cref="PyObject"/> or <see cref="IReadOnlyList{T}"/> of object.</param>
    /// <returns>The JSON text.</returns>
    public static string Dumps(object? value)
    {
        StringBuilder builder = new();
        Write(builder, value, 0);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, object? value, int depth)
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
            case long whole:
                builder.Append(whole.ToString(CultureInfo.InvariantCulture));
                break;
            case double real:
                builder.Append(double.IsNaN(real) ? "NaN" : double.IsInfinity(real) ? (real > 0 ? "Infinity" : "-Infinity") : PyFloat.Repr(real));
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
                    Write(builder, map[i].Value, depth + 1);
                    builder.Append(i < map.Count - 1 ? ",\n" : "\n");
                }

                builder.Append(close).Append('}');
                break;
            default:
                IReadOnlyList<object?> list = (IReadOnlyList<object?>)value;
                if (list.Count == 0)
                {
                    builder.Append("[]");
                    break;
                }

                builder.Append("[\n");
                for (int i = 0; i < list.Count; i++)
                {
                    builder.Append(pad);
                    Write(builder, list[i], depth + 1);
                    builder.Append(i < list.Count - 1 ? ",\n" : "\n");
                }

                builder.Append(close).Append(']');
                break;
        }
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
