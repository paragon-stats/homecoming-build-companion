namespace CohEngine.Core.Data;

/// <summary>One level entry of <c>levels.json</c>.</summary>
public sealed record LevelEntry
{
    /// <summary>Gets the 0-based level index.</summary>
    public required int LevelIndex { get; init; }

    /// <summary>Gets the slots granted at the level (slot-grant entries only).</summary>
    public int Slots { get; init; }
}
