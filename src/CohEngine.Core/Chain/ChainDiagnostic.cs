using System.Globalization;
using System.Text;

namespace CohEngine.Core.Chain;

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
