using CohEngine.Core.Data;
using CohEngine.Core.Expressions;
using CohEngine.Core.Maths;
using CohEngine.Core.SetBonuses;

namespace CohEngine.Core.Totals;

/// <summary>
/// The lookups every magnitude read needs. <see cref="EnhMult"/> maps (build index, effect index) to the effect's
/// enhancement multiplier; an absent key is an unenhanced effect.
/// </summary>
internal sealed record MagContext
{
    private static readonly string[] SpeedEffectTypes = ["SpeedFlying", "SpeedRunning", "SpeedJumping"];

    /// <summary>Gets the AttribMod tables.</summary>
    public required AttribMods Mods { get; init; }

    /// <summary>Gets the class table.</summary>
    public required ArchetypeDb Classes { get; init; }

    /// <summary>Gets the build's class index.</summary>
    public required int ArchetypeIndex { get; init; }

    /// <summary>Gets the engine configuration.</summary>
    public required EngineConfig Config { get; init; }

    /// <summary>Gets the enhancement multipliers by (build index, effect index).</summary>
    public IReadOnlyDictionary<(int BuildIndex, int EffectIndex), double> EnhMult { get; init; } = new Dictionary<(int BuildIndex, int EffectIndex), double>();

    /// <summary>
    /// Gets the buffed endurance cost by build index for the powers whose effects read <c>power.base&gt;endcost</c>; the
    /// final magnitude of such an effect reads it, as Mids does from the buffed power. Empty reads the power's own cost.
    /// </summary>
    public IReadOnlyDictionary<int, double> BuffedEndCost { get; init; } = new Dictionary<int, double>();

    /// <summary>Gets the eEnhance names, None excluded.</summary>
    public required IReadOnlySet<string> EnhanceNames { get; init; }

    /// <summary>Gets the enum maps.</summary>
    public required EnumMaps Enums { get; init; }

    /// <summary>Gets the build state conditions and expressions read, or null.</summary>
    public ExpressionContext? Expr { get; init; }

    /// <summary>Gets a value indicating whether every conditional and SpecialCase effect counts; it exists for the hazard-gate test.</summary>
    public bool IncludeAllConditionals { get; init; }

    /// <summary>
    /// <c>Effect.CanInclude</c>. A SpecialCase effect stays excluded, as the character-state switch is not ported;
    /// this diverges from Mids, which keeps many default-true special cases.
    /// </summary>
    /// <param name="effect">The effect.</param>
    /// <returns>True when the effect counts.</returns>
    public bool CanInclude(Effect effect)
    {
        return IncludeAllConditionals || Conditionals.EffectIncluded(effect, Expr);
    }

    /// <summary>Whether a power is IsGlobalAccuracySource: the set-bonus virtual power or a GlobalBoost.</summary>
    /// <param name="power">The power.</param>
    /// <returns>True when its accuracy is global.</returns>
    public static bool IsGlobalAccSource(Power power)
    {
        return power.BuildIndex == Accumulator.SetBonusBuildIndex || string.Equals(power.PowerType, "GlobalBoost", StringComparison.Ordinal);
    }

    /// <summary>The effect's raw magnitude.</summary>
    /// <param name="effect">The effect.</param>
    /// <param name="power">The power that owns it.</param>
    /// <returns>The magnitude.</returns>
    public double Mag(Effect effect, Power power)
    {
        Power read = BuffedEndCost.TryGetValue(power.BuildIndex, out double endCost) ? power with { EndCost = endCost } : power;
        return EffectMagnitude.Mag(effect, read, Mods, Classes, ArchetypeIndex, Expr);
    }

    /// <summary><c>Effect.PvXInclude</c> with a non-null archetype.</summary>
    /// <param name="effect">The effect.</param>
    /// <returns>True when the effect counts in the configured mode for the build's class.</returns>
    public bool PvxInclude(Effect effect)
    {
        bool pvOk = (!string.Equals(effect.PvMode, "PvP", StringComparison.Ordinal) && !Config.DisablePve) || (!string.Equals(effect.PvMode, "PvE", StringComparison.Ordinal) && Config.DisablePve);
        bool classOk = effect.NIdClassName == -1 || effect.NIdClassName == ArchetypeIndex;
        return pvOk && classOk;
    }

