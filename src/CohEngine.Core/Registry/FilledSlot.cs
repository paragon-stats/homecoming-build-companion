using CohEngine.Core.Data;

namespace CohEngine.Core.Registry;

/// <summary>One filled slot: its power, the slot, and the resolved enhancement (null when its index is not in the database).</summary>
/// <param name="Power">The power.</param>
/// <param name="Slot">The slot.</param>
/// <param name="Enhancement">The enhancement, or null.</param>
internal readonly record struct FilledSlot(Power Power, SlotRef Slot, EnhancementLegality? Enhancement);
