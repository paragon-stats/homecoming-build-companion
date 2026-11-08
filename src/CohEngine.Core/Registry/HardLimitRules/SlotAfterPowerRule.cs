using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>H-SLOT-003: an added slot cannot be placed before its power is available (its pick level).</summary>
internal static class SlotAfterPowerRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        foreach ((Data.Power power, Data.SlotRef slot) in context.AddedPairs.Where(pair => pair.Slot.Level < pair.Power.PickLevel))
        {
            string message = $"a slot in {PyText.Repr(power.FullName)} is placed at level {slot.Level + 1}, before the "
                + $"power was picked (level {power.PickLevel + 1})";
            yield return new Diagnostic("H-SLOT-003", "error", message)
            {
                Fix = "place the slot at or after the power's pick level",
                Expected = power.PickLevel + 1,
                Actual = slot.Level + 1,
            };
        }
    }
}
