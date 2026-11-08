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

/// <summary>A finding the chain rules report, rendered as <c>json.dumps</c> and the text line of the Python port do.</summary>
/// <param name="RuleId">The rule id.</param>
/// <param name="Severity">error, warning or info.</param>
/// <param name="Message">The message.</param>
/// <param name="Path">The anchor path.</param>
/// <param name="Fix">The optional fix text.</param>
/// <param name="Expected">The optional expected text.</param>
/// <param name="Actual">The optional actual text.</param>
public sealed record ChainDiagnostic(string RuleId, string Severity, string Message, string Path, string Fix = "", string? Expected = null, string? Actual = null)
{
    /// <summary>The machine-parseable line <c>path:1:1: severity: rule: message</c>.</summary>
    /// <returns>The line.</returns>
    public string FormatText()
    {
        return $"{Path}:1:1: {Severity}: {RuleId}: {Message}";
    }

    /// <summary>The JSON object: required keys always, optional context when set.</summary>
    /// <returns>The ordered object.</returns>
    public PyObject ToJson()
    {
        PyObject result = new()
        {
            { "rule_id", RuleId },
            { "severity", Severity },
            { "path", Path },
            { "line", 1L },
            { "column", 1L },
            { "message", Message },
        };
        if (Fix.Length > 0)
        {
            result.Add("fix", Fix);
        }

        if (Expected is not null)
        {
            result.Add("expected", Expected);
        }

        if (Actual is not null)
        {
            result.Add("actual", Actual);
        }

        return result;
    }
}

/// <summary>An ordered JSON object, as a Python dict is.</summary>
public sealed class PyObject : List<KeyValuePair<string, object?>>
{
    /// <summary>Appends a key and value.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value: null, bool, long, double, string, <see cref="PyObject"/> or a list of those.</param>
    public void Add(string key, object? value)
    {
        Add(new KeyValuePair<string, object?>(key, value));
    }
}