    /// <summary>
    /// <c>Power.GetEffectMagSum</c> as (effect, magnitude) pairs. Each magnitude is the enhanced BuffedMag when the
    /// effect has a multiplier, raw Mag otherwise, then multiplied by its ticks for a stacking DoT.
    /// </summary>
    /// <param name="power">The power.</param>
    /// <param name="effectType">The eEffectType summed.</param>
    /// <param name="includeDelayed">Whether effects delayed past 5 s count.</param>
    /// <param name="onlySelf">Whether only self effects count.</param>
    /// <param name="onlyTarget">Whether only target effects count.</param>
    /// <param name="maxMode">Whether only Max-aspect effects count.</param>
    /// <returns>The contributing effects and their magnitudes.</returns>
    public List<(Effect Effect, double Mag)> EffectMagSum(Power power, string effectType, bool includeDelayed = false, bool onlySelf = false, bool onlyTarget = false, bool maxMode = false)
    {
        List<(Effect Effect, double Mag)> output = [];
        foreach (Effect effect in power.Effects)
        {
            bool flag = (string.Equals(effect.ToWho, "Target", StringComparison.Ordinal) && !onlySelf)
                || (string.Equals(effect.ToWho, "Self", StringComparison.Ordinal) && !onlyTarget)
                || string.Equals(effect.ToWho, "All", StringComparison.Ordinal);
            if (SpeedEffectTypes.Contains(effectType, StringComparer.Ordinal) && !maxMode && string.Equals(effect.Aspect, "Max", StringComparison.Ordinal))
            {
                flag = false;
            }

            if ((Config.Suppression & effect.Suppression) != 0)
            {
                flag = false;
            }

            if (!flag
                || effect.Probability <= 0
                || (maxMode && !string.Equals(effect.Aspect, "Max", StringComparison.Ordinal))
                || !string.Equals(effect.EffectType, effectType, StringComparison.Ordinal)
                || effect.EffectClass is "Ignored" or "Special"
                || (effect.DelayedTime > 5 && !includeDelayed)
                || !CanInclude(effect)
                || !PvxInclude(effect))
            {
                continue;
            }

            double mag = Mag(effect, power);
            if (EnhMult.TryGetValue((power.BuildIndex, effect.Index), out double mult))
            {
                mag = F32.Round(mag * mult);
            }

            if (effect.Ticks > 1 && string.Equals(effect.Stacking, "Yes", StringComparison.Ordinal))
            {
                mag = F32.Round(mag * effect.Ticks);
            }

            output.Add((effect, mag));
        }

        return output;
    }

    /// <summary>
    /// <c>Power.GetEnhancementMagSum(iEffect, -1)</c>: the Enhancement and DamageBuff effects whose ETModifies is
    /// <paramref name="effectType"/>, not aimed at a target, with their unenhanced magnitude.
    /// </summary>
    /// <param name="power">The power.</param>
    /// <param name="effectType">The eEffectType the effects modify.</param>
    /// <returns>The contributing effects and their magnitudes.</returns>
    public List<(Effect Effect, double Mag)> EnhancementMagSum(Power power, string effectType)
    {
        List<(Effect Effect, double Mag)> output = [];
        foreach (Effect effect in power.Effects)
        {
            if (effect.Probability <= 0
                || !string.Equals(effect.EtModifies, effectType, StringComparison.Ordinal)
                || effect.EffectType is not ("Enhancement" or "DamageBuff")
                || (effect.AbsorbedEffect && string.Equals(effect.AbsorbedPowerType, "GlobalBoost", StringComparison.Ordinal))
                || !CanInclude(effect)
                || !PvxInclude(effect)
                || string.Equals(effect.ToWho, "Target", StringComparison.Ordinal))
            {
                continue;
            }

            output.Add((effect, Mag(effect, power)));
        }

        return output;
    }
}
