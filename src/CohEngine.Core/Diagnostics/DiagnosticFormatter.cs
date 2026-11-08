using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace CohEngine.Core.Diagnostics;

/// <summary>
/// Renders a run's diagnostics as the text block, as the <c>--format=json</c> array, and as the exit code. The JSON is
/// written by hand to match Python's <c>json.dumps(..., indent=2)</c> byte for byte: ASCII-only, lowercase
/// <c>\u</c> escapes, Python's float text.
/// </summary>
public static class DiagnosticFormatter
{
    /// <summary>Joins a run's diagnostics into one newline-separated block, in order.</summary>
    /// <param name="diagnostics">The diagnostics.</param>
    /// <returns>The text.</returns>
    public static string FormatText(IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return string.Join("\n", diagnostics.Select(diagnostic => diagnostic.FormatText()));
    }

    /// <summary>The <c>--format=json</c> array for a run's diagnostics, in order.</summary>
    /// <param name="diagnostics">The diagnostics.</param>
    /// <returns>The JSON text, without a trailing newline.</returns>
    public static string FormatJson(IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (diagnostics.Count == 0)
        {
            return "[]";
        }

        StringBuilder builder = new("[\n");
        for (int index = 0; index < diagnostics.Count; index++)
        {
            builder.Append("  {\n");
            List<(string Key, object Value)> fields = Fields(diagnostics[index]);
            for (int field = 0; field < fields.Count; field++)
            {
                builder.Append("    ").Append(Quote(fields[field].Key)).Append(": ");
                WriteValue(builder, fields[field].Value, 2);
                builder.Append(field < fields.Count - 1 ? ",\n" : "\n");
            }

            builder.Append(index < diagnostics.Count - 1 ? "  },\n" : "  }\n");
        }

        return builder.Append(']').ToString();
    }

    /// <summary>The standard exit code: any error is 1, else any warning is 2, else 0.</summary>
    /// <param name="diagnostics">The diagnostics.</param>
    /// <returns>The exit code.</returns>
    public static int ExitCode(IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        string[] severities = [.. diagnostics.Select(diagnostic => diagnostic.Severity)];
        if (severities.Contains("error", StringComparer.Ordinal))
        {
            return 1;
        }

        return severities.Contains("warning", StringComparer.Ordinal) ? 2 : 0;
    }

    private static List<(string Key, object Value)> Fields(Diagnostic diagnostic)
    {
        List<(string Key, object Value)> fields =
        [
            ("rule_id", diagnostic.RuleId),
            ("severity", diagnostic.Severity),
            ("path", diagnostic.Path),
            ("line", diagnostic.Line),
            ("column", diagnostic.Column),
            ("message", diagnostic.Message),
        ];
        if (diagnostic.Fix.Length > 0)
        {
            fields.Add(("fix", diagnostic.Fix));
        }

        if (diagnostic.Expected is not null)
        {
            fields.Add(("expected", diagnostic.Expected));
        }

        if (diagnostic.Actual is not null)
        {
            fields.Add(("actual", diagnostic.Actual));
        }

        return fields;
    }

    private static void WriteValue(StringBuilder builder, object value, int depth)
    {
        switch (value)
        {
            case string text:
                builder.Append(Quote(text));
                break;
            case bool flag:
                builder.Append(flag ? "true" : "false");
                break;
            case int or long or BigInteger:
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
            case double number:
                builder.Append(PyText.NumberRepr(number));
                break;
            case IEnumerable<int> or IEnumerable<string>:
                WriteList(builder, [.. ((IEnumerable)value).Cast<object>()], depth);
                break;
            default:
                throw new ArgumentException($"no JSON form for {value.GetType().Name}", nameof(value));
        }
    }

    private static void WriteList(StringBuilder builder, List<object> items, int depth)
    {
        if (items.Count == 0)
        {
            builder.Append("[]");
            return;
        }

        builder.Append("[\n");
        for (int index = 0; index < items.Count; index++)
        {
            builder.Append(' ', (depth + 1) * 2);
            WriteValue(builder, items[index], depth + 1);
            builder.Append(index < items.Count - 1 ? ",\n" : "\n");
        }

        builder.Append(' ', depth * 2).Append(']');
    }

    private static string Quote(string text)
    {
        StringBuilder builder = new("\"");
        foreach (char letter in text)
        {
            builder.Append(letter switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\b' => "\\b",
                '\f' => "\\f",
                _ when letter < ' ' || letter > '~' => "\\u" + ((int)letter).ToString("x4", CultureInfo.InvariantCulture),
                _ => letter.ToString(),
            });
        }

        return builder.Append('"').ToString();
    }
}
