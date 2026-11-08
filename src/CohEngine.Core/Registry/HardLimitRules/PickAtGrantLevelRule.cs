using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>
/// H-POWER-003: each pick sits on the schedule level of its own position among the 24. Mids places a pick by its
/// position, not its level, so a level that belongs to another position still loads the power into the wrong pick.
/// </summary>
internal static class PickAtGrantLevelRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        IReadOnlyList<int> order = context.Schedule.PowerPickLevels;

        // A pick past the 24th is H-POWER-001's.
        foreach (Power power in context.Picks.Where(pick => pick.BuildIndex < order.Count && pick.PickLevel != order[pick.BuildIndex]))
        {
            int expected = order[power.BuildIndex] + 1;
            string message = $"{PyText.Repr(power.FullName)} is pick {power.BuildIndex + 1} of {order.Count}, which the schedule "
                + $"grants at level {expected}, but it is picked at level {power.PickLevel + 1}";
            yield return new Diagnostic("H-POWER-003", "error", message)
            {
                Fix = $"move this power to the pick the schedule grants at level {power.PickLevel + 1}, or pick it at level {expected}",
                Expected = expected,
                Actual = power.PickLevel + 1,
            };
        }
    }
}
