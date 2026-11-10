using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>One source's contribution: DPS, cast-time share, endurance drain, and a per-item breakdown.</summary>
public sealed record SourceResult
{
    /// <summary>Gets the source name.</summary>
    public required string Source { get; init; }

    /// <summary>Gets the DPS.</summary>
    public required double Dps { get; init; }

    /// <summary>Gets the timeline share.</summary>
    public double TimelineShare { get; init; }

    /// <summary>Gets the endurance per second.</summary>
    public double EndPerSec { get; init; }

    /// <summary>Gets the per-item breakdown.</summary>
    public IReadOnlyList<(string Name, double Dps)> Detail { get; init; } = [];

    /// <summary>Gets the label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets the source's own damage baseline, or null for the context's.</summary>
    public double? DamageBuffBaseline { get; init; }
}
