using System.Globalization;

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
            foreach (SlotRef slot in context.Slots.GetValueOrDefault(power.BuildIndex, []).Where(slot => format.IoBoundsByNid.TryGetValue(slot.Enh, out (int Min, int Max) range) && (slot.IoLevel < range.Min || slot.IoLevel > range.Max)))
            {
                (int min, int max) = format.IoBoundsByNid[slot.Enh];
                string message = string.Create(CultureInfo.InvariantCulture, $"a slot in {PyText.Repr(power.FullName)} at level {slot.Level + 1} has IoLevel {slot.IoLevel} for ")
                    + string.Create(CultureInfo.InvariantCulture, $"enhancement nID {slot.Enh}, outside {min} to {max}");
                yield return new Diagnostic("H-FMT-002", "error", message)
                {
                    Fix = string.Create(CultureInfo.InvariantCulture, $"write the IoLevel as at most {max}"),
                    Expected = max,
                    Actual = slot.IoLevel,
                };
            }
        }
    }
}
