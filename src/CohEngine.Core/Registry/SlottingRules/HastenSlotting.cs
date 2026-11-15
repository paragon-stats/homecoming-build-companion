using System.Globalization;

using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.SlottingRules;

/// <summary>P-POWER-002: Hasten missing, or holding fewer than two Recharge enhancements, on a build that needs to click powers (info).</summary>
internal static class HastenSlotting
{
    private const string HastenPower = "Pool.Speed.Hasten";
    private const string RechargeAspect = "RechargeTime";
    private const int Norm = 2;

    // A Mastermind's pets do the work, so it has no need to click powers constantly.
    private const string MastermindPrefix = "Mastermind_Summon.";

    // Kinetics' own recharge self-buff stands in for Hasten.
    private const string SiphonSpeedSuffix = ".Siphon_Speed";

    /// <summary>Runs the rule.</summary>
    /// <param name="context">The slotting context.</param>
    /// <returns>The finding, when there is one.</returns>
    public static IEnumerable<Diagnostic> Run(SlottingContext context)
    {
        if (context.Powers.Any(power => power.FullName.StartsWith(MastermindPrefix, StringComparison.Ordinal) || power.FullName.EndsWith(SiphonSpeedSuffix, StringComparison.Ordinal)))
        {
            yield break;
        }

        bool picked = context.Powers.Any(power => string.Equals(power.FullName, HastenPower, StringComparison.Ordinal));
        int held = context.Filled.Count(slot => string.Equals(slot.Power.FullName, HastenPower, StringComparison.Ordinal)
            && context.EnhEffects.TryGetValue(slot.Slot.Enh, out Data.EnhancementRecord? record)
            && record.Effects.Any(effect => string.Equals(effect.Enhance, RechargeAspect, StringComparison.Ordinal)));
        if (picked && held >= Norm)
        {
            yield break;
        }

        string message = "the build has no Hasten; most characters take it with two Recharge IOs boosted to +5";
        if (picked)
        {
            string plural = held == 1 ? string.Empty : "s";
            message = string.Create(CultureInfo.InvariantCulture, $"'{HastenPower}' holds {held} Recharge enhancement{plural}; two Recharge IOs boosted to +5 are the norm");
        }

        yield return new Diagnostic("P-POWER-002", "info", message)
        {
            Fix = "take Hasten with two Recharge IOs boosted to +5, or record why this character does not need to click powers constantly",
        };
    }
}
