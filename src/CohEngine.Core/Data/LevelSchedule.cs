namespace CohEngine.Core.Data;

/// <summary>
/// The game's slot-grant and power-pick levels as 0-based level indices (the Python port's <c>levels.LevelSchedule</c>).
/// </summary>
/// <param name="SlotsPerLevel">Slots granted per slot-grant level index.</param>
/// <param name="PowerPickLevels">The 24-pick schedule in order; the n-th entry is the level index of the n-th pick.</param>
public sealed record LevelSchedule(IReadOnlyDictionary<int, int> SlotsPerLevel, IReadOnlyList<int> PowerPickLevels)
{
    /// <summary>Gets the level indices that grant at least one slot.</summary>
    public IReadOnlySet<int> SlotGrantLevels => SlotsPerLevel.Keys.ToHashSet();

    /// <summary>Gets the total slots the schedule grants (67 on Homecoming).</summary>
    public int TotalSlotsGranted => SlotsPerLevel.Values.Sum();
}
