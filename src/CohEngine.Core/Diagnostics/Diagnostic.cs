namespace CohEngine.Core.Diagnostics;

/// <summary>
/// One finding, rendered per <c>error-output.md</c>. <see cref="Expected"/> and <see cref="Actual"/> are optional
/// structured context (bool, int, long, double, string, or a list of int or string): the JSON form carries them only
/// when set. Location defaults to the whole-build anchor.
/// </summary>
public sealed record Diagnostic
{
    private static readonly string[] Severities = ["error", "warning", "info"];

    /// <summary>Initializes a new instance of the <see cref="Diagnostic"/> class.</summary>
    /// <param name="ruleId">The stable rule id.</param>
    /// <param name="severity">One of error, warning, info.</param>
    /// <param name="message">The concrete message.</param>
    /// <exception cref="EngineException">E10: the severity is not one of the three.</exception>
    public Diagnostic(string ruleId, string severity, string message)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        ArgumentNullException.ThrowIfNull(severity);
        ArgumentNullException.ThrowIfNull(message);
        if (!Severities.Contains(severity, StringComparer.Ordinal))
        {
            throw new EngineException(
                $"E10: diagnostic severity {PyText.Repr(severity)} is not one of {PyText.List(Severities)}; use 'error', 'warning', or 'info'");
        }

        RuleId = ruleId;
        Severity = severity;
        Message = message;
    }

    /// <summary>Gets the rule id.</summary>
    public string RuleId { get; }

    /// <summary>Gets the severity.</summary>
    public string Severity { get; }

    /// <summary>Gets the message.</summary>
    public string Message { get; }

    /// <summary>Gets the file the finding is in.</summary>
    public string Path { get; init; } = "build.json";

    /// <summary>Gets the 1-based line.</summary>
    public int Line { get; init; } = 1;

    /// <summary>Gets the 1-based column.</summary>
    public int Column { get; init; } = 1;

    /// <summary>Gets the suggested fix, or empty.</summary>
    public string Fix { get; init; } = string.Empty;

    /// <summary>Gets the expected value, or null.</summary>
    public object? Expected { get; init; }

    /// <summary>Gets the actual value, or null.</summary>
    public object? Actual { get; init; }

    /// <summary>Gets the machine-parseable line <c>{path}:{line}:{column}: {severity}: {rule_id}: {message}</c>.</summary>
    /// <returns>The line.</returns>
    public string FormatText()
    {
        return $"{Path}:{Line}:{Column}: {Severity}: {RuleId}: {Message}";
    }
}
