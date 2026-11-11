using CohEngine.Core.Data;
using CohEngine.Core.Enhancement;
using CohEngine.Core.Maths;
using CohEngine.Core.SetBonuses;

using EnhancementPipeline = CohEngine.Core.Enhancement.Pipeline;

namespace CohEngine.Core.Totals.Phases;

/// <summary>
/// ExecutePerPowerEnhancementMathPhase: GBPA_Pass1 to Pass4 per effect, giving each effect the multiplier
/// <c>1 + ED(sum slotted + incarnate pre-ED) + global + incarnate post-ED</c> its BuffedMag is read with.
/// </summary>
internal static class PerPowerEnhancementMathPhase
{
    private static readonly HashSet<string> Pass3ScalarTypes = new(["Accuracy", "EnduranceDiscount", "InterruptTime", "Range", "RechargeTime"], StringComparer.Ordinal);
    private static readonly HashSet<string> Pass3SpeedTypes = new(["SpeedRunning", "SpeedJumping", "JumpHeight", "SpeedFlying"], StringComparer.Ordinal);

    /// <summary>The eEnhance names, None excluded: the Pass1 name-identity route set.</summary>
    /// <param name="enums">The enum maps.</param>
    /// <returns>The names.</returns>
    public static HashSet<string> EnhanceNames(EnumMaps enums)
    {
        HashSet<string> names = new(enums.Maps["eEnhance"].Keys, StringComparer.Ordinal);
        names.Remove("None");
        return names;
    }

