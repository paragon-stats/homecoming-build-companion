using CohEngine.Core.Data;

namespace CohEngine.Core.Totals.Phases;

/// <summary>
/// ExecuteEnhancementBucketPhase: the enhancement pass of <c>CalculateAndApplyEffects</c>, which fills
/// <c>_selfEnhance</c> with the build-wide enhancement buffs read as pre-Pass5 base magnitudes.
/// </summary>
internal static class EnhancementBucketPhase
{
    private static readonly HashSet<string> SpeedScalars = new(["SpeedRunning", "SpeedFlying", "SpeedJumping", "JumpHeight"], StringComparer.Ordinal);

    /// <summary>
    /// The enhancement pass for one power. DamageBuff is gathered with the normal effect sum and every other bucket
    /// with <c>GetEnhancementMagSum</c>; <paramref name="context"/> must carry no enhancement multiplier.
    /// </summary>
    /// <param name="power">The power.</param>
    /// <param name="buffs">The <c>_selfEnhance</c> accumulator.</param>
    /// <param name="context">The base-magnitude context.</param>
    public static void Apply(Power power, BuffsX buffs, MagContext context)
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

            List<(Effect Effect, double Mag)> pairs = string.Equals(effectType, "DamageBuff", StringComparison.Ordinal)
                ? context.EffectMagSum(power, effectType)
                : context.EnhancementMagSum(power, effectType);
            foreach ((Effect effect, double value) in pairs)
            {
                if (effect.ToWho is "Self" or "All")
                {
                    RouteEnhanceEffect(effect, value, buffs, effectType, effIndex, maps);
                }
            }
        }
    }

    /// <summary>
    /// One enhancement-pass sub-effect: the boost head accumulates unconditionally, GlobalBoost-absorbed effects stop,
    /// then the main routing consumes the effect.
    /// </summary>
    private static void RouteEnhanceEffect(Effect effect, double value, BuffsX buffs, string effectType, int effIndex, BuffMaps maps)
    {
        maps.ApplyEnhancementBoosts(effect, value, buffs, includeRange: false);
        if (string.Equals(effect.AbsorbedPowerType, "GlobalBoost", StringComparison.Ordinal))
        {
            return;
        }

        RouteEnhanceMain(effect, value, buffs, effectType, effIndex, maps);
    }

    /// <summary>
    /// The main routing for a DamageBuff or Enhancement effect; every branch is terminal. Accuracy and Heal are dropped,
    /// as the buff pass and the boost head fold them, and a debuff-mode speed scalar goes to EffectAux.
    /// </summary>
    private static void RouteEnhanceMain(Effect effect, double value, BuffsX buffs, string effectType, int effIndex, BuffMaps maps)
    {
        string etm = effect.EtModifies;
        if (string.Equals(etm, "Mez", StringComparison.Ordinal))
        {
            BuffsX.Add(buffs.Mez, maps.EMez[effect.MezType], value);
            return;
        }

        if (etm is "Defense" or "Resistance")
        {
            double[] bucket = string.Equals(etm, "Defense", StringComparison.Ordinal) ? buffs.Defense : buffs.Resistance;
            if (!string.Equals(effect.DamageType, "None", StringComparison.Ordinal))
            {
                BuffsX.Add(bucket, maps.EDamage[effect.DamageType], value);
            }
            else
            {
                BuffsX.Add(buffs.Effect, effIndex, value);
            }

            return;
        }

        if (string.Equals(effectType, "DamageBuff", StringComparison.Ordinal))
        {
            // Only the Tertiary enhancement-effect arm of the Defiance exclusion reaches here: CanInclude already drops
            // every conditional or SpecialCase effect.
            if (!(effect.IsEnhancementEffect && string.Equals(effect.EffectClass, "Tertiary", StringComparison.Ordinal)))
            {
                BuffsX.Add(buffs.Damage, maps.EDamage[effect.DamageType], value);
            }

            return;
        }

        if (etm is "Accuracy" or "Heal")
        {
            return;
        }

        if (SpeedScalars.Contains(etm))
        {
            BuffsX.Add(string.Equals(effect.BuffMode, "Debuff", StringComparison.Ordinal) ? buffs.EffectAux : buffs.Effect, maps.EType[etm], value);
            return;
        }

        BuffsX.Add(buffs.Effect, effIndex, value);
    }
}
