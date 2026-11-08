using CohEngine.Core.Data;
using CohEngine.Core.Enhancement;
using CohEngine.Core.Maths;

using EnhancementPipeline = CohEngine.Core.Enhancement.Pipeline;

namespace CohEngine.Core.Totals;

/// <summary>
/// The buffed per-power scalars (recharge, end cost, interrupt) and the endurance rate: each base scalar over its
/// Pass 3/4 fold, recharge clamped to the AT recharge cap, and an ignored aspect left at base.
/// </summary>
internal static class PowerStatistics
{
    /// <summary>The derived scalars and endurance per second for every build power, in build order.</summary>
    /// <param name="powers">The build's powers.</param>
    /// <param name="slots">The build's slots.</param>
    /// <param name="context">The shared inputs.</param>
    /// <returns>The statistics.</returns>
    public static List<PowerStats> ComputeBuildStats(IReadOnlyList<Power> powers, IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots, StatsContext context)
    {
        return [.. powers.Select(power => ComputePowerStats(power, slots.GetValueOrDefault(power.BuildIndex, []), context))];
    }

    /// <summary>One power's derived scalars and endurance per second.</summary>
    /// <param name="power">The power.</param>
    /// <param name="powerSlots">Its slots.</param>
    /// <param name="context">The shared inputs.</param>
    /// <returns>The statistics.</returns>
    public static PowerStats ComputePowerStats(Power power, IReadOnlyList<SlotRef> powerSlots, StatsContext context)
    {
        double recharge = power.RechargeTime <= 0.0 ? 0.0 : F32.Round(power.RechargeTime / ScalarFold(power, powerSlots, AspectRouting.Recharge, context));
        double endCost = F32.Round(power.EndCost / ScalarFold(power, powerSlots, AspectRouting.End, context));
        double interrupt = F32.Round(power.InterruptTime / ScalarFold(power, powerSlots, AspectRouting.Interrupt, context));
        double endPerSec;
        if (string.Equals(power.PowerType, "Toggle", StringComparison.Ordinal) && power.ActivatePeriod > 0.0)
        {
            endPerSec = F32.Round(endCost / power.ActivatePeriod);
        }
        else
        {
            double cadence = F32.Round(recharge + F32.Round(power.CastTime + interrupt));
            endPerSec = cadence > 0.0 ? F32.Round(endCost / cadence) : 0.0;
        }

        bool isAttack = string.Equals(power.PowerType, "Click", StringComparison.Ordinal) && power.Effects.Any(effect => string.Equals(effect.EffectType, "Damage", StringComparison.Ordinal));
        return new PowerStats(power.FullName, recharge, endCost, interrupt, endPerSec, power.CastTime, isAttack);
    }

    /// <summary>The fold divisor for one aspect, the incarnate pre-ED term co-ED'd with the slots and the post-ED term after.</summary>
    private static double ScalarFold(Power power, IReadOnlyList<SlotRef> powerSlots, AspectHandler handler, StatsContext context)
    {
        (double preEd, double postEd) = context.IncarnateScalar.GetValueOrDefault((power.BuildIndex, handler.Aspect), (0.0, 0.0));
        double aggregate = EnhancementPipeline.AggregateAndEd(powerSlots, handler.Aspect, context.EnhDb, context.Config.ForceLevel, context.Tables, preEdAddend: preEd);
        return AspectRouting.FoldScalar(handler, aggregate, AspectRouting.GlobalTerm(handler, context.GlobalEnhance), power.IgnoreEnh, context.RechargeCap, postEd);
    }

    /// <summary>The inputs every per-power derived statistic reads.</summary>
    internal sealed record StatsContext
    {
        /// <summary>Gets the engine configuration.</summary>
        public required EngineConfig Config { get; init; }

        /// <summary>Gets the Maths.mhd tables.</summary>
        public required MathTables Tables { get; init; }

        /// <summary>Gets the enhancement records.</summary>
        public required IReadOnlyDictionary<int, EnhancementRecord> EnhDb { get; init; }

        /// <summary>Gets the build's global terms.</summary>
        public required GlobalEnhance GlobalEnhance { get; init; }

        /// <summary>Gets the AT recharge cap.</summary>
        public required double RechargeCap { get; init; }

        /// <summary>Gets the incarnate scalar addends.</summary>
        public IReadOnlyDictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)> IncarnateScalar { get; init; } = new Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)>();
    }

    /// <summary>One power's derived scalars; cast time is not recharge-reduced, and an attack is a click that deals damage.</summary>
    /// <param name="FullName">The power's full name.</param>
    /// <param name="RechargeTime">The buffed recharge.</param>
    /// <param name="EndCost">The buffed end cost.</param>
    /// <param name="InterruptTime">The buffed interrupt time.</param>
    /// <param name="EndPerSec">The endurance per second.</param>
    /// <param name="CastTime">The cast time.</param>
    /// <param name="IsAttack">Whether the power is an attack.</param>
    internal sealed record PowerStats(string FullName, double RechargeTime, double EndCost, double InterruptTime, double EndPerSec, double CastTime, bool IsAttack);
}
