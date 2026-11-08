namespace CohEngine.Core.Data;

/// <summary>A build's <c>totals.json</c>: Mids' uncapped and capped totals.</summary>
public sealed record MidsTotals
{
    /// <summary>Gets the class name.</summary>
    public required string Class { get; init; }

    /// <summary>Gets the character level.</summary>
    public required int Level { get; init; }

    /// <summary>Gets the uncapped totals.</summary>
    public required TotalStatistics Totals { get; init; }

    /// <summary>Gets the AT- and server-capped totals.</summary>
    public required TotalStatistics TotalsCapped { get; init; }
}
