using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Totals.Phases;

/// <summary>
/// ExecuteSelfBuffBucketPhase: the buff pass of <c>CalculateAndApplyEffects</c>, which routes each eEffectType's
/// contributing effects, read as enhanced BuffedMag, into <c>_selfBuffs</c>.
/// </summary>
internal static class SelfBuffBucketPhase
{
    // The eStatType MaxXSpeed bucket and the eEffectType speed it sums from.
    private static readonly Dictionary<string, string> MaxSpeedSources = new(StringComparer.Ordinal)
    {
        ["MaxRunSpeed"] = "SpeedRunning",
        ["MaxJumpSpeed"] = "SpeedJumping",
        ["MaxFlySpeed"] = "SpeedFlying",
    };

    /// <summary>The buff pass for one power; GlobalBoost powers are skipped, as the C# caller's contract has it.</summary>
    /// <param name="power">The power.</param>
    /// <param name="buffs">The <c>_selfBuffs</c> accumulator.</param>
    /// <param name="context">The enhanced-magnitude context.</param>
    /// <param name="prevHpMax">The previous recompute's HPMax, which the Absorb normalization reads.</param>
    /// <param name="globalAccSource">Whether the power is IsGlobalAccuracySource.</param>
    public static void Apply(Power power, BuffsX buffs, MagContext context, double prevHpMax, bool globalAccSource)
    {
        if (string.Equals(power.PowerType, "GlobalBoost", StringComparison.Ordinal))
        {
            return;
        }

        IReadOnlyDictionary<int, string> effectTypeNames = context.Enums.Inverse["eEffectType"];
        BuffMaps maps = BuffMaps.From(context.Enums);
        for (int effIndex = 0; effIndex < buffs.Effect.Length; effIndex++)
        {
            string effectType = effectTypeNames[effIndex];
            if (string.Equals(effectType, "Damage", StringComparison.Ordinal))
            {
                continue;
            }

            List<(Effect Effect, double Mag)> pairs;
            if (MaxSpeedSources.TryGetValue(effectType, out string? source))
            {
                pairs = context.EffectMagSum(power, source, maxMode: true);
                double selfSum = SumMags(context.EffectMagSum(power, effectType, onlySelf: true));
                buffs.Effect[maps.Stat[effectType]] = F32.Round(buffs.Effect[maps.Stat[effectType]] + selfSum);
            }
            else
            {
                pairs = context.EffectMagSum(power, effectType);
            }

            foreach ((Effect effect, double value) in pairs)
            {
                if (effect.ToWho is "Self" or "All")
                {
                    Route(new Routed(effect, value, effectType, effIndex), buffs, context, power, prevHpMax, maps, globalAccSource);
                }
            }
        }
    }

    /// <summary>
    /// Routes one contributing effect. Order is load-bearing: the boost and status buckets accumulate unconditionally,
    /// then the first matching typed or scalar router consumes the effect, else the generic fallback runs.
    /// </summary>
    private static void Route(Routed routed, BuffsX buffs, MagContext context, Power power, double prevHpMax, BuffMaps maps, bool globalAccSource)
    {
        Effect effect = routed.Effect;
        maps.ApplyEnhancementBoosts(effect, routed.Value, buffs);
        if (string.Equals(effect.AbsorbedPowerType, "GlobalBoost", StringComparison.Ordinal))
        {
            return;
        }

        ApplyStatusBuckets(effect, routed.Value, buffs, maps);
        if (RouteHealEndurance(effect, routed.Value, buffs)
            || RouteMezEtmod(effect, routed.Value, buffs, maps)
            || RouteTypedVectors(routed, buffs, maps)
            || RouteScalarAndSpeed(routed, buffs, maps, globalAccSource))
        {
            return;
        }

        ApplyEffectFallback(routed, buffs, context, power, prevHpMax, maps);
    }

    private static void ApplyStatusBuckets(Effect effect, double value, BuffsX buffs, BuffMaps maps)
    {
        switch (effect.EffectType)
        {
            case "Mez":
                BuffsX.Add(buffs.StatusProtection, maps.EMez[effect.MezType], value);
                break;
            case "MezResist":
                BuffsX.Add(buffs.StatusResistance, maps.EMez[effect.MezType], value);
                break;
            case "ResEffect":
                BuffsX.Add(buffs.DebuffResistance, maps.EType[effect.EtModifies], value);
                break;
            default:
                break;
        }
    }

    /// <summary>The Heal skip and MaxEnd; true when the effect is consumed.</summary>
    private static bool RouteHealEndurance(Effect effect, double value, BuffsX buffs)
    {
        if (string.Equals(effect.EffectType, "Heal", StringComparison.Ordinal) || (string.Equals(effect.EffectType, "Enhancement", StringComparison.Ordinal) && string.Equals(effect.EtModifies, "Heal", StringComparison.Ordinal)))
        {
            return true;
        }

        if (string.Equals(effect.EffectType, "Endurance", StringComparison.Ordinal) && string.Equals(effect.Aspect, "Max", StringComparison.Ordinal))
        {
            buffs.MaxEnd = F32.Round(buffs.MaxEnd + value);
            return true;
        }

        return false;
    }