    /// <summary>
    /// The eEnhance aspect whose slotted value enhances a Buffable effect's magnitude: the one named as its EffectType,
    /// plus Enhancement with ETModifies Accuracy, ResEffect with ETModifies Defense, and the allowed-list-only Heal case.
    /// Special damage and duration-typed Mez gain no magnitude. Null leaves the magnitude unenhanced.
    /// </summary>
    /// <param name="effect">The effect.</param>
    /// <param name="enhanceNames">The eEnhance names.</param>
    /// <returns>The aspect, or null.</returns>
    public static string? EnhanceAspect(Effect effect, IReadOnlySet<string> enhanceNames)
    {
        if (!effect.Buffable)
        {
            return null;
        }

        if (string.Equals(effect.EffectType, "Damage", StringComparison.Ordinal) && string.Equals(effect.DamageType, "Special", StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(effect.EffectType, "Mez", StringComparison.Ordinal) && string.Equals(effect.AttribType, "Duration", StringComparison.Ordinal))
        {
            return null;
        }

        if (enhanceNames.Contains(effect.EffectType))
        {
            return effect.EffectType;
        }

        if (string.Equals(effect.EffectType, "Enhancement", StringComparison.Ordinal) && string.Equals(effect.EtModifies, "Accuracy", StringComparison.Ordinal))
        {
            return "Accuracy";
        }

        if (string.Equals(effect.EffectType, "ResEffect", StringComparison.Ordinal) && string.Equals(effect.EtModifies, "Defense", StringComparison.Ordinal))
        {
            return "Defense";
        }

        if (IsAllowedListOnly(effect))
        {
            return "Heal";
        }

        return null;
    }

    /// <summary>
    /// The per-effect multipliers of every build power; only non-unit multipliers are stored. The context's EnhMult is
    /// empty while this runs, so the sign gate reads unenhanced magnitudes, and the set-bonus virtual power is never
    /// enhanced.
    /// </summary>
    /// <param name="included">The included powers.</param>
    /// <param name="slots">The build's slots.</param>
    /// <param name="enhDb">The enhancement records.</param>
    /// <param name="tables">The Maths.mhd tables.</param>
    /// <param name="forceLevel">The exemplar cutoff.</param>
    /// <param name="context">The base-magnitude context.</param>
    /// <param name="incarnateEffect">The incarnate effect addends.</param>
    /// <param name="selfEnhance">The <c>_selfEnhance</c> accumulator, or null.</param>
    /// <returns>The multipliers by (build index, effect index).</returns>
    public static Dictionary<(int BuildIndex, int EffectIndex), double> ComputeEnhMultipliers(
        IReadOnlyList<Power> included,
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots,
        IReadOnlyDictionary<int, EnhancementRecord> enhDb,
        MathTables tables,
        int forceLevel,
        MagContext context,
        IReadOnlyDictionary<(int BuildIndex, int EffectIndex), (double PreEd, double PostEd)>? incarnateEffect = null,
        BuffsX? selfEnhance = null)
    {
        Dictionary<(int BuildIndex, int EffectIndex), double> output = [];
        foreach (Power power in included)
        {
            if (power.BuildIndex == Accumulator.SetBonusBuildIndex)
            {
                continue;
            }

            IReadOnlyList<SlotRef> powerSlots = slots.GetValueOrDefault(power.BuildIndex, []);
            Dictionary<MultiplierKey, double> memo = [];
            foreach (Effect effect in power.Effects)
            {
                (double PreEd, double PostEd)? inc = incarnateEffect is not null && incarnateEffect.TryGetValue((power.BuildIndex, effect.Index), out (double PreEd, double PostEd) found) ? found : null;
                double? mult = EffectMultiplier(power, effect, powerSlots, memo, inc, new Inputs(enhDb, tables, forceLevel, context, selfEnhance));
                if (mult is { } value)
                {
                    output[(power.BuildIndex, effect.Index)] = value;
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Each toggle's endurance-discount aggregate <c>ED(sum EndRdx + incarnate pre-ED)</c>, which EndUse needs apart from
    /// the effect multipliers since EnduranceDiscount reaches no effect bucket. Only non-zero aggregates are stored.
    /// </summary>
    /// <param name="included">The included powers.</param>
    /// <param name="slots">The build's slots.</param>
    /// <param name="enhDb">The enhancement records.</param>
    /// <param name="tables">The Maths.mhd tables.</param>
    /// <param name="forceLevel">The exemplar cutoff.</param>
    /// <param name="incarnateScalar">The incarnate scalar addends.</param>
    /// <returns>The aggregates by build index.</returns>
    public static Dictionary<int, double> ComputeToggleEndAgg(
        IReadOnlyList<Power> included,
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots,
        IReadOnlyDictionary<int, EnhancementRecord> enhDb,
        MathTables tables,
        int forceLevel,
        IReadOnlyDictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)> incarnateScalar)
    {
        Dictionary<int, double> output = [];
        foreach (Power power in included)
        {
            if (!string.Equals(power.PowerType, "Toggle", StringComparison.Ordinal))
            {
                continue;
            }

            (double preEd, _) = incarnateScalar.GetValueOrDefault((power.BuildIndex, AspectRouting.End.Aspect), (0.0, 0.0));
            double aggregate = EnhancementPipeline.AggregateAndEd(slots.GetValueOrDefault(power.BuildIndex, []), "EnduranceDiscount", enhDb, forceLevel, tables, preEdAddend: preEd);
            if (!Exact.Same(aggregate, 0.0))
            {
                output[power.BuildIndex] = aggregate;
            }
        }

        return output;
    }

    /// <summary>The one effect enhanced only through an enhancement's allowed-effects list, which ED never reaches.</summary>
    private static bool IsAllowedListOnly(Effect effect)
    {
        return string.Equals(effect.EffectType, "ResEffect", StringComparison.Ordinal) && string.Equals(effect.EtModifies, "Regeneration", StringComparison.Ordinal) && string.Equals(effect.MezType, "None", StringComparison.Ordinal);
    }

    /// <summary>The build-wide enhancement buff Pass 3 adds to one effect's enhancement after ED.</summary>
    private static double GlobalEnhancementTerm(Effect effect, double baseMag, BuffsX? selfEnhance, EnumMaps enums)
    {
        if (selfEnhance is null || !effect.Buffable || Pass3ScalarTypes.Contains(effect.EffectType))
        {
            return 0.0;
        }

        IReadOnlyDictionary<string, int> eDamage = enums.Maps["eDamage"];
        switch (effect.EffectType)
        {
            case "Damage":
                return selfEnhance.Damage[eDamage[effect.DamageType]];
            case "Defense":
                return selfEnhance.Defense[eDamage[effect.DamageType]];
            case "Mez":
                return string.Equals(effect.AttribType, "Duration", StringComparison.Ordinal) ? 0.0 : selfEnhance.Mez[enums.Maps["eMez"][effect.MezType]];
            case "Resistance":
                return selfEnhance.Resistance[eDamage[effect.DamageType]];
            default:
                break;
        }

        IReadOnlyDictionary<string, int> eType = enums.Maps["eEffectType"];
        string speed;
        if (string.Equals(effect.EffectType, "Enhancement", StringComparison.Ordinal) && Pass3SpeedTypes.Contains(effect.EtModifies))
        {
            speed = effect.EtModifies;
        }
        else if (Pass3SpeedTypes.Contains(effect.EffectType))
        {
            speed = effect.EffectType;
        }
        else
        {
            return selfEnhance.Effect[eType[effect.EffectType]];
        }

        if (baseMag > 0)
        {
            return selfEnhance.Effect[eType[speed]];
        }

        return baseMag < 0 ? selfEnhance.EffectAux[eType[speed]] : 0.0;
    }

    /// <summary>
    /// One effect's multiplier. Slotted-only and incarnate-touched effects share one accumulator: the incarnate pre-ED
    /// term (0 when absent, bit-identical to none) is co-ED'd with the slotted aggregate, then the global and post-ED terms
    /// are added. The ED schedule keys on the routed aspect, or on the effect's own type when it routes to none, whose
    /// slotted aggregate is then 0. Null for a zero accumulator, which the caller leaves absent.
    /// </summary>
    private static double? EffectMultiplier(Power power, Effect effect, IReadOnlyList<SlotRef> powerSlots, Dictionary<MultiplierKey, double> memo, (double PreEd, double PostEd)? inc, Inputs inputs)
    {
        MagContext context = inputs.Context;
        string? aspect = EnhanceAspect(effect, context.EnhanceNames);
        double baseMag = context.Mag(effect, power);
        double glob = GlobalEnhancementTerm(effect, baseMag, inputs.SelfEnhance, context.Enums);
        if (aspect is null && inc is null && Exact.Same(glob, 0.0))
        {
            return null;
        }

        (double preEd, double postEd) = inc ?? (0.0, 0.0);
        string edAspect = aspect ?? effect.EffectType;
        int subEnh = string.Equals(edAspect, "Mez", StringComparison.Ordinal) ? context.Enums.Maps["eMez"][effect.MezType] : -1;
        bool skipEd = IsAllowedListOnly(effect);
        MultiplierKey key = new(edAspect, subEnh, skipEd, baseMag <= 0.0, baseMag >= 0.0, preEd, glob, postEd);
        if (!memo.TryGetValue(key, out double accumulator))
        {
            double aggregate = EnhancementPipeline.AggregateAndEd(
                aspect is not null ? powerSlots : [],
                edAspect,
                inputs.EnhDb,
                inputs.ForceLevel,
                inputs.Tables,
                subEnh,
                baseMag,
                preEd,
                skipEd);
            accumulator = memo[key] = F32.Round(F32.Round(aggregate + glob) + postEd);
        }

        return !Exact.Same(accumulator, 0.0) ? F32.Round(1.0 + accumulator) : null;
    }

    /// <summary>The memo key: two effects of one power with the same key share one aggregate.</summary>
    private sealed record MultiplierKey(string Aspect, int SubEnh, bool SkipEd, bool NonPositive, bool NonNegative, double PreEd, double Global, double PostEd);

    /// <summary>The inputs every effect of the pass shares.</summary>
    private sealed record Inputs(IReadOnlyDictionary<int, EnhancementRecord> EnhDb, MathTables Tables, int ForceLevel, MagContext Context, BuffsX? SelfEnhance);
}
