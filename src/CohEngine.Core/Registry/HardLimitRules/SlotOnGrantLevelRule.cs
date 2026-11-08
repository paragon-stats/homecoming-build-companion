using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>H-SLOT-004: an added slot must land on a real slot-grant level in the schedule.</summary>
internal static class SlotOnGrantLevelRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        IReadOnlySet<int> grantLevels = context.Schedule.SlotGrantLevels;
        foreach ((Data.Power power, Data.SlotRef slot) in context.AddedPairs.Where(pair => !grantLevels.Contains(pair.Slot.Level)))
        {
            string message = $"a slot in {PyText.Repr(power.FullName)} is placed at level {slot.Level + 1}, which grants no "
                + "enhancement slot in the level schedule";
            yield return new Diagnostic("H-SLOT-004", "error", message)
            {
                Fix = "move the slot to a level that grants a slot",
                Actual = slot.Level + 1,
            };
        }
    }
}
