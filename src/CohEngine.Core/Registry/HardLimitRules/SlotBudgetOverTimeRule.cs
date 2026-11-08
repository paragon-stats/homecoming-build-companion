using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>
/// H-SLOT-005: a slot cannot be placed before it is earned. Slots may be deferred (the surplus carries forward), so
/// the check is cumulative: walking levels in order, the running placed count must never pass the running granted
/// count. It reports the first level where the build has placed more than it has earned.
/// </summary>
internal static class SlotBudgetOverTimeRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        int granted = 0;
        int placed = 0;
        foreach (int level in context.Schedule.SlotGrantLevels.Union(context.AddedByLevel.Keys).Order())
        {
            granted += context.Schedule.SlotsPerLevel.GetValueOrDefault(level);
            placed += context.AddedByLevel.GetValueOrDefault(level);
            if (placed > granted)
            {
                string message = $"by level {level + 1}, {placed} added slots are placed but only "
                    + $"{granted} have been granted (a slot cannot be placed before it is earned)";
                yield return new Diagnostic("H-SLOT-005", "error", message)
                {
                    Fix = "move the excess slot(s) to a later grant level, or remove them",
                    Expected = granted,
                    Actual = placed,
                };
                yield break;
            }
        }
    }
}
