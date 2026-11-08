namespace CohEngine.Core.Data;

/// <summary>A resolved enhancement slot (the Python port's <c>enhancement.SlotRef</c>).</summary>
public sealed record SlotRef
{
    /// <summary>Gets the 0-based slot placement level.</summary>
    public required int Level { get; init; }

    /// <summary>Gets a value indicating whether this is the power's inherent slot.</summary>
    public required bool IsInherent { get; init; }

    /// <summary>Gets the enhancement index, or -1 for an empty slot.</summary>
    public required int Enh { get; init; }

    /// <summary>Gets the eEnhGrade ordinal.</summary>
    public required int Grade { get; init; }

    /// <summary>Gets the 0-based IO level.</summary>
    public required int IoLevel { get; init; }

    /// <summary>Gets the eEnhRelative ordinal.</summary>
    public required int RelativeLevel { get; init; }
}
