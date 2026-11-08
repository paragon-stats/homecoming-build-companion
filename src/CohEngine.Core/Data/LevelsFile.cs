namespace CohEngine.Core.Data;

/// <summary>The <c>levels.json</c> fields the schedule reads.</summary>
public sealed record LevelsFile
{
    /// <summary>Gets the slot-grant levels.</summary>
    public required IReadOnlyList<LevelEntry> SlotGrantLevels { get; init; }

    /// <summary>Gets the power-pick levels in pick order.</summary>
    public required IReadOnlyList<LevelEntry> PowerPickLevels { get; init; }
}
