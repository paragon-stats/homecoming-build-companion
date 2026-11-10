namespace CohEngine.Core.BuildFile;

/// <summary>One <c>SubPowerEntries</c> object of a <c>.mbd</c> file.</summary>
public sealed record MbdSubPowerDoc
{
    /// <summary>Gets the sub-power's full name.</summary>
    public string? PowerName { get; set; }

    /// <summary>Gets a value indicating whether it counts toward totals.</summary>
    public bool StatInclude { get; set; }
}
