namespace CohEngine.Core.Data;

/// <summary>One <c>slots.json</c> entry: a build power's slots and, in newer dumps, its ProcInclude flag.</summary>
public sealed record BuildSlots
{
    /// <summary>Gets the power's index in the build.</summary>
    public required int BuildIndex { get; init; }

    /// <summary>Gets the power's slots in order.</summary>
    public required IReadOnlyList<SlotRef> Slots { get; init; }

    /// <summary>Gets the ProcInclude flag; null in dumps predating it.</summary>
    public bool? ProcInclude { get; set; }
}
