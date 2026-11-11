using CohEngine.Core.Chain;

namespace CohEngine.Core.Diagnostics;

/// <summary>
/// Renders a run's diagnostics as the text block, as the <c>--format=json</c> array, and as the exit code. The JSON is
/// written by <see cref="PyJson"/> to match Python's <c>json.dumps(..., indent=2)</c> byte for byte, with non-finite
/// floats in Python's <c>repr</c> text.
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
        return PyJson.Dumps(diagnostics.Select(Fields).ToList(), pythonReprNonFinite: true);
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

    private static PyObject Fields(Diagnostic diagnostic)
    {
        PyObject fields = new()
        {
            { "rule_id", diagnostic.RuleId },
            { "severity", diagnostic.Severity },
            { "path", diagnostic.Path },
            { "line", diagnostic.Line },
            { "column", diagnostic.Column },
            { "message", diagnostic.Message },
        };
        if (diagnostic.Fix.Length > 0)
        {
            fields.Add("fix", diagnostic.Fix);
        }

        if (diagnostic.Expected is not null)
        {
            fields.Add("expected", diagnostic.Expected);
        }

        if (diagnostic.Actual is not null)
        {
            fields.Add("actual", diagnostic.Actual);
        }

        return fields;
    }
}
