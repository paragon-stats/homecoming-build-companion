using CohEngine.Core.Data;

namespace CohEngine.Core.Registry;

/// <summary>The inputs every hard-limit rule reads, with the build walked once.</summary>
/// <param name="Powers">Every build power.</param>
/// <param name="Slots">The build's slots by build index.</param>
/// <param name="Schedule">The level schedule.</param>
/// <param name="MaxSlots">The added-slot cap from the server dump.</param>
/// <param name="Picks">The real picked powers in build order.</param>
/// <param name="AddedByPower">Added-slot counts by build index; powers without an added slot are absent.</param>
/// <param name="AddedByLevel">Added-slot counts by placement level index.</param>
/// <param name="AddedPairs">Every power and added slot pair.</param>
/// <param name="Format">The <c>.mbd</c> file facts, or null for a dump directory.</param>
internal sealed record HardLimitsContext(
    IReadOnlyList<Power> Powers,
    IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> Slots,
    LevelSchedule Schedule,
    int MaxSlots,
    IReadOnlyList<Power> Picks,
    IReadOnlyDictionary<int, int> AddedByPower,
    IReadOnlyDictionary<int, int> AddedByLevel,
    IReadOnlyList<(Power Power, SlotRef Slot)> AddedPairs,
    MbdFormat? Format);
