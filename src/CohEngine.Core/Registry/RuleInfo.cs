namespace CohEngine.Core.Registry;

/// <summary>A registered rule's identity: the id the CLI lists and a one-line summary.</summary>
public sealed record RuleInfo
{
    /// <summary>Initializes a new instance of the <see cref="RuleInfo"/> class.</summary>
    /// <param name="ruleId">The rule id, as the Python registry names it.</param>
    /// <param name="summary">The first line of the Python rule's docstring, or what the handler binds.</param>
    public RuleInfo(string ruleId, string summary)
    {
        Id = ruleId;
        Summary = summary;
    }

    /// <summary>Gets the rule id.</summary>
    public string Id { get; }

    /// <summary>Gets the one-line summary.</summary>
    public string Summary { get; }
}
