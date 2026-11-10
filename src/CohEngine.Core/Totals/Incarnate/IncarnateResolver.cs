using CohEngine.Core.Data;
using CohEngine.Core.Maths;

using IncarnatePower = CohEngine.Core.Data.Incarnate;

namespace CohEngine.Core.Totals.Incarnate;

/// <summary>
/// GBPA_ApplyIncarnateEnhancements for the GrantPower-delivered incarnate enhancement, without the GrantPower
/// absorption subsystem: each granted sub-power's Enhancement and DamageBuff effects reach every target power whose
/// enhancement classes meet the sub-power's, either as a scalar aspect or as an effect-borne aspect, split pre-ED
/// (IgnoreED false) and post-ED (IgnoreED true).
/// </summary>
internal static class IncarnateResolver
{
    /// <summary>
    /// Folds every accepted incarnate aspect over every build power into the raw addend maps.
    /// </summary>
    /// <param name="incarnates">The StatInclude incarnate powers and their granted sub-powers.</param>
    /// <param name="powers">The build's powers.</param>
    /// <param name="mods">The AttribMod tables.</param>
    /// <param name="classes">The class table.</param>
    /// <param name="archetypeIndex">The build's class index.</param>
    /// <returns>The addends.</returns>
    /// <exception cref="NotSupportedException">E20 for an unported delivery path.</exception>
    public static IncarnateAddends Resolve(IEnumerable<IncarnatePower> incarnates, IReadOnlyList<Power> powers, AttribMods mods, ArchetypeDb classes, int archetypeIndex)
    {
        Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)> scalar = [];
        Dictionary<(int BuildIndex, int EffectIndex), (double PreEd, double PostEd)> effect = [];
        foreach (IncarnatePower incarnate in incarnates)
        {
            foreach (IncarnateSubPower sub in incarnate.SubPowers)
            {
                // The magnitude does not depend on the target power, so it is read once per sub-power effect.
                Power context = SubPowerContext(sub.FullName);
                double[] mags = [.. sub.Effects.Select(inc => EffectMagnitude.Mag(inc, context, mods, classes, archetypeIndex))];
                foreach (Power power in powers)
                {
                    if (!sub.Enhancements.Overlaps(power.Enhancements))
                    {
                        continue;
                    }

                    for (int i = 0; i < sub.Effects.Count; i++)
                    {
                        RouteEffect(sub.Effects[i], mags[i], sub.FullName, power, scalar, effect);
                    }
                }
            }
        }

