using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>H-FMT-002: each IoLevel lies within its enhancement's bounds; MidsReborn clamps it on load otherwise.</summary>
internal static class MbdIoLevelsRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        if (context.Format is not { } format)
        {
            yield break;
        }

        foreach (Power power in context.Powers)
        {
            foreach (SlotRef slot in context.Slots.GetValueOrDefault(power.BuildIndex, []))
            {
                if (format.IoBoundsByNid.TryGetValue(slot.Enh, out (int Min, int Max) bounds) && (slot.IoLevel < bounds.Min || slot.IoLevel > bounds.Max))
                {
                    string message = $"a slot in {PyText.Repr(power.FullName)} at level {slot.Level + 1} has IoLevel {slot.IoLevel} for "
                        + $"enhancement nID {slot.Enh}, outside {bounds.Min} to {bounds.Max}";
                    yield return new Diagnostic("H-FMT-002", "error", message)
                    {
                        Fix = $"write the IoLevel as at most {bounds.Max}",
                        Expected = bounds.Max,
                        Actual = slot.IoLevel,
                    };
                }
            }
        }
    }
}
