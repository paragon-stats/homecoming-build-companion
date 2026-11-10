namespace CohEngine.Core.BuildFile;

/// <summary>
/// One power and its enhancements, as character levels (the <c>.mbd</c> stores them so).
/// <see cref="Enhancements"/>[0] fills the free (A) slot at <see cref="Level"/>; the rest are added slots, filled in
/// order as they are placed.
/// </summary>
public sealed record PickSpec
{
    /// <summary>Gets the power's full name.</summary>
    public required string Power { get; init; }

    /// <summary>Gets the character level the power is picked at.</summary>
    public required int Level { get; init; }

    /// <summary>Gets the enhancement UIDs, a null entry being an empty slot; the default is one empty slot.</summary>
    public IReadOnlyList<string?> Enhancements { get; init; } = [null];

    /// <summary>Gets a value indicating whether the power counts toward totals.</summary>
    public bool StatInclude { get; init; }

    /// <summary>Gets a value indicating whether this is an exemplar-10 essential, whose first two added slots come first by level 15.</summary>
    public bool Essential { get; init; }

    /// <summary>Gets the grant levels that pin the added slots, or null to let the writer place them.</summary>
    public IReadOnlyList<int>? SlotLevels { get; init; }
}