        return new IncarnateAddends(scalar, effect);
    }

    /// <summary>
    /// The scalar aspect an incarnate effect folds into, or null when it is effect-borne or a gated aspect the target
    /// ignores; Mids' switch then falls to the effect default, which finds no matching scalar effect.
    /// </summary>
    /// <param name="effect">The incarnate effect.</param>
    /// <param name="power">The target power.</param>
    /// <returns>The eEnhance aspect, or null.</returns>
    internal static string? ScalarAspect(Effect effect, Power power)
    {
        (string Aspect, bool Gated)? route = effect.EtModifies switch
        {
            "Accuracy" => ("Accuracy", true),
            "EnduranceDiscount" => ("EnduranceDiscount", true),
            "InterruptTime" => ("Interrupt", false),
            "Range" => ("Range", false),
            "RechargeTime" => ("RechargeTime", true),
            _ => null,
        };
        if (route is not { } found || (found.Gated && power.IgnoreEnh.Contains(found.Aspect, StringComparer.Ordinal)))
        {
            return null;
        }

        return found.Aspect;
    }

    /// <summary>
    /// <c>HandleDefaultIncarnateEnh</c>'s match: a DamageBuff meets a Resistance or Damage target of its damage type;
    /// otherwise the target's type must equal ETModifies, Damage and Defense also matching on damage type.
    /// </summary>
    /// <param name="target">The target effect.</param>
    /// <param name="inc">The incarnate effect.</param>
    /// <returns>True when the incarnate effect enhances the target.</returns>
    internal static bool MatchesEffect(Effect target, Effect inc)
    {
        if (!target.Buffable)
        {
            return false;
        }

        if (string.Equals(inc.EffectType, "DamageBuff", StringComparison.Ordinal))
        {
            return target.EffectType is "Resistance" or "Damage" && string.Equals(target.DamageType, inc.DamageType, StringComparison.Ordinal);
        }

        if (!string.Equals(target.EffectType, inc.EtModifies, StringComparison.Ordinal))
        {
            return false;
        }

        return inc.EtModifies is not ("Damage" or "Defense") || string.Equals(target.DamageType, inc.DamageType, StringComparison.Ordinal);
    }

    /// <summary>Refuses the incarnate delivery paths this port does not apply yet.</summary>
    /// <param name="effect">The incarnate effect.</param>
    /// <param name="subFullName">The sub-power's full name.</param>
    /// <exception cref="NotSupportedException">E20 for a GrantPower or GlobalChanceMod sub-effect, or a Mez aspect.</exception>
    internal static void GuardSupported(Effect effect, string subFullName)
    {
        if (effect.EffectType is not ("Enhancement" or "DamageBuff"))
        {
            throw new NotSupportedException(
                $"E20: incarnate sub-power '{subFullName}' carries a '{effect.EffectType}' effect; only "
                + "Enhancement- and DamageBuff-delivered incarnate aspects are ported (GrantPower/GlobalChanceMod "
                + "need a nested-GrantPower / Hybrid fixture first)");
        }

        if (string.Equals(effect.EtModifies, "Mez", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"E20: incarnate sub-power '{subFullName}' enhances Mez; the magnitude/duration split of an "
                + "incarnate Mez aspect is unported — add a Nerve-style fixture before applying it");
        }
    }

    /// <summary>Accumulates a magnitude into the pre-ED (IgnoreED false) or post-ED (true) slot of a key.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="bucket">The bucket.</param>
    /// <param name="key">The key.</param>
    /// <param name="mag">The magnitude.</param>
    /// <param name="ignoreEd">Whether the effect ignores ED.</param>
    internal static void AddSplit<TKey>(Dictionary<TKey, (double PreEd, double PostEd)> bucket, TKey key, double mag, bool ignoreEd)
        where TKey : notnull
    {
        (double pre, double post) = bucket.GetValueOrDefault(key, (0.0, 0.0));
        bucket[key] = ignoreEd ? (pre, F32.Round(post + mag)) : (F32.Round(pre + mag), post);
    }

    private static void RouteEffect(
        Effect inc,
        double mag,
        string subFullName,
        Power power,
        Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)> scalar,
        Dictionary<(int BuildIndex, int EffectIndex), (double PreEd, double PostEd)> effect)
    {
        if (ScalarAspect(inc, power) is { } aspect)
        {
            AddSplit(scalar, (power.BuildIndex, aspect), mag, inc.IgnoreEd);
            return;
        }

        GuardSupported(inc, subFullName);
        foreach (Effect target in power.Effects.Where(target => MatchesEffect(target, inc)))
        {
            AddSplit(effect, (power.BuildIndex, target.Index), mag, inc.IgnoreEd);
        }
    }

    /// <summary>
    /// The power the sub-power magnitude reads: no ForcedClass and no variable scaling, so GetModifier takes the
    /// build's class at the fixed math level.
    /// </summary>
    private static Power SubPowerContext(string fullName)
    {
        return new Power
        {
            BuildIndex = -1,
            NidPower = -1,
            FullName = fullName,
            StaticIndex = -1,
            PowerType = string.Empty,
            ForcedClass = string.Empty,
            ClickBuff = false,
            Level = 0,
            EndCost = 0.0,
            ActivatePeriod = 0.0,
            ToggleCost = 0.0,
            VariableEnabled = false,
            StatInclude = false,
            VariableValue = 0,
            Effects = [],
            SetTypes = [],
            Enhancements = [],
            PickLevel = 0,
            RechargeTime = 0.0,
            CastTime = 0.0,
            InterruptTime = 0.0,
            IgnoreEnh = [],
        };
    }
}
