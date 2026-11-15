using System.Globalization;

using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.SlottingRules;

/// <summary>P-POWER-001: a movement-and-recharge auto power picked after the level it opens at (a warning).</summary>
internal static class LateQualityOfLifePick
{
    // The +5 rule: an exemplared character keeps powers up to five levels above the mission level.
    private const int ExemplarReach = 5;
    private const int ExemplarGap = 6;

    // Flight Speed, Jumping and Run Speed in enhancement_classes.json; a .mbd power carries classes, not effects.
    private static readonly int[] MovementClasses = [10, 15, 20];

    /// <summary>Runs the rule.</summary>
    /// <param name="context">The slotting context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(SlottingContext context)
    {
        foreach (Power power in context.Powers.Where(power => string.Equals(power.PowerType, "Auto_", StringComparison.Ordinal) && power.Level > 1 && power.PickLevel >= 0 && power.PickLevel + 1 > power.Level && power.Enhancements.Any(MovementClasses.Contains)))
        {
            int pick = power.PickLevel + 1;
            int first = Math.Max(1, power.Level - ExemplarReach);
            string clause = pick - ExemplarGap >= first
                ? string.Create(CultureInfo.InvariantCulture, $"; exemplared to levels {first} to {pick - ExemplarGap} the character lacks it")
                : string.Empty;
            yield return new Diagnostic("P-POWER-001", "warning", string.Create(CultureInfo.InvariantCulture, $"'{power.FullName}' opens at level {power.Level} but is picked at level {pick}{clause}"))
            {
                Fix = string.Create(CultureInfo.InvariantCulture, $"pick this power at level {power.Level}, or record the later pick as a trade-off"),
                Expected = power.Level,
                Actual = pick,
            };
        }
    }
}
