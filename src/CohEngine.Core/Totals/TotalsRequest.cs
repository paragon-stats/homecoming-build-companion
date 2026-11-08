using CohEngine.Core.Data;

namespace CohEngine.Core.Totals;

/// <summary>What the totals fold reads: the included powers, both accumulators, and the build's class and server.</summary>
internal sealed record TotalsRequest
{
    /// <summary>Gets the included powers.</summary>
    public required IReadOnlyList<Power> Included { get; init; }

    /// <summary>Gets <c>_selfBuffs</c>.</summary>
    public required BuffsX SelfBuffs { get; init; }

    /// <summary>Gets <c>_selfEnhance</c>.</summary>
    public required BuffsX SelfEnhance { get; init; }

    /// <summary>Gets the build's class.</summary>
    public required Archetype Archetype { get; init; }

    /// <summary>Gets the enhanced-magnitude context.</summary>
    public required MagContext Context { get; init; }

    /// <summary>Gets the server constants.</summary>
    public required ServerData Server { get; init; }

    /// <summary>Gets each toggle's endurance-discount aggregate.</summary>
    public required IReadOnlyDictionary<int, double> ToggleEndAgg { get; init; }

    /// <summary>Gets the incarnate scalar addends.</summary>
    public required IReadOnlyDictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)> IncarnateScalar { get; init; }
}