    /// <summary>Mez and MezResist expressed as ETModifies; true when consumed.</summary>
    private static bool RouteMezEtmod(Effect effect, double value, BuffsX buffs, BuffMaps maps)
    {
        if (string.Equals(effect.EffectType, "ResEffect", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(effect.EtModifies, "Mez", StringComparison.Ordinal))
        {
            BuffsX.Add(buffs.Mez, maps.EMez[effect.MezType], value);
            return true;
        }

        if (string.Equals(effect.EtModifies, "MezResist", StringComparison.Ordinal))
        {
            BuffsX.Add(buffs.MezRes, maps.EMez[effect.MezType], value);
            return true;
        }

        return false;
    }

    /// <summary>The typed Defense, Resistance and Elusivity vectors; true when consumed.</summary>
    private static bool RouteTypedVectors(Routed routed, BuffsX buffs, BuffMaps maps)
    {
        if (string.Equals(routed.Effect.DamageType, "None", StringComparison.Ordinal))
        {
            return false;
        }

        double[]? bucket = routed.EffectType switch
        {
            "Defense" => buffs.Defense,
            "Resistance" => buffs.Resistance,
            "Elusivity" => buffs.Elusivity,
            _ => null,
        };
        if (bucket is null)
        {
            return false;
        }

        BuffsX.Add(bucket, maps.EDamage[routed.Effect.DamageType], routed.Value);
        return true;
    }

    /// <summary>Accuracy as ToHit, the MaxSpeed caps and ToHit; true when consumed.</summary>
    private static bool RouteScalarAndSpeed(Routed routed, BuffsX buffs, BuffMaps maps, bool globalAccSource)
    {
        Effect effect = routed.Effect;
        if (!string.Equals(effect.EffectType, "ResEffect", StringComparison.Ordinal) && string.Equals(effect.EtModifies, "Accuracy", StringComparison.Ordinal))
        {
            // A set-bonus or GlobalBoost accuracy buff is BuffAcc; every ordinary power's acts as ToHit.
            BuffsX.Add(buffs.Effect, maps.Stat[globalAccSource ? "BuffAcc" : "ToHit"], routed.Value);
            return true;
        }

        foreach ((string speed, string stat) in new[] { ("SpeedRunning", "MaxRunSpeed"), ("SpeedFlying", "MaxFlySpeed"), ("SpeedJumping", "MaxJumpSpeed") })
        {
            if (IsMaxSpeedCap(effect, speed))
            {
                BuffsX.Add(buffs.Effect, maps.Stat[stat], routed.Value);
                return true;
            }
        }

        if (string.Equals(routed.EffectType, "ToHit", StringComparison.Ordinal))
        {
            if (!(effect.IsEnhancementEffect && string.Equals(effect.EffectClass, "Tertiary", StringComparison.Ordinal)))
            {
                BuffsX.Add(buffs.Effect, routed.EffIndex, routed.Value);
            }

            return true;
        }

        return false;
    }

    /// <summary>The generic <c>Effect[effIndex]</c> add, with the Absorb percent-of-HP normalization and the click mirror.</summary>
    private static void ApplyEffectFallback(Routed routed, BuffsX buffs, MagContext context, Power power, double prevHpMax, BuffMaps maps)
    {
        double value = routed.Value;
        if (routed.EffIndex == maps.Stat["Absorb"] && routed.Effect.DisplayPercentage)
        {
            value = F32.Round(value * prevHpMax);
        }

        BuffsX.Add(buffs.Effect, routed.EffIndex, value);

        // C# also guards on the effect string not reading "From Enh", which only an enhancement-granted effect can.
        if (string.Equals(power.PowerType, "Click", StringComparison.Ordinal) && !power.ClickBuff)
        {
            buffs.Effect[routed.EffIndex] = F32.Round(buffs.Effect[routed.EffIndex] - context.Mag(routed.Effect, power));
        }
    }

    private static bool IsMaxSpeedCap(Effect effect, string modifies)
    {
        return !string.Equals(effect.EffectType, "ResEffect", StringComparison.Ordinal) && string.Equals(effect.EtModifies, modifies, StringComparison.Ordinal) && string.Equals(effect.Aspect, "Max", StringComparison.Ordinal);
    }

    private static double SumMags(List<(Effect Effect, double Mag)> pairs)
    {
        double total = 0.0;
        foreach ((_, double mag) in pairs)
        {
            total = F32.Round(total + mag);
        }

        return total;
    }

    /// <summary>One contributing effect, its magnitude, and the eEffectType bucket it was gathered for.</summary>
    private sealed record Routed(Effect Effect, double Value, string EffectType, int EffIndex);
}
