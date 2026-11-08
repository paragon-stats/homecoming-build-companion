using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>H-SLOT-002: a power may hold at most 5 added slots (1 inherent + 5 = 6 total).</summary>
internal static class PerPowerAddedSlotsRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        foreach (Power power in context.Powers)
        {
            int count = context.AddedByPower.GetValueOrDefault(power.BuildIndex);
            if (count > PowerGroups.MaxAddedSlotsPerPower)
            {
                string message = $"{PyText.Repr(power.FullName)} has {count} added slots; a power may hold at most "
                    + $"{PowerGroups.MaxAddedSlotsPerPower} (plus its inherent slot)";
                yield return new Diagnostic("H-SLOT-002", "error", message)
                {
                    Fix = $"remove {count - PowerGroups.MaxAddedSlotsPerPower} slot(s) from this power",
                    Expected = PowerGroups.MaxAddedSlotsPerPower,
                    Actual = count,
                };
            }
        }
    }
}
